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
/// Pins the refusals and guards from the source-level audit. Each test fails against the behaviour
/// the audit found: the silent drive-root escape, the trusting cleanup delete, the silent second
/// run directory and the first-file coin toss.
/// </summary>
public class AuditHardeningTests
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
            .WithTimeOut(TimeSpan.FromSeconds(5))
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
    public async Task ASecondRunDirectoryIsRefusedByName()
    {
        string root = CreateTempDirectory();

        try
        {
            Timeline timeline = Timeline.Create()
                .UseRunDirectory(Var.Ref<string>("root"))
                .UseRunDirectory(Var.Ref<string>("root"))
                .Build();

            TimelineRun run = await timeline.SetupRun()
                .AddVariable("root", root)
                .RunAsync();

            TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
            Assert.Contains(exception.FailedSteps, step =>
                ExceptionChainContains(step.StepException, "already declared"));
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
        string path = Path.Combine(Path.GetTempPath(), $"localio-audit-{Guid.NewGuid():N}");
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
