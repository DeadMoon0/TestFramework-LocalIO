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
    /// <remarks>
    /// Logs the resolved path once and turns a cancellation into a <see cref="TimeoutException"/>
    /// that names the file, so a timeout says WHICH path was watched instead of only that some
    /// file-exists event expired. <c>CoreRunner</c> catches <see cref="TimeoutException"/> before
    /// <see cref="OperationCanceledException"/>, so the message survives verbatim.
    /// </remarks>
    public override async Task<EmptyStepResultContext?> DoEventPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string resolvedPath = ResolvePath(variableStore);
        logger.LogInformation($"Waiting for the file \"{resolvedPath}\" to appear.");

        // CoreRunner races the step's cancellation token against its own Task.WaitAsync(timeout),
        // and the plain "The operation has timed out." of that WaitAsync wins whenever the step
        // has to unwind first. Giving up a hair earlier on our own deadline lets the descriptive
        // message get there first, where CoreRunner's `catch (TimeoutException)` preserves it verbatim.
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan ownDeadline = GetOwnDeadline(variableStore);
        if (ownDeadline > TimeSpan.Zero)
            deadline.CancelAfter(ownDeadline);

        try
        {
            return await base.DoEventPolling(serviceProvider, variableStore, artifactStore, logger, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The file \"{resolvedPath}\" never appeared. Check that the producing step really writes to this path - "
                + "a relative path resolves against the LocalIO run directory when the timeline calls UseRunDirectory(), "
                + "and against the process working directory otherwise, so a mismatched working directory is the usual cause.");
        }
    }

    /// <summary>
    /// Returns the deadline this event enforces itself, slightly ahead of the step timeout.
    /// </summary>
    private TimeSpan GetOwnDeadline(VariableStore variableStore)
    {
        TimeSpan stepTimeout = TimeOutOptions.TimeOut.GetValue(variableStore);
        if (stepTimeout <= TimeSpan.Zero || stepTimeout > TimeSpan.FromDays(1)) return TimeSpan.Zero;

        TimeSpan margin = TimeSpan.FromMilliseconds(Math.Min(50, stepTimeout.TotalMilliseconds * 0.1));
        return stepTimeout - margin;
    }

    /// <inheritdoc />
    public override Task<SequentialPollingResult<EmptyStepResultContext>> OnSequentialPolling(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string _path = ResolvePath(variableStore);
        return Task.FromResult(new SequentialPollingResult<EmptyStepResultContext>(File.Exists(_path), null, pollDelay?.GetValue(variableStore) ?? TimeSpan.FromSeconds(0.5)));
    }

    private string ResolvePath(VariableStore variableStore)
    {
        string raw = path.GetValue(variableStore) ?? throw new ArgumentNullException(nameof(path), "The Path cannot be Null.");
        return LocalPath.Resolve(raw, variableStore);
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