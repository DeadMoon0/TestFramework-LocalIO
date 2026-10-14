using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// A command that exits while something it started still holds its output finishes with what it wrote, and what it
/// started does not outlive the step.
/// </summary>
/// <remarks>
/// <c>cmd /c start /b worker</c> exits at once while the worker keeps the inherited pipes open. The step used to wait
/// for the pipes to close, sat until its deadline, reported a timeout for a command that had succeeded - and then
/// could not stop the worker, because the shell it would have walked the tree from was already gone. Each case leaves
/// a child that would write a sentinel a few seconds later; the sentinel never appearing is the proof it was stopped.
/// </remarks>
public sealed class CommandLeftRunningTests : IDisposable
{
    private static readonly TimeSpan SentinelDelay = TimeSpan.FromSeconds(8);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"localio-left-running-{Guid.NewGuid():N}");

    public CommandLeftRunningTests() => Directory.CreateDirectory(_directory);

    private string Sentinel => Path.Combine(_directory, "sentinel.txt");

    [WindowsFact]
    public Task OnWindows_ACommandThatLeavesAChildHoldingItsOutput_FinishesWithWhatItWrote()
        => AssertFinishesAndStopsTheChild("echo started&& start /b cmd /c \"ping 127.0.0.1 -n 6 > nul && echo leaked > sentinel.txt\"");

    [UnixFact]
    public Task OnUnix_ACommandThatLeavesAChildHoldingItsOutput_FinishesWithWhatItWrote()
        => AssertFinishesAndStopsTheChild("echo started; (sleep 5; echo leaked > sentinel.txt) &");

    [WindowsFact]
    public Task OnWindows_ADeadlineAfterTheShellExited_StillStopsWhatItStarted()
        => AssertDeadlineStopsTheChild("start /b cmd /c \"ping 127.0.0.1 -n 6 > nul && echo leaked > sentinel.txt\"");

    [UnixFact]
    public Task OnUnix_ADeadlineAfterTheShellExited_StillStopsWhatItStarted()
        => AssertDeadlineStopsTheChild("(sleep 5; echo leaked > sentinel.txt) &");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task AssertFinishesAndStopsTheChild(string command)
    {
        Stopwatch elapsed = Stopwatch.StartNew();

        TimelineRun run = await Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const(command), Var.Const(_directory))).Name("cmd")
            .WithTimeOut(TimeSpan.FromSeconds(60))
            .Build()
            .SetupRun()
            .RunAsync();

        run.EnsureRanToCompletion();
        CmdResultContext result = Assert.IsType<CmdResultContext>(run.Step("cmd").LastResult.Result);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("started", result.StandardOutput, StringComparison.Ordinal);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(20), $"The step waited {elapsed.Elapsed} for a command that had exited.");

        await AssertChildWasStopped();
    }

    private async Task AssertDeadlineStopsTheChild(string command)
    {
        // Shorter than the pipe grace, so the deadline ends the step while the shell is already gone.
        TimelineRun run = await Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const(command), Var.Const(_directory))).Name("cmd")
            .WithTimeOut(TimeSpan.FromMilliseconds(800))
            .Build()
            .SetupRun()
            .RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);
        Assert.Contains(failure.FailedSteps, step => step.StepException is TimeoutException);

        await AssertChildWasStopped();
    }

    private async Task AssertChildWasStopped()
    {
        await Task.Delay(SentinelDelay);
        Assert.False(File.Exists(Sentinel), "What the command started outlived the step and kept writing files.");
    }
}
