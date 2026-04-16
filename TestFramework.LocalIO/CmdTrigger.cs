using System.Diagnostics;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFrameworkLocalIO;

public class CmdTrigger(VariableReference<string> command, VariableReference<string> workingDirectory) : Step<int>
{
    public override bool DoesReturn => true;

    public override string Name => "Cmd Trigger";
    public override string Description => "A Trigger which executes a Cmd Command";

    public override Step<int> Clone()
    {
        return new CmdTrigger(command, workingDirectory).WithClonedOptions(this);
    }

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

    public override StepInstance<Step<int>, int> GetInstance() => new StepInstance<Step<int>, int>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (command.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(command.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (workingDirectory.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(workingDirectory.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }
}
