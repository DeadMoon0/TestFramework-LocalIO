using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Polls until a file exists at the resolved path.
/// </summary>
/// <param name="path">The file path to watch.</param>
/// <param name="pollDelay">The delay between polling attempts. Defaults to 500 ms when omitted.</param>
public class FileExistsEvent(VariableReference<string> path, VariableReference<TimeSpan>? pollDelay = null) : SequentialEvent<FileExistsEvent, EmptyStepResultContext>
{
    /// <inheritdoc />
    public override string Name => "File Exists Event";

    /// <inheritdoc />
    public override string Description => "Completes when the target file exists.";

    /// <inheritdoc />
    public override bool DoesReturn => false;

    /// <inheritdoc />
    public override Step<EmptyStepResultContext> Clone()
    {
        return new FileExistsEvent(path, pollDelay).WithClonedOptions(this);
    }

    /// <inheritdoc />
    public override async Task<SequentialPollingResult<EmptyStepResultContext>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string _path = LocalPath.Resolve(
            path.GetValue(variableStore) ?? throw new ArgumentNullException(nameof(path), "The Path cannot be Null."),
            variableStore);
        return new SequentialPollingResult<EmptyStepResultContext>(File.Exists(_path), null, pollDelay?.GetValue(variableStore) ?? TimeSpan.FromSeconds(0.5));
    }

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        if (path.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(path.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (pollDelay is not null && pollDelay.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(pollDelay.Identifier!.Identifier, StepIOKind.Variable, false, typeof(System.TimeSpan)));
    }
}