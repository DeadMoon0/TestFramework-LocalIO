using System.Runtime.Versioning;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class LocalIOAdvancedTests
{
    [Fact]
    [Trait("Category", "WindowsOnly")]
    [SupportedOSPlatform("windows")]
    public async Task CmdTrigger_Execute_UsesConfiguredWorkingDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "marker.txt"), "ready");
            Timeline timeline = Timeline.Create()
                .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmd"), Var.Ref<string>("cwd")))
                .Name("cmd")
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("cmd", "if exist marker.txt (exit /b 0) else (exit /b 1)")
                .AddVariable("cwd", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            CmdResultContext result = Assert.IsType<CmdResultContext>(run.Step("cmd").LastResult.Result);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(tempDir, result.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "WindowsOnly")]
    [SupportedOSPlatform("windows")]
    public async Task CmdTrigger_Execute_ReturnsProcessExitCodeForFailingCommand()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("exit /b 7")))
            .Name("cmd")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(7, Assert.IsType<CmdResultContext>(run.Step("cmd").LastResult.Result).ExitCode);
    }

    [Fact]
    [Trait("Category", "WindowsOnly")]
    [SupportedOSPlatform("windows")]
    public async Task CmdTrigger_BindsUsefulOutputs_IntoTimelineVariables()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string tempDir = CreateTempDirectory();

        try
        {
            Timeline timeline = Timeline.Create()
                .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello&&echo warning 1>&2&&exit /b 3"), Var.Const(tempDir)))
                .Name("cmd")
                .GetCommandResult("cmdResult")
                .GetExitCode("cmdExitCode")
                .GetStandardOutput("cmdStdOut")
                .GetStandardError("cmdStdErr")
                .GetCommand("cmdText")
                .GetWorkingDirectory("cmdWorkingDirectory")
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            run.EnsureRanToCompletion();

            Assert.True(run.VariableStore.TryGetVariable<CmdResultContext>("cmdResult", out CmdResultContext? result));
            Assert.NotNull(result);
            Assert.Equal(3, result!.ExitCode);
            Assert.Contains("hello", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("warning", result.StandardError, StringComparison.OrdinalIgnoreCase);

            Assert.True(run.VariableStore.TryGetVariable<int>("cmdExitCode", out int exitCode));
            Assert.Equal(3, exitCode);
            Assert.True(run.VariableStore.TryGetVariable<string>("cmdStdOut", out string? stdOut));
            Assert.Contains("hello", stdOut, StringComparison.OrdinalIgnoreCase);
            Assert.True(run.VariableStore.TryGetVariable<string>("cmdStdErr", out string? stdErr));
            Assert.Contains("warning", stdErr, StringComparison.OrdinalIgnoreCase);
            Assert.True(run.VariableStore.TryGetVariable<string>("cmdText", out string? command));
            Assert.Equal("echo hello&&echo warning 1>&2&&exit /b 3", command);
            Assert.True(run.VariableStore.TryGetVariable<string>("cmdWorkingDirectory", out string? workingDirectory));
            Assert.Equal(tempDir, workingDirectory);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    [Trait("Category", "WindowsOnly")]
    [SupportedOSPlatform("windows")]
    public async Task CmdTrigger_WithTimelineTimeout_FailsWithCancellationError()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Timeline timeline = Timeline.Create()
            .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("ping 127.0.0.1 -n 6 > nul")))
            .Name("cmd")
            .WithTimeOut(TimeSpan.FromMilliseconds(100))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

        Assert.Contains(exception.FailedSteps, step => step.StepException is TimeoutException);
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
    public async Task FileArtifactFolderFinder_FindAsync_ReturnsNullWhenFolderIsEmpty()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            Timeline timeline = Timeline.Create()
                .FindArtifact("file", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.Empty(run.ArtifactStore.GetAll());
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
                .FindArtifactsAs(["file0", "file1"], new FileArtifactFolderFinder(Var.Ref<string>("folder")))
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
    public async Task FileArtifactFolderFinder_FindArtifacts_GeneratesBaseNameAndIndexedSuffixes()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "b");
            Timeline timeline = Timeline.Create()
                .FindArtifacts("file", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            run.EnsureRanToCompletion();

            string[] identifiers = run.ArtifactStore.GetAll()
                .Select(x => x.Identifier.Identifier)
                .ToArray();

            Assert.Contains("file_0", identifiers);
            Assert.Contains("file_1", identifiers);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FileArtifactFolderFinder_FindArtifactsAs_FailsWhenFinderCountDoesNotMatchNames()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(tempDir, "b.txt"), "b");
            Timeline timeline = Timeline.Create()
                .FindArtifactsAs(["file0"], new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", tempDir)
                .RunAsync();

            TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

            Assert.Contains(exception.FailedSteps, step =>
                step.StepException is ArtifactCountMismatchException artifactCountMismatchException &&
                artifactCountMismatchException.Message.Contains("expected 1 artifact name", StringComparison.OrdinalIgnoreCase) &&
                artifactCountMismatchException.Message.Contains("finder produced 2 result", StringComparison.OrdinalIgnoreCase));
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
    public void AddFileArtifact_WithBinaryData_PreservesBytesAndUsesProvidedPath()
    {
        FakeTimelineRunBuilder builder = new();

        builder.AddFileArtifact("file", "sample.bin", [1, 2, 3]);

        Assert.NotNull(builder.ArtifactIdentifier);
        Assert.Equal("file", builder.ArtifactIdentifier!.Identifier);
        Assert.NotNull(builder.Reference);
        Assert.Equal(new byte[] { 1, 2, 3 }, builder.Data!.Data);
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

        public ITimelineRunBuilder SetEnv(IEnvironmentProvider environment) => this;
    }
}