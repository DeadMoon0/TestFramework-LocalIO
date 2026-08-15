using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class RunDirectoryTests
{
    [Fact]
    public async Task UseRunDirectory_ScopesRelativePathsAndRemovesTheDirectoryAgain()
    {
        // "echo hello > out.txt" is valid for both CMD.EXE /C and bash -c.
        Timeline timeline = Timeline.Create()
            .UseRunDirectory()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello > out.txt")))
            .Name("cmd")
            .GetWorkingDirectory("cwd")
            .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const("out.txt")))
            .WithTimeOut(TimeSpan.FromSeconds(10))
            .RegisterArtifact("outFile", LocalIOExt.Artifacts.FileRef(Var.Const("out.txt")))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains("hello", run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String, StringComparison.OrdinalIgnoreCase);

        Assert.True(run.VariableStore.TryGetVariable<string>("cwd", out string? workingDirectory));
        Assert.Contains("tf-localio-", workingDirectory);
        Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(workingDirectory!.TrimEnd(Path.DirectorySeparatorChar)));

        Assert.True(run.VariableStore.TryGetVariable<string>("localIoRunDirectory", out string? runDirectory));
        Assert.Equal(workingDirectory, runDirectory);
        Assert.False(Directory.Exists(runDirectory), "The run directory survived the cleanup stage.");
    }

    [Fact]
    public async Task UseRunDirectory_WithExplicitRoot_CreatesTheDirectoryUnderThatRoot()
    {
        string root = CreateTempDirectory();

        try
        {
            Timeline timeline = Timeline.Create()
                .UseRunDirectory(Var.Ref<string>("root"))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("root", root)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.True(run.VariableStore.TryGetVariable<string>("localIoRunDirectory", out string? runDirectory));
            Assert.Equal(root, Path.GetDirectoryName(runDirectory));
            Assert.False(Directory.Exists(runDirectory));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RegisterArtifact_WithoutRunDirectory_StillResolvesAgainstTheCurrentDirectory()
    {
        string previous = Environment.CurrentDirectory;
        string tempDir = CreateTempDirectory();

        try
        {
            Environment.CurrentDirectory = tempDir;
            File.WriteAllText(Path.Combine(tempDir, "legacy.txt"), "legacy");

            Timeline timeline = Timeline.Create()
                .RegisterArtifact("legacy", LocalIOExt.Artifacts.FileRef(Var.Const("legacy.txt")).Observed())
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();
            Assert.Equal("legacy", run.ArtifactStore.GetFileArtifact("legacy").Last.DataAsUtf8String);
        }
        finally
        {
            Environment.CurrentDirectory = previous;
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Observed_KeepsCleanupFromDeletingAFileTheRunDidNotCreate()
    {
        string tempDir = CreateTempDirectory();
        string preExisting = Path.Combine(tempDir, "pre-existing.txt");

        try
        {
            File.WriteAllText(preExisting, "keep me");

            Timeline timeline = Timeline.Create()
                .RegisterArtifact("kept", LocalIOExt.Artifacts.FileRef(Var.Const(preExisting)).Observed())
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();
            Assert.True(File.Exists(preExisting), "An observed artifact was deleted during cleanup.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_ProducesObservedReferences_AndLeavesTheFilesOnDisk()
    {
        string tempDir = CreateTempDirectory();
        string discovered = Path.Combine(tempDir, "discovered.txt");

        try
        {
            File.WriteAllText(discovered, "discovered");

            Timeline timeline = Timeline.Create()
                .FindArtifact("file", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.True(File.Exists(discovered), "Discovering a file is not a licence to delete it.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-rundir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
