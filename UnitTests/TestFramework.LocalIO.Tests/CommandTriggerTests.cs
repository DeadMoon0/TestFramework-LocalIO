using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// What a command step promises: a value passed as an argument stays one value, a command that runs out of time
/// says what it had written, and a command that cannot start says where it tried.
/// </summary>
public class CommandTriggerTests
{
    [UnixFact]
    [Trait("Category", "UnixOnly")]
    public async Task Exec_PassesAValueWithShellSyntaxAsOneArgument()
    {
        // Through a shell this value would be three commands and a redirection; passed directly it is text.
        const string value = "a b && echo injected > out.txt";

        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Exec(Var.Const("printf"), Var.Const("%s"), Var.Const(value))).Label("exec")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        CmdResultContext result = Assert.IsType<CmdResultContext>(run.Step("exec").LastResult.Result);
        Assert.Equal(value, result.StandardOutput);
    }

    [WindowsFact]
    [Trait("Category", "WindowsOnly")]
    public async Task Exec_PassesAValueWithShellSyntaxAsOneArgument_OnWindows()
    {
        // findstr's /C: takes the rest of its argument as one literal search string, so it only finds the line
        // when the value arrives whole - split by a shell, the && would run a second command instead.
        const string value = "a b && echo injected > out.txt";
        string file = Path.Combine(Path.GetTempPath(), $"tf-exec-{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(file, ["unrelated", value]);

        try
        {
            Timeline timeline = Timeline.Create()
                .Trigger(LocalIOExt.Trigger.Exec(Var.Const("findstr"), Var.Const("/L"), Var.Const("/C:" + value), Var.Const(file))).Label("exec")
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();
            CmdResultContext result = Assert.IsType<CmdResultContext>(run.Step("exec").LastResult.Result);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(value, result.StandardOutput.TrimEnd());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Exec_StartsAProgramByName()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Exec(Var.Const("hostname"))).Label("exec")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        CmdResultContext result = Assert.IsType<CmdResultContext>(run.Step("exec").LastResult.Result);
        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    [Fact]
    public async Task AProgramThatCannotBeStarted_IsNamedWithTheDirectoryItWasStartedIn()
    {
        string program = $"tf-no-such-program-{Guid.NewGuid():N}";
        string workingDirectory = Path.GetTempPath();

        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.ExecIn(Var.Const(workingDirectory), Var.Const(program))).Label("exec")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        Exception failure = Assert.IsType<FrameworkConfigurationException>(run.Step("exec").LastResult.Exception);
        Assert.Contains(program, failure.Message, StringComparison.Ordinal);
        Assert.Contains(workingDirectory, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingWorkingDirectory_IsRefusedByName()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"tf-missing-{Guid.NewGuid():N}");

        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello"), Var.Const(missing))).Label("cmd")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        Exception failure = Assert.IsType<FrameworkConfigurationException>(run.Step("cmd").LastResult.Exception);
        Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandThatRunsOutOfTime_SaysWhatItHadWritten()
    {
        string command = OperatingSystem.IsWindows()
            ? "echo started-the-work&& ping -n 30 127.0.0.1 >nul"
            : "echo started-the-work; sleep 30";

        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const(command)))
                .WithTimeout(TimeSpan.FromSeconds(3)).Label("cmd")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        StepResultGeneric result = run.Step("cmd").LastResult;
        Assert.Equal(StepState.Timeout, result.State);
        Assert.Contains("started-the-work", result.Exception?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACapturedStream_KeepsItsEnd_AndSaysHowMuchWasDropped()
    {
        string written = new string('x', 100) + "the-end";
        using StreamReader reader = new(new MemoryStream(Encoding.UTF8.GetBytes(written)));

        PipeCapture capture = PipeCapture.Start(reader, limit: 10, CancellationToken.None);
        await capture.Completion;

        Assert.EndsWith("xxxthe-end", capture.Text, StringComparison.Ordinal);
        Assert.StartsWith("[97 earlier characters were dropped; the last 10 are kept.]", capture.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACapturedStreamWithinTheLimit_IsKeptWhole()
    {
        using StreamReader reader = new(new MemoryStream(Encoding.UTF8.GetBytes("short")));

        PipeCapture capture = PipeCapture.Start(reader, limit: 10, CancellationToken.None);
        await capture.Completion;

        Assert.Equal("short", capture.Text);
    }
}
