using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// Covers the Core artifact type-mismatch exception through file artifacts.
/// </summary>
/// <remarks>
/// These live here rather than in the Core test suite because they need a concrete artifact kind to
/// be meaningful, and Core must not depend on one of its own extension packages.
/// </remarks>
public class ArtifactExceptionTests
{
    [Fact]
    public void ArtifactTypeMismatchException_ExplainsRequestedAndActualTypes()
    {
        var ex = new ArtifactTypeMismatchException("payload", typeof(FileArtifactData), typeof(ArtifactDataGeneric));

        Assert.Contains("payload", ex.Message);
        Assert.Contains(nameof(FileArtifactData), ex.ToString());
        Assert.Contains(nameof(ArtifactDataGeneric), ex.ToString());
    }

    [Fact]
    public async Task TimelineRun_TypedArtifactSelection_UsesFriendlyTypeMismatchException()
    {
        TimelineRun run = await Timeline.Create()
            .SetupArtifact("file")
            .Build()
            .SetupRun()
            .AddFileArtifact("file", Path.Combine(Path.GetTempPath(), $"typed-artifact-{Guid.NewGuid():N}.txt"), "payload")
            .RunAsync();

        ArtifactTypeMismatchException ex = Assert.Throws<ArtifactTypeMismatchException>(() => run.Artifact<MismatchArtifactData>("file").Select(_ => "ignored"));

        Assert.Contains("file", ex.Message);
        Assert.Contains(nameof(FileArtifactData), ex.ToString());
    }

    private sealed class MismatchArtifactReference : ArtifactReference<MismatchArtifactReference, MismatchArtifactDescriber, MismatchArtifactData>
    {
        public override Task<ArtifactResolveResult<MismatchArtifactDescriber, MismatchArtifactData, MismatchArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => throw new NotSupportedException();

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
        {
        }

        public override string ToString() => nameof(MismatchArtifactReference);
    }

    private sealed class MismatchArtifactData : ArtifactData<MismatchArtifactData, MismatchArtifactDescriber, MismatchArtifactReference>
    {
        public override string ToString() => nameof(MismatchArtifactData);
    }

    private sealed class MismatchArtifactDescriber : ArtifactDescriber<MismatchArtifactDescriber, MismatchArtifactData, MismatchArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, MismatchArtifactData data, MismatchArtifactReference reference, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, MismatchArtifactReference reference, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => nameof(MismatchArtifactDescriber);
    }
}
