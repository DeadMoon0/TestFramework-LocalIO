using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class FileArtifactReferenceTests
{
    [Fact]
    public async Task Deconstruct_DeletesThePinnedFile_EvenWhenThePathVariableIsReboundMidRun()
    {
        string tempDir = CreateTempDirectory();
        string pinnedPath = Path.Combine(tempDir, "pinned.txt");
        string decoyPath = Path.Combine(tempDir, "decoy.txt");

        try
        {
            File.WriteAllText(pinnedPath, "pinned");
            File.WriteAllText(decoyPath, "decoy");

            Timeline timeline = Timeline.Create()
                .RegisterArtifact("file", LocalIOExt.Artifacts.FileRef(Var.Ref<string>("path")))
                .SetVariable("path", Var.Const(decoyPath))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("path", pinnedPath)
                .RunAsync();

            run.EnsureRanToCompletion();

            Assert.True(File.Exists(decoyPath), "Cleanup followed the rebound variable and deleted the wrong file.");
            Assert.False(File.Exists(pinnedPath), "Cleanup did not delete the file the reference was pinned to.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ToString_ReportsTheResolvedPath_AfterCoreHasPinnedTheReference()
    {
        string tempDir = CreateTempDirectory();
        string filePath = Path.Combine(tempDir, "described.txt");

        try
        {
            File.WriteAllText(filePath, "described");

            FileArtifactReference reference = LocalIOExt.Artifacts.FileRef(Var.Const(filePath));
            Assert.Equal("File: (unresolved)", reference.ToString());

            Timeline timeline = Timeline.Create()
                .RegisterArtifact("file", reference)
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();

            // The run pins its own copy of the reference, not the instance registered on the
            // timeline. That is what keeps two runs of one timeline from sharing pinned state, so
            // the resolved path is read from the run's artifact - the declaration stays unresolved.
            ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> artifact =
                run.ArtifactStore.GetFileArtifact("file");
            Assert.Equal($"File: \"{filePath}\"", artifact.Reference.ToString());
            Assert.Equal("File: (unresolved)", reference.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GetPath_UsedByTheDescriber_MatchesTheFileSetupWrote()
    {
        string tempDir = CreateTempDirectory();
        string filePath = Path.Combine(tempDir, "setup.txt");

        try
        {
            Timeline timeline = Timeline.Create()
                .SetupArtifact("file")
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddFileArtifact("file", filePath, "written by setup")
                .RunAsync();

            run.EnsureRanToCompletion();

            ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> artifact = run.ArtifactStore.GetFileArtifact("file");
            Assert.Equal($"File: \"{filePath}\"", artifact.Reference.ToString());
            Assert.False(File.Exists(filePath), "The setup artifact should have been removed during cleanup.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-ref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
