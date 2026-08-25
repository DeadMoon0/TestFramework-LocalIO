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
/// <param name="command">The command to execute.</param>
/// <param name="workingDirectory">
/// The working directory for the command. When omitted the command runs in the LocalIO run
/// directory, or - when the timeline declared none - in <see cref="Environment.CurrentDirectory"/>
/// as it is at RUN time.
/// </param>
public class CmdTrigger(VariableReference<string> command, VariableReference<string>? workingDirectory = null) : Step<CmdResultContext>
{
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
            info = new ProcessStartInfo
            {
                FileName = shell,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            // The whole command must arrive as ONE argv entry. Assigning it to Arguments would
            // tokenize it, so "echo hello > out.txt" would run bare "echo" with $0 = "hello".
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(cmdText);
        }

        using Process process = Process.Start(info) ?? throw new FrameworkStateException("Could not start the CMD process.");

        // Drain both pipes concurrently BEFORE waiting for exit: a child that writes more than the
        // pipe buffer (~4 KB on Windows, ~64 KB on Linux) blocks forever otherwise.
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(context.Deadline.Token);
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(context.Deadline.Token);

        string outStd;
        string errorStd;
        try
        {
            await process.WaitForExitAsync(context.Deadline.Token);
            outStd = await standardOutputTask;
            errorStd = await standardErrorTask;
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process, context.Logger);
            Observe(standardOutputTask);
            Observe(standardErrorTask);
            throw;
        }

        if (!String.IsNullOrWhiteSpace(outStd)) context.Logger.LogInformation(outStd);
        if (!String.IsNullOrWhiteSpace(errorStd)) context.Logger.LogWarning("[External stderr]\n" + errorStd);

        return new CmdResultContext(process.ExitCode, outStd, errorStd, cmdText, workingDir);
    }

    /// <summary>
    /// Kills the shell and everything it spawned. Killing only the shell would orphan the real
    /// command, because both <c>CMD.EXE /C</c> and <c>bash -c</c> run it as a grandchild.
    /// </summary>
    private static void KillProcessTree(Process process, ScopedLogger logger)
    {
        try
        {
            if (process.HasExited) return;
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited and its handle is gone - nothing left to kill.
            return;
        }
        catch (Win32Exception exception)
        {
            logger.LogWarning($"Could not kill the cancelled command process tree: {exception.Message}");
            return;
        }
        catch (NotSupportedException exception)
        {
            logger.LogWarning($"Could not kill the cancelled command process tree: {exception.Message}");
            return;
        }

        try
        {
            // Bounded wait so the handle is released and the redirected pipes close.
            process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
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
        if (command.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(command.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (workingDirectory is not null && workingDirectory.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(workingDirectory.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }
}
