using System;
using System.IO;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// Pins the places LocalIO refuses by name instead of guessing: a path that would silently escape the run
/// directory onto the current drive, a cleanup delete that would trust a rewritten variable, a second run
/// directory, and a single find that would pick whichever of two files came first.
/// </summary>
public class RefusalTests
{
    [WindowsFact]
    [Trait("Category", "WindowsOnly")]
    public async Task ARootedButNotQualifiedPathIsRefusedByName()
    {
        // '\results\out.txt' is rooted but not fully qualified: Path.Combine returns it unchanged,
        // so before the refusal it silently escaped the run directory onto the current drive.
        Timeline timeline = Timeline.Create()
            .UseRunDirectory()
            .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(@"\results\out.txt")))
            .WithTimeout(TimeSpan.FromSeconds(5))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
        Assert.Contains(exception.FailedSteps, step =>
            ExceptionChainContains(step.StepException, "rooted but not fully qualified"));
    }

    [Fact]
    public async Task TheCleanupDeleteNeverTrustsARewrittenRunDirectoryVariable()
    {
        string root = CreateTempDirectory();
        string victim = Path.Combine(root, "victim-data");
        Directory.CreateDirectory(victim);
        File.WriteAllText(Path.Combine(victim, "precious.txt"), "keep me");

        try
        {
            // The variable is a coordinate anything can overwrite; the recursive delete must not
            // follow it to a directory the framework did not create.
            Timeline timeline = Timeline.Create()
                .UseRunDirectory(Var.Ref<string>("root"))
                .Trigger(new RetargetRunDirectoryStep(victim))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("root", root)
                .RunAsync();

            run.EnsureRanToCompletion();
            Assert.True(File.Exists(Path.Combine(victim, "precious.txt")), "The cleanup delete followed a rewritten variable into a directory the run did not create.");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ASecondRunDirectoryIsRefusedWhileTheRunIsPlanned()
    {
        // Before anything exists: the refusal used to come from the second step at run time, after the first
        // directory had been created.
        string root = CreateTempDirectory();

        try
        {
            Timeline timeline = Timeline.Create()
                .UseRunDirectory(Var.Ref<string>("root"))
                .UseRunDirectory(Var.Ref<string>("root"))
                .Build();

            FrameworkConfigurationException refusal = await Assert.ThrowsAsync<FrameworkConfigurationException>(
                () => timeline.SetupRun().AddVariable("root", root).RunAsync());

            Assert.Contains(LocalPath.RunDirectoryVariable, refusal.Message, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AFolderWithTwoFilesRefusesTheSingleFindInsteadOfGuessing()
    {
        string folder = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(folder, "a.txt"), "a");
            File.WriteAllText(Path.Combine(folder, "b.txt"), "b");

            Timeline timeline = Timeline.Create()
                .FindArtifact("single", new FileArtifactFolderFinder(Var.Ref<string>("folder")))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("folder", folder)
                .RunAsync();

            TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
            Assert.Contains(exception.FailedSteps, step =>
                ExceptionChainContains(step.StepException, "a.txt")
                && ExceptionChainContains(step.StepException, "b.txt")
                && ExceptionChainContains(step.StepException, "ambiguous"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private static bool ExceptionChainContains(Exception? exception, string text)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"localio-refusal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RetargetRunDirectoryStep(string target) : Step<EmptyStepResultContext>
    {
        public override string Name => "Retarget Run Directory";

        public override string Description => "Overwrites the run directory variable, standing in for anything that can.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new RetargetRunDirectoryStep(target).WithClonedOptions(this);

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetVariable("localIoRunDirectory", target);
            return Task.FromResult<EmptyStepResultContext?>(null);
        }

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }
    }
}
