using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Creates a unique directory for the current run, publishes it as the LocalIO run directory and
/// removes it again during cleanup.
/// </summary>
/// <remarks>
/// Every relative path handed to a LocalIO artifact, event or command resolves against this
/// directory, so concurrent runs cannot read, overwrite or delete each other's files.
/// </remarks>
/// <param name="root">The directory the run directory is created in. Defaults to the system temp directory.</param>
public class RunDirectoryStep(VariableReference<string>? root = null) : Step<EmptyStepResultContext>, IHasCleanupStep
{
    /// <inheritdoc />
    public override string Name => "Run Directory";

    /// <inheritdoc />
    public override string Description => "Creates the per-run directory that LocalIO resolves relative paths against.";

    /// <inheritdoc />
    public override bool DoesReturn => false;

    /// <inheritdoc />
    public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

    /// <inheritdoc />
    public override Step<EmptyStepResultContext> Clone() => new RunDirectoryStep(root).WithClonedOptions(this);

    /// <inheritdoc />
    public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string rootPath = root?.GetValue(variableStore) ?? Path.GetTempPath();
        string runDirectory = Path.Combine(rootPath, $"tf-localio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runDirectory);
        variableStore.SetVariable(LocalPath.RunDirectoryVariable, runDirectory);
        logger.LogInformation($"LocalIO run directory: {runDirectory}");
        return Task.FromResult<EmptyStepResultContext?>(null);
    }

    /// <inheritdoc />
    public StepGeneric CreateCleanupStep(VariableStore variableStore) => new RunDirectoryCleanupStep();

    /// <inheritdoc />
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        if (root is not null && root.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(root.Identifier!.Identifier, StepIOKind.Variable, false, typeof(string)));
        contract.Outputs.Add(new StepIOEntry(LocalPath.RunDirectoryVariable, StepIOKind.Variable, true, typeof(string)));
    }
}

/// <summary>
/// Removes the directory created by <see cref="RunDirectoryStep"/>.
/// </summary>
public class RunDirectoryCleanupStep : Step<EmptyStepResultContext>
{
    /// <inheritdoc />
    public override string Name => "Run Directory Cleanup";

    /// <inheritdoc />
    public override string Description => "Removes the per-run LocalIO directory and everything left in it.";

    /// <inheritdoc />
    public override bool DoesReturn => false;

    /// <inheritdoc />
    public override Step<EmptyStepResultContext> Clone() => new RunDirectoryCleanupStep().WithClonedOptions(this);

    /// <inheritdoc />
    public override Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string? runDirectory = LocalPath.TryGetRunDirectory(variableStore);
        if (runDirectory is null)
        {
            logger.LogWarning("No LocalIO run directory was published, so there is nothing to clean up.");
            return Task.FromResult<EmptyStepResultContext?>(null);
        }

        if (!Directory.Exists(runDirectory))
        {
            return Task.FromResult<EmptyStepResultContext?>(null);
        }

        try
        {
            Directory.Delete(runDirectory, true);
            logger.LogInformation($"Removed the LocalIO run directory: {runDirectory}");
        }
        catch (IOException exception)
        {
            logger.LogWarning($"Could not remove the LocalIO run directory \"{runDirectory}\": {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning($"Could not remove the LocalIO run directory \"{runDirectory}\": {exception.Message}");
        }

        return Task.FromResult<EmptyStepResultContext?>(null);
    }

    /// <inheritdoc />
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext>(this);

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
    }
}
