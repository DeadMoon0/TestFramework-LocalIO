using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class LocalIOAdvancedTests
{
    [Fact]
    public async Task CmdTrigger_Execute_UsesConfiguredWorkingDirectory()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "marker.txt"), "ready");
            Timeline timeline = Timeline.Create()
                .Trigger(LocalIO.Trigger.Cmd(Var.Ref<string>("cmd"), Var.Ref<string>("cwd")))
                .Name("cmd")
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("cmd", "if exist marker.txt (exit /b 0) else (exit /b 1)")
                .AddVariable("cwd", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.Equal(0, Assert.IsType<int>(run.Step("cmd").LastResult.Result));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_FindAsync_ReturnsAFileReferenceWhenFolderContainsFiles()
    {
        string tempDir = CreateTempDirectory();
        string filePath = Path.Combine(tempDir, "a.txt");

        try
        {
            File.WriteAllText(filePath, "hello");
            Timeline timeline = Timeline.Create()
                .FindArtifact("file", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> artifact = Assert.Single(
                run.ArtifactStore.GetAll().Cast<ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference>>());
            Assert.Equal("hello", artifact.Last.DataAsUtf8String);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_FindMultiAsync_ReturnsAllFilesInFolder()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "b");
            Timeline timeline = Timeline.Create()
                .FindArtifactMulti(["file0", "file1"], new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            FileArtifactData[] data = run.ArtifactStore.GetAll()
                .Cast<ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference>>()
                .Select(x => x.Last)
                .ToArray();

            Assert.Equal(2, data.Length);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddFileArtifact_WithUtf8Text_EncodesDataAndUsesProvidedPath()
    {
        FakeTimelineRunBuilder builder = new();

        builder.AddFileArtifact("file", "sample.txt", "hello");

        Assert.NotNull(builder.ArtifactIdentifier);
        Assert.Equal("file", builder.ArtifactIdentifier!.Identifier);
        Assert.NotNull(builder.Reference);
        Assert.Equal("hello", builder.Data!.DataAsUtf8String);
    }

    [Fact]
    public async Task GetFileArtifact_ReturnsTypedArtifactInstance()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("file", new FileArtifactReference(Var.Const("sample.txt")), new FileArtifactData([1, 2, 3]))
            .RunAsync();

        ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> resolved = run.ArtifactStore.GetFileArtifact("file");

        Assert.Equal(new byte[] { 1, 2, 3 }, resolved.Last.Data);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeTimelineRunBuilder : ITimelineRunBuilder
    {
        public ArtifactIdentifier? ArtifactIdentifier { get; private set; }
        public FileArtifactReference? Reference { get; private set; }
        public FileArtifactData? Data { get; private set; }

        public ITimelineRunBuilder AddArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference, ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference> data)
            where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
            where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
            where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        {
            ArtifactIdentifier = identifier;
            Reference = (FileArtifactReference)(object)reference;
            Data = (FileArtifactData)(object)data;
            return this;
        }

        public ITimelineRunBuilder AddVariable<T>(VariableIdentifier identifier, T value) => this;

        public Task<TimelineRun> RunAsync() => Task.FromResult<TimelineRun>(null!);
    }
}