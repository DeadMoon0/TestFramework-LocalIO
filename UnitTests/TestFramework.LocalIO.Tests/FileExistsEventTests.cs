using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;
using LocalIOFacade = TestFramework.LocalIO.LocalIO;

namespace TestFramework.LocalIO.Tests;

public class FileExistsEventTests
{
    [Fact]
    public async Task OnSequentialPolling_ReturnsTrueAndConfiguredDelayWhenFileExists()
    {
        string path = Path.Combine(Path.GetTempPath(), $"exists-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(path, "ready");
            Timeline timeline = Timeline.Create()
                .WaitForEvent(LocalIOFacade.Events.FileExists(Var.Ref<string>("path"), Var.Ref<TimeSpan>("delay")))
                .Name("file-exists")
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("path", path)
                .AddVariable("delay", TimeSpan.FromMilliseconds(125))
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.Equal(StepState.Complete, run.Step("file-exists").State);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task OnSequentialPolling_ThrowsWhenResolvedPathIsNull()
    {
        Timeline timeline = Timeline.Create()
            .WaitForEvent(LocalIOFacade.Events.FileExists(Var.Ref<string>("path")))
            .Name("file-exists")
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddVariable<string?>("path", null)
            .RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
        Assert.Contains(exception.FailedSteps, step => step.StepException is ArgumentNullException);
    }

    [Fact]
    public void DeclareIO_AddsPathAndPollDelayMetadata()
    {
        FileExistsEvent fileExists = LocalIOFacade.Events.FileExists(Var.Ref<string>("path"), Var.Ref<TimeSpan>("delay"));
        StepIOContract contract = new();

        fileExists.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("path", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
                Assert.Equal(typeof(string), entry.DeclaredType);
            },
            entry =>
            {
                Assert.Equal("delay", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.False(entry.Required);
                Assert.Equal(typeof(TimeSpan), entry.DeclaredType);
            });
    }

    [Fact]
    public void LocalIOCmdFactory_UsesCurrentDirectoryAsDefaultWorkingDirectoryDeclaration()
    {
        CmdTrigger trigger = LocalIOFacade.Trigger.Cmd(Var.Ref<string>("cmd"));
        StepIOContract contract = new();

        trigger.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("cmd", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
                Assert.Equal(typeof(string), entry.DeclaredType);
            });
    }

}