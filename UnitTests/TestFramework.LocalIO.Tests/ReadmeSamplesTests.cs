using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

// README sync note: these tests mirror the public README samples for TestFramework.LocalIO.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    // Mirrors the "Quick Start" sample in TestFramework.LocalIO/README.md.
    [Fact]
    public async Task QuickStart_CmdWaitAndRegisterArtifact_CompletesAndReadsFile()
    {
        Timeline timeline = Timeline.Create()
            .UseRunDirectory()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello > out.txt")))
            .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const("out.txt"))).WithTimeOut(TimeSpan.FromSeconds(10))
            .RegisterArtifact("outFile", LocalIOExt.Artifacts.FileRef(Var.Const("out.txt")))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        string content = run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String;
        Assert.Contains("hello", content, StringComparison.OrdinalIgnoreCase);
    }

    // Mirrors the "Quickstart" sample in the root README.md.
    [Fact]
    public async Task RootReadmeQuickstart_CanExecuteCommandAndWaitForFile()
    {
        Timeline timeline = Timeline.Create()
            .UseRunDirectory()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello > out.txt")))
            .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const("out.txt")))
            .WithTimeOut(TimeSpan.FromSeconds(10))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
    }

    // Mirrors the "Command Output Assertions" sample in TestFramework.LocalIO/README.md.
    [WindowsFact]
    [Trait("Category", "WindowsOnly")]
    public async Task CommandOutputAssertions_BindStdOutAndStdErr()
    {
        Timeline timeline = Timeline.Create()
            .UseRunDirectory()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
            .GetStandardOutput("commandStdOut")
            .GetStandardError("commandStdErr")
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddVariable("cmdCommand", "echo hello&&echo warning 1>&2")
            .RunAsync();

        run.EnsureRanToCompletion();

        string? stdOut = run.VariableStore.GetVariable<string>("commandStdOut");
        string? stdErr = run.VariableStore.GetVariable<string>("commandStdErr");
        Assert.Contains("hello", stdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("warning", stdErr, StringComparison.OrdinalIgnoreCase);
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
        string tempDir = CreateTempDirectory();
        string inputPath = Path.Combine(tempDir, "input.txt");

        try
        {
            Timeline timeline = Timeline.Create().Build();

            TimelineRun run = await timeline.SetupRun()
                .AddFileArtifact("inputFile", inputPath, "hello world")
                .RunAsync();

            run.EnsureRanToCompletion();
            string content = run.ArtifactStore.GetFileArtifact("inputFile").Last.DataAsUtf8String;
            Assert.Equal("hello world", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DeterministicMultiFileAssertions_OrdersDiscoveredArtifactsByPath()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "b.csv"), "b");
            File.WriteAllText(Path.Combine(tempDir, "a.csv"), "a");
            File.WriteAllText(Path.Combine(tempDir, "c.csv"), "c");

            Timeline timeline = Timeline.Create()
                .FindArtifacts("exports", new FileArtifactFolderFinder(Var.Const(tempDir)))
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();

            IReadOnlyList<string> orderedPaths = run.ArtifactStore
                .GetFileArtifacts("exports")
                .Select(x => x.Reference.FilePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(
                [Path.Combine(tempDir, "a.csv"), Path.Combine(tempDir, "b.csv"), Path.Combine(tempDir, "c.csv")],
                orderedPaths);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FilePath_BeforeTheRunResolvesTheReference_ExplainsItself()
    {
        FileArtifactReference reference = LocalIOExt.Artifacts.FileRef(Var.Const("out.txt"));

        FrameworkStateException exception = Assert.Throws<FrameworkStateException>(() => reference.FilePath);
        Assert.Contains("not resolved yet", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-readme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}