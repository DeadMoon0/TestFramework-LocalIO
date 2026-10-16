using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Executes a command and returns the process exit code.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes, built through <see cref="LocalIOTrigger"/>. <c>Cmd</c> hands one line to the platform shell -
/// <c>%ComSpec% /C</c> on Windows, <c>bash -c</c> (or <c>/bin/sh -c</c>) elsewhere - and inserts every value
/// verbatim, so a run value that reaches it is shell syntax. <c>Exec</c> starts a program directly with each
/// argument passed as its own argument, no shell, so a value is only ever a value.
/// </para>
/// <para>
/// Each stream keeps its last <see cref="PipeCapture.Limit"/> characters; the log shows the last
/// <see cref="LogTail"/> of them.
/// </para>
/// </remarks>
public class CmdTrigger : Step<CmdResultContext>
{
    /// <summary>The most of one stream that is written to the run's log; the step's result keeps more.</summary>
    internal const int LogTail = 8 * 1024;

    private readonly VariableReference<string>? command;
    private readonly VariableReference<string>? program;
    private readonly IReadOnlyList<VariableReference<string>> arguments;
    private readonly VariableReference<string>? workingDirectory;

    // Internal: the one way to build this step is the factory, which already offers each shape as its own
    // overload. A public constructor beside it was a second way, and its optional parameter was a signature every
    // compiled caller would have depended on.
    private CmdTrigger(
        VariableReference<string>? command,
        VariableReference<string>? program,
        IReadOnlyList<VariableReference<string>> arguments,
        VariableReference<string>? workingDirectory)
    {
        this.command = command;
        this.program = program;
        this.arguments = arguments;
        this.workingDirectory = workingDirectory;
    }

    internal static CmdTrigger Shell(VariableReference<string> command, VariableReference<string>? workingDirectory)
        => new(command, null, [], workingDirectory);

    internal static CmdTrigger Direct(VariableReference<string> program, IReadOnlyList<VariableReference<string>> arguments, VariableReference<string>? workingDirectory)
        => new(null, program, [.. arguments], workingDirectory);

    /// <inheritdoc />
    public override bool DoesReturn => true;

    /// <inheritdoc />
    public override string Name => program is null ? "Cmd Trigger" : "Exec Trigger";

    /// <inheritdoc />
    public override string Description => program is null
        ? "A trigger that executes a shell command and returns the process exit code."
        : "A trigger that starts a program with its arguments, no shell, and returns the process exit code.";

    /// <inheritdoc />
    public override Step<CmdResultContext> Clone()
    {
        return new CmdTrigger(command, program, arguments, workingDirectory).WithClonedOptions(this);
    }

    /// <inheritdoc />
    public override async Task<CmdResultContext?> Execute(RunContext context)
    {
        string workingDir = ResolveWorkingDirectory(context);
        ProcessStartInfo info = program is null
            ? ShellStart(Required(command!, "command", context), workingDir)
            : DirectStart(Required(program, "program", context), [.. arguments.Select(argument => Required(argument, "argument", context))], workingDir);
        string commandLine = Describe(info);

        using Process process = Start(info, commandLine, workingDir);
        using CommandProcessTree tree = CommandProcessTree.Own(process, startedThroughSetSid: !OperatingSystem.IsWindows() && CommandProcessTree.SetSid is not null);

        // Drain both pipes concurrently BEFORE waiting for exit: a child that writes more than the
        // pipe buffer (~4 KB on Windows, ~64 KB on Linux) blocks forever otherwise.
        using CancellationTokenSource drain = CancellationTokenSource.CreateLinkedTokenSource(context.Deadline.Token);
        PipeCapture standardOutput = PipeCapture.Start(process.StandardOutput, drain.Token);
        PipeCapture standardError = PipeCapture.Start(process.StandardError, drain.Token);

        try
        {
            await process.WaitForExitAsync(context.Deadline.Token);
            await DrainAsync(standardOutput, standardError, drain, tree, context);
        }
        catch (OperationCanceledException) when (context.Deadline.HasExpired)
        {
            // The shell may have exited already, with what it started still running: the tree is stopped as a
            // whole, not walked from a root that may be gone. What the command wrote until now is the account of
            // why it did not finish, so it goes into the failure rather than being thrown away with the pipes.
            tree.Kill();
            Observe(standardOutput.Completion);
            Observe(standardError.Completion);
            // A TimeoutException, like every step's own account of its deadline, so it is filed as the step's timeout.
            throw new TimeoutException(
                $"The command '{commandLine}' was still running when the step's {context.Deadline.Total} ran out, and was stopped."
                + Partial(standardOutput.Text, standardError.Text));
        }
        catch (OperationCanceledException)
        {
            tree.Kill();
            Observe(standardOutput.Completion);
            Observe(standardError.Completion);
            LogStreams(context, standardOutput.Text, standardError.Text);
            throw;
        }

        string outStd = standardOutput.Text;
        string errorStd = standardError.Text;
        LogStreams(context, outStd, errorStd);

        return new CmdResultContext(process.ExitCode, outStd, errorStd, commandLine, workingDir);
    }

    private string ResolveWorkingDirectory(RunContext context)
    {
        string? configuredWorkingDir = workingDirectory?.GetValue(context.Variables);
        string workingDir = configuredWorkingDir is null
            ? LocalPath.TryGetRunDirectory(context.Variables) ?? Environment.CurrentDirectory
            : LocalPath.Resolve(configuredWorkingDir, context.Variables);

        // Checked by name: left to the process, a missing directory surfaces as a Win32Exception that names
        // neither the directory nor the command.
        if (!Directory.Exists(workingDir))
        {
            throw new FrameworkConfigurationException($"The command's working directory '{workingDir}' does not exist.")
            {
                RecoverySteps =
                [
                    "Create the directory in an earlier step, or point the command at one that exists.",
                    "A relative directory resolves against the run directory when the timeline declared one (UseRunDirectory), otherwise against the process working directory.",
                ],
            };
        }

        return workingDir;
    }

    private static string Required(VariableReference<string> reference, string what, RunContext context)
        => reference.GetValue(context.Variables)
            ?? throw new FrameworkStateException($"The {what} of the command resolved to null.");

    private static ProcessStartInfo ShellStart(string cmdText, string workingDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                // The shell Windows itself names, not whatever "cmd.exe" a PATH entry finds first.
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                // Intentionally NOT ArgumentList: CMD.EXE /C consumes the raw remainder of the
                // command line. Going through ArgumentList would quote anything containing a
                // space and break operators such as &&, > and 1>&2.
                Arguments = "/C " + cmdText,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        }

        // Prefer bash if available, otherwise fall back to sh
        string shell = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";

        // The whole command must arrive as ONE argv entry. Assigning it to Arguments would
        // tokenize it, so "echo hello > out.txt" would run bare "echo" with $0 = "hello".
        return UnixStart(shell, ["-c", cmdText], workingDir);
    }

    private static ProcessStartInfo DirectStart(string fileName, IReadOnlyList<string> args, string workingDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in args)
                info.ArgumentList.Add(argument);

            return info;
        }

        return UnixStart(fileName, args, workingDir);
    }

    // Through setsid where there is one, so the process leads a group its children stay in, and the group can be
    // stopped after the process itself has exited.
    private static ProcessStartInfo UnixStart(string fileName, IReadOnlyList<string> args, string workingDir)
    {
        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = CommandProcessTree.SetSid ?? fileName,
            UseShellExecute = false,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (CommandProcessTree.SetSid is not null)
            info.ArgumentList.Add(fileName);

        foreach (string argument in args)
            info.ArgumentList.Add(argument);

        return info;
    }

    private static Process Start(ProcessStartInfo info, string commandLine, string workingDir)
    {
        try
        {
            return Process.Start(info) ?? throw new FrameworkStateException($"The command '{commandLine}' could not be started.");
        }
        catch (Win32Exception exception)
        {
            throw new FrameworkConfigurationException(
                $"The command '{commandLine}' could not be started in '{workingDir}': {exception.Message}", exception)
            {
                RecoverySteps = ["Check that the program exists and is on PATH, or give its full path."],
            };
        }
    }

    // What the result and the messages call the command: the line as given for a shell command, and the program
    // with its arguments - quoted where a reader would otherwise misread them - for one started directly.
    private string Describe(ProcessStartInfo info)
    {
        if (program is null)
            return info.ArgumentList.Count > 0 ? info.ArgumentList[^1] : info.Arguments["/C ".Length..];

        IEnumerable<string> parts = info.ArgumentList.Count > 0 && CommandProcessTree.SetSid is not null && !OperatingSystem.IsWindows()
            ? info.ArgumentList
            : [info.FileName, .. info.ArgumentList];

        return string.Join(" ", parts.Select(Quote));

        static string Quote(string part)
            => part.Length > 0 && !part.Any(character => char.IsWhiteSpace(character) || character == '"')
                ? part
                : "\"" + part.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void LogStreams(RunContext context, string outStd, string errorStd)
    {
        if (!string.IsNullOrWhiteSpace(outStd)) context.Logger.LogInformation(Tail(outStd));
        if (!string.IsNullOrWhiteSpace(errorStd)) context.Logger.LogWarning("[External stderr]\n" + Tail(errorStd));
    }

    private static string Partial(string outStd, string errorStd)
    {
        string partial = string.Empty;
        if (!string.IsNullOrWhiteSpace(outStd)) partial += $"\nIt had written to stdout:\n{Tail(outStd)}";
        if (!string.IsNullOrWhiteSpace(errorStd)) partial += $"\nIt had written to stderr:\n{Tail(errorStd)}";

        return partial.Length == 0 ? "\nIt had written nothing." : partial;
    }

    private static string Tail(string text)
        => text.Length <= LogTail
            ? text
            : $"[last {LogTail} of {text.Length} characters]\n{text[^LogTail..]}";

    /// <summary>
    /// How long the pipes may stay open after the shell has exited.
    /// </summary>
    /// <remarks>
    /// The last writes of a command that has finished arrive within milliseconds. A pipe still open after this is
    /// held by something the command started and left running - <c>start /b</c>, a trailing <c>&amp;</c> - which
    /// will not close it until it ends.
    /// </remarks>
    internal static readonly TimeSpan PipeGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Waits for both pipes to close; if something the command left running still holds them after the grace,
    /// stops it and keeps what was written.
    /// </summary>
    /// <remarks>
    /// The step used to wait for the end of the pipes, so <c>cmd /c start /b worker</c> - which exits at once - sat
    /// until the step's deadline and was reported as a timeout although it had succeeded, and the worker outlived the
    /// run. A child that redirected its own output holds no pipe and is left alone, as a detached process should be.
    /// </remarks>
    private static async Task DrainAsync(PipeCapture standardOutput, PipeCapture standardError, CancellationTokenSource drain, CommandProcessTree tree, RunContext context)
    {
        Task both = Task.WhenAll(standardOutput.Completion, standardError.Completion);
        Task finished = await Task.WhenAny(both, Task.Delay(PipeGrace, context.Deadline.Token));

        if (finished == both)
        {
            await both;
            return;
        }

        context.Deadline.Token.ThrowIfCancellationRequested();

        context.Logger.LogWarning(
            $"The command exited, and something it started was still holding its output {PipeGrace.TotalSeconds:0} s later. "
            + "It was stopped, and the output written until then is the step's result. Redirect a process meant to keep running (> file 2>&1).");

        tree.Kill();
        await drain.CancelAsync();
        Observe(standardOutput.Completion);
        Observe(standardError.Completion);
    }

    /// <summary>
    /// Marks an abandoned pipe read as observed so its cancellation does not surface as an
    /// unobserved task exception.
    /// </summary>
    private static void Observe(Task task)
    {
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <inheritdoc />
    public override StepInstance<Step<CmdResultContext>, CmdResultContext> GetInstance() => new StepInstance<Step<CmdResultContext>, CmdResultContext>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(command);
        contract.Input(program);
        foreach (VariableReference<string> argument in arguments)
            contract.Input(argument);
        contract.Input(workingDirectory);
    }
}
