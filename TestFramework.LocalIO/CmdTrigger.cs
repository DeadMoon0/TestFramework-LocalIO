using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
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
/// <param name="workingDirectory">The working directory for the command.</param>
public class CmdTrigger(VariableReference<string> command, VariableReference<string> workingDirectory) : Step<CmdResultContext>
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
    public override async Task<CmdResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string? cmdText = command.GetValue(variableStore);
        if (cmdText is null)
            throw new InvalidOperationException("CmdTrigger command is null");
        string workingDir = workingDirectory.GetValue(variableStore) ?? Environment.CurrentDirectory;
        ProcessStartInfo info;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            info = new ProcessStartInfo
            {
                FileName = "CMD.EXE",
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
            string shellArgsPrefix = "-c ";
            info = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgsPrefix + cmdText,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        }
        Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not Start the CMD Process");
        await process.WaitForExitAsync(cancellationToken);
        string outStd = process.StandardOutput.ReadToEnd();
        string errorStd = process.StandardError.ReadToEnd();

        if (!String.IsNullOrWhiteSpace(outStd)) logger.LogInformation(outStd);
        if (!String.IsNullOrWhiteSpace(errorStd)) logger.LogWarning("[External stderr]\n" + errorStd);

        return new CmdResultContext(process.ExitCode);
    }

    /// <inheritdoc />
    public override StepInstance<Step<CmdResultContext>, CmdResultContext> GetInstance() => new StepInstance<Step<CmdResultContext>, CmdResultContext>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        if (command.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(command.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (workingDirectory.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(workingDirectory.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }
}
