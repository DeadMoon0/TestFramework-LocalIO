using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
                .RegisterArtifact("legacy", LocalIOExt.Artifacts.FileRef(Var.Const("legacy.txt")))
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
    public async Task MarkReadonly_KeepsCleanupFromDeletingAFileTheRunDidNotCreate()
    {
        string tempDir = CreateTempDirectory();
        string preExisting = Path.Combine(tempDir, "pre-existing.txt");

        try
        {
            File.WriteAllText(preExisting, "keep me");

            Timeline timeline = Timeline.Create()
                .RegisterArtifact("kept", LocalIOExt.Artifacts.FileRef(Var.Const(preExisting)))
                .MarkReadonly()
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();
            Assert.True(File.Exists(preExisting), "A readonly artifact was deleted during cleanup.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_DeletesWhatItDiscovered_BecauseThatIsTheDefault()
    {
        // The finder no longer decides ownership on the author's behalf. Discovery gets the same
        // default as everything else - deleted at teardown - and MarkReadonly() is the way out.
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
            Assert.False(File.Exists(discovered), "Teardown deletes a discovered file unless the timeline marks it readonly.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_WithMarkReadonly_LeavesTheFilesOnDisk()
    {
        string tempDir = CreateTempDirectory();
        string discovered = Path.Combine(tempDir, "discovered.txt");

        try
        {
            File.WriteAllText(discovered, "discovered");

            Timeline timeline = Timeline.Create()
                .FindArtifact("file", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .MarkReadonly()
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.True(File.Exists(discovered), "MarkReadonly() is the author's decision and nothing may overrule it.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_WithMarkReadonly_ProtectsEveryDiscoveredFile()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "b");

            Timeline timeline = Timeline.Create()
                .FindArtifacts("files", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .MarkReadonly()
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.Equal(2, Directory.EnumerateFiles(tempDir).Count());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_WithAMissingFolder_WarnsInsteadOfThrowing()
    {
        string missingFolder = Path.Combine(Path.GetTempPath(), $"localio-missing-{Guid.NewGuid():N}");

        Timeline timeline = Timeline.Create()
            .FindArtifact("single", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
            .FindArtifacts("many", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddVariable("folder", missingFolder)
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Empty(run.ArtifactStore.GetAll());
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-rundir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
