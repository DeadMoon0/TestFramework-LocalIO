using TestFramework.Core;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;
using TestFrameworkLocalIO;
using TestFrameworkLocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class LocalIOAdvancedTests
{
    [Fact]
    public async Task CmdTrigger_Execute_UsesConfiguredWorkingDirectory()
    {
        RuntimeContext runtime = new();
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "marker.txt"), "ready");
            runtime.VariableStore.SetVariable("cmd", "if exist marker.txt (exit /b 0) else (exit /b 1)");
            runtime.VariableStore.SetVariable("cwd", tempDir);
            CmdTrigger trigger = new(Var.Ref<string>("cmd"), Var.Ref<string>("cwd"));

            int exitCode = await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_FindAsync_ReturnsAFileReferenceWhenFolderContainsFiles()
    {
        RuntimeContext runtime = new();
        string tempDir = CreateTempDirectory();
        string filePath = Path.Combine(tempDir, "a.txt");

        try
        {
            File.WriteAllText(filePath, "hello");
            runtime.VariableStore.SetVariable("folder", tempDir);
            FileArtifactFolderFinder finder = new(Var.Ref<string>("folder"));

            ArtifactFinderResult? result = await finder.FindAsync(runtime.ServiceProvider, runtime.VariableStore, runtime.Logger, CancellationToken.None);

            Assert.NotNull(result);
            ArtifactResolveResult<FileArtifactDescriber, FileArtifactData, FileArtifactReference> resolved =
                await ((FileArtifactReference)result!.Reference).ResolveToDataAsync(runtime.ServiceProvider, ArtifactVersionIdentifier.Default, runtime.VariableStore, runtime.Logger);
            Assert.True(resolved.Found);
            Assert.Equal("hello", resolved.Data!.DataAsUtf8String);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_FindMultiAsync_ReturnsAllFilesInFolder()
    {
        RuntimeContext runtime = new();
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "b");
            runtime.VariableStore.SetVariable("folder", tempDir);
            FileArtifactFolderFinder finder = new(Var.Ref<string>("folder"));

            ArtifactFinderResultMulti result = await finder.FindMultiAsync(runtime.ServiceProvider, runtime.VariableStore, runtime.Logger, CancellationToken.None);

            Assert.Equal(2, result.ArtifactReferences.Length);
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
    public void GetFileArtifact_ReturnsTypedArtifactInstance()
    {
        RuntimeContext runtime = new();
        ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> instance =
            new(new FileArtifactDescriber(), "file", new FileArtifactReference(Var.Const("sample.txt")), new FileArtifactData([1, 2, 3]));

        runtime.ArtifactStore.AddArtifact(instance);

        ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> resolved = runtime.ArtifactStore.GetFileArtifact("file");

        Assert.Same(instance, resolved);
        Assert.Equal(new byte[] { 1, 2, 3 }, resolved.Last.Data);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RuntimeContext
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(EmptyRunDebugger.CreateNew());
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        public RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
            ArtifactStore = new ArtifactStore(Logger, DebuggingSession);
        }
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