using System.Diagnostics;
using System.Runtime.Versioning;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Executes a Windows command through <c>CMD.EXE</c> and returns the process exit code.
/// </summary>
/// <param name="command">The command to execute.</param>
/// <param name="workingDirectory">The working directory for the command.</param>
[SupportedOSPlatform("windows")]
public class CmdTrigger(VariableReference<string> command, VariableReference<string> workingDirectory) : Step<int>
{
    /// <inheritdoc />
    public override bool DoesReturn => true;

    /// <inheritdoc />
    public override string Name => "Cmd Trigger";

    /// <inheritdoc />
    public override string Description => "A trigger that executes a Windows CMD command.";

    /// <inheritdoc />
    public override Step<int> Clone()
    {
        return new CmdTrigger(command, workingDirectory).WithClonedOptions(this);
    }

    /// <inheritdoc />
    public override async Task<int> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = "CMD.EXE",
            Arguments = "/C " + command.GetValue(variableStore),
            UseShellExecute = false,
            WorkingDirectory = workingDirectory.GetValue(variableStore),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not Start the CMD Process");
        await process.WaitForExitAsync(cancellationToken);
        string outStd = process.StandardOutput.ReadToEnd();
        string errorStd = process.StandardError.ReadToEnd();

        if (!String.IsNullOrWhiteSpace(outStd)) logger.LogInformation(outStd);
        if (!String.IsNullOrWhiteSpace(errorStd)) logger.LogWarning("[External stderr]\n" + errorStd);

        return process.ExitCode;
    }

    /// <inheritdoc />
    public override StepInstance<Step<int>, int> GetInstance() => new StepInstance<Step<int>, int>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        if (command.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(command.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (workingDirectory.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(workingDirectory.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }
}
