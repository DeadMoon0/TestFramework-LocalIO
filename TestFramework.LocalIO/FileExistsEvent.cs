using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

public class FileExistsEvent(VariableReference<string> path, VariableReference<TimeSpan>? pollDelay = null) : SequentialEvent<FileExistsEvent, object?>
{
    public override string Name => "File Exists Event";
    public override string Description => "Completes if the File Exists";

    public override bool DoesReturn => false;

    public override Step<object?> Clone()
    {
        return new FileExistsEvent(path, pollDelay).WithClonedOptions(this);
    }

    public override async Task<SequentialPollingResult<object?>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string _path = path.GetValue(variableStore) ?? throw new ArgumentNullException(nameof(path), "The Path cannot be Null.");
        return new SequentialPollingResult<object?>(File.Exists(_path), null, pollDelay?.GetValue(variableStore) ?? TimeSpan.FromSeconds(0.5));
    }

    public override void DeclareIO(StepIOContract contract)
    {
        if (path.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(path.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (pollDelay is not null && pollDelay.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(pollDelay.Identifier!.Identifier, StepIOKind.Variable, false, typeof(System.TimeSpan)));
    }
}