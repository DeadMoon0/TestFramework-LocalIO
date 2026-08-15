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

        // The step has to give up marginally before its own timeout to say anything useful.
        // CoreRunner awaits the step with Task.WaitAsync(token), which throws the instant the
        // timeout token fires and abandons the running task - so an exception the step raises at
        // that same moment is never observed, and the caller only ever sees the generic
        // "Step '...' timed out". Finishing first is the only way the path reaches the user.
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan ownDeadline = GetOwnDeadline(variableStore);
        if (ownDeadline > TimeSpan.Zero)
            deadline.CancelAfter(ownDeadline);

        try
        {
            return await base.DoEventPolling(serviceProvider, variableStore, artifactStore, logger, deadline.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The file \"{resolvedPath}\" never appeared. Check that the producing step really writes to this path - "
                + "a relative path resolves against the LocalIO run directory when the timeline calls UseRunDirectory(), "
                + "and against the process working directory otherwise, so a mismatched working directory is the usual cause.",
                exception);
        }
    }

    /// <summary>
    /// Returns the deadline this event enforces itself, slightly ahead of the step timeout.
    /// </summary>
    /// <remarks>
    /// The margin has to cover more than the polling itself. This deadline is measured from the
    /// moment the event starts polling, while the step timeout is measured from the moment the step
    /// starts, and on a loaded machine the gap between those two is real - large enough, on a
    /// two-core CI runner, to swallow a small margin entirely and let the generic message win.
    /// So: a sixth of the timeout, never below 200 ms, never above a second so that a long wait is
    /// not meaningfully shortened. Earlier versions used 50 ms and then 100 ms; CI lost both.
    /// </remarks>
    private TimeSpan GetOwnDeadline(VariableStore variableStore)
    {
        TimeSpan stepTimeout = TimeOutOptions.TimeOut.GetValue(variableStore);
        if (stepTimeout <= TimeSpan.Zero || stepTimeout > TimeSpan.FromDays(1))
            return TimeSpan.Zero;

        double marginMs = Math.Clamp(stepTimeout.TotalMilliseconds / 6, 200, 1000);

        // A timeout too short to carry the margin keeps a usable slice rather than going negative.
        return marginMs >= stepTimeout.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(stepTimeout.TotalMilliseconds / 2)
            : stepTimeout - TimeSpan.FromMilliseconds(marginMs);
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