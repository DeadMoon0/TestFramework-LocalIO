using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Executes a shell command and returns the process exit code.
/// On Windows this uses <c>CMD.EXE /C</c>; on Unix-like systems it prefers <c>bash -c</c>
/// and falls back to <c>/bin/sh -c</c> if bash is not present.
/// </summary>
public class CmdTrigger : Step<CmdResultContext>
{
    private readonly VariableReference<string> command;
    private readonly VariableReference<string>? workingDirectory;

    // Internal: the one way to build this step is the factory, which already offers each shape as its own
    // overload. A public constructor beside it was a second way, and its optional parameter was a signature every
    // compiled caller would have depended on.
    internal CmdTrigger(VariableReference<string> command, VariableReference<string>? workingDirectory)
    {
        this.command = command;
        this.workingDirectory = workingDirectory;
    }

    /// <inheritdoc />
    public override bool DoesReturn => true;

    /// <inheritdoc />
    public override string Name => "Cmd Trigger";

    /// <inheritdoc />
    public override string Description => "A trigger that executes a shell command and returns the process exit code.";

    /// <inheritdoc />
    public override Step<CmdResultContext> Clone()
    {
        return new CmdTrigger(command, workingDirectory).WithClonedOptions(this);
    }

    /// <inheritdoc />
    public override async Task<CmdResultContext?> Execute(RunContext context)
    {
        string? cmdText = command.GetValue(context.Variables);
        if (cmdText is null)
            throw new FrameworkStateException("CmdTrigger command is null.");
        string? configuredWorkingDir = workingDirectory?.GetValue(context.Variables);
        string workingDir = configuredWorkingDir is null
            ? LocalPath.TryGetRunDirectory(context.Variables) ?? Environment.CurrentDirectory
            : LocalPath.Resolve(configuredWorkingDir, context.Variables);
        ProcessStartInfo info;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            info = new ProcessStartInfo
            {
                FileName = "CMD.EXE",
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
        else
        {
            // Prefer bash if available, otherwise fall back to sh
            string shell = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";

            // Through setsid where there is one, so the shell leads a process group its children stay in, and
            // the group can be stopped after the shell itself has exited.
            info = new ProcessStartInfo
            {
                FileName = CommandProcessTree.SetSid ?? shell,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (CommandProcessTree.SetSid is not null)
                info.ArgumentList.Add(shell);

            // The whole command must arrive as ONE argv entry. Assigning it to Arguments would
            // tokenize it, so "echo hello > out.txt" would run bare "echo" with $0 = "hello".
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(cmdText);
        }

        using Process process = Process.Start(info) ?? throw new FrameworkStateException("Could not start the CMD process.");
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
        catch (OperationCanceledException)
        {
            // The shell may have exited already, with what it started still running: the tree is stopped as a
            // whole, not walked from a root that may be gone.
            tree.Kill();
            Observe(standardOutput.Completion);
            Observe(standardError.Completion);
            throw;
        }

        string outStd = standardOutput.Text;
        string errorStd = standardError.Text;

        if (!String.IsNullOrWhiteSpace(outStd)) context.Logger.LogInformation(outStd);
        if (!String.IsNullOrWhiteSpace(errorStd)) context.Logger.LogWarning("[External stderr]\n" + errorStd);

        return new CmdResultContext(process.ExitCode, outStd, errorStd, cmdText, workingDir);
    }

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
        contract.Input(workingDirectory);
    }
}
