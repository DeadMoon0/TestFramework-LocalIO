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
    /// <para>
    /// Logs the resolved path once and turns running out of time into a <see cref="TimeoutException"/>
    /// that names the file, so a timeout says WHICH path was watched instead of only that some
    /// file-exists event expired.
    /// </para>
    /// <para>
    /// This used to cancel itself a sixth of the timeout early, because the runner abandoned a step at the
    /// instant it cancelled it, so anything the step threw then went unobserved. The margin was tuned
    /// twice and CI lost both attempts. Core waits a grace window now and surfaces whatever the step says
    /// in it, so there is nothing left to out-run.
    /// </para>
    /// </remarks>
    public override async Task<EmptyStepResultContext?> DoEventPolling(RunContext context)
    {
        string resolvedPath = ResolvePath(context.Variables);
        context.Logger.LogInformation($"Waiting for the file \"{resolvedPath}\" to appear.");

        try
        {
            return await base.DoEventPolling(context);
        }

        // Only when the time actually ran out. A run cancelled from outside is not the file's fault, and
        // claiming the file never appeared would blame the wrong thing. Asked of the deadline rather than
        // worked out from Remaining: the arithmetic version reads the edge wrong under load.
        catch (OperationCanceledException exception) when (context.Deadline.HasExpired)
        {
            // Re-resolved so the message names the path that was actually polled last: the path rides
            // a variable, and a variable rewritten mid-wait would otherwise make the timeout name a
            // path this event stopped watching. Best effort - the initial resolution is the fallback.
            string lastPolled;
            try
            {
                lastPolled = ResolvePath(context.Variables);
            }
            catch
            {
                lastPolled = resolvedPath;
            }

            string watched = string.Equals(lastPolled, resolvedPath, StringComparison.Ordinal)
                ? $"\"{resolvedPath}\""
                : $"\"{lastPolled}\" (initially \"{resolvedPath}\" - the path variable changed while waiting)";

            throw new TimeoutException(
                $"The file {watched} never appeared. Check that the producing step really writes to this path - "
                + "a relative path resolves against the LocalIO run directory when the timeline calls UseRunDirectory(), "
                + "and against the process working directory otherwise, so a mismatched working directory is the usual cause.",
                exception);
        }
    }

    /// <inheritdoc />
    public override Task<SequentialPollingResult<EmptyStepResultContext>> OnSequentialPolling(RunContext context)
    {
        string _path = ResolvePath(context.Variables);
        return Task.FromResult(new SequentialPollingResult<EmptyStepResultContext>(File.Exists(_path), null, pollDelay?.GetValue(context.Variables) ?? TimeSpan.FromSeconds(0.5)));
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
        // Required, because that is the truth: a named-but-missing variable throws at run time, so
        // declaring it optional traded a plan-time refusal with the fix named for a mid-poll crash.
        if (pollDelay is not null && pollDelay.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(pollDelay.Identifier!.Identifier, StepIOKind.Variable, true, typeof(System.TimeSpan)));
    }
}