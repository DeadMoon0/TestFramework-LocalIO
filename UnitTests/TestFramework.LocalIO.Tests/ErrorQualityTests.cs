using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Tests;

public class ErrorQualityTests
{
    [Fact]
    public async Task FileExistsEvent_OnTimeout_NamesTheResolvedPath()
    {
        string tempDir = CreateTempDirectory();
        string missingPath = Path.Combine(tempDir, "never-written.txt");

        try
        {
            // Three seconds, not the couple of hundred milliseconds this used to use. The event has
            // to give up before the step timeout to report the path at all (see the framework
            // limitation in AUDIT-STATUS.md), and its margin is measured from when the event starts
            // polling while the step timeout is measured from when the step starts. On a loaded
            // two-core runner the gap between those two moments was larger than the whole margin,
            // so the generic message won and this test failed for a reason it does not test.
            Timeline timeline = Timeline.Create()
                .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(missingPath), Var.Const(TimeSpan.FromMilliseconds(20))))
                .WithTimeOut(TimeSpan.FromSeconds(5))
                .Name("wait")
                .Build();

            TimelineRun run = await timeline.SetupRun().RunAsync();

            TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
            TimeoutException timeout = Assert.IsType<TimeoutException>(Assert.Single(exception.FailedSteps).StepException);
            Assert.Contains(missingPath, timeout.Message, StringComparison.Ordinal);
            Assert.Contains("working directory", timeout.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SetupArtifact_CreatesTheParentDirectory_InsteadOfThrowingDirectoryNotFound()
    {
        string tempDir = CreateTempDirectory();
        string nestedDirectory = Path.Combine(tempDir, "nested", "deeper");
        string filePath = Path.Combine(nestedDirectory, "created.txt");

        try
        {
            Timeline timeline = Timeline.Create()
                .SetupArtifact("file")
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddFileArtifact("file", filePath, "content", removeParentDirectoryIfEmpty: true)
                .RunAsync();

            run.EnsureRanToCompletion();

            Assert.False(File.Exists(filePath));
            Assert.False(Directory.Exists(nestedDirectory), "Cleanup should remove the directory Setup created.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Cleanup_KeepsAParentDirectoryItDidNotCreate_EvenWithTheOptIn()
    {
        string tempDir = CreateTempDirectory();
        string preExistingDirectory = Path.Combine(tempDir, "pre-existing");
        string filePath = Path.Combine(preExistingDirectory, "created.txt");

        try
        {
            Directory.CreateDirectory(preExistingDirectory);

            Timeline timeline = Timeline.Create()
                .SetupArtifact("file")
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddFileArtifact("file", filePath, "content", removeParentDirectoryIfEmpty: true)
                .RunAsync();

            run.EnsureRanToCompletion();

            Assert.False(File.Exists(filePath));
            Assert.True(Directory.Exists(preExistingDirectory), "Cleanup removed a directory the run did not create.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ResolveToData_WithANullPath_ThrowsAFrameworkStateExceptionWithARealMessage()
    {
        Timeline timeline = Timeline.Create()
            .RegisterArtifact("file", LocalIOExt.Artifacts.FileRef(Var.Ref<string>("path")))
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddVariable<string?>("path", null)
            .RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
        Assert.Contains(exception.FailedSteps, step =>
            step.StepException is FrameworkStateException frameworkStateException
            && frameworkStateException.Message.Contains("path to a file cannot be null", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-errors-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
