using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

// README sync note: these tests mirror the public README samples for TestFramework.LocalIO.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    [WindowsFact]
    [Trait("Category", "WindowsOnly")]
    public async Task QuickStart_CmdWaitAndRegisterArtifact_CompletesAndReadsFile()
    {
        string tempDir = CreateTempDirectory();
        const string outputFileName = "out.txt";
        string outputPath = Path.Combine(tempDir, outputFileName);

        try
        {
            Timeline timeline = Timeline.Create()
                .Trigger(LocalIOExt.Trigger.Cmd(Var.Const($"echo hello > {outputFileName}"), Var.Const(tempDir)))
                .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(outputPath))).WithTimeOut(TimeSpan.FromSeconds(10))
                .RegisterArtifact("outFile", LocalIOExt.Artifacts.FileRef(Var.Const(outputPath)))
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();
            string content = run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String;
            Assert.Contains("hello", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WaitUntilFileExists_CompletesWhenTargetFileAlreadyExists()
    {
        string tempDir = CreateTempDirectory();
        string outputPath = Path.Combine(tempDir, "out.txt");

        try
        {
            File.WriteAllText(outputPath, "ready");

            Timeline timeline = Timeline.Create()
                .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(outputPath)))
                .WithTimeOut(TimeSpan.FromSeconds(10))
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AddOrReadFileArtifacts_AddsAndReadsUtf8Content()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun()
            .AddFileArtifact("inputFile", "input.txt", "hello world")
            .RunAsync();

        run.EnsureRanToCompletion();
        string content = run.ArtifactStore.GetFileArtifact("inputFile").Last.DataAsUtf8String;
        Assert.Equal("hello world", content);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-readme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}