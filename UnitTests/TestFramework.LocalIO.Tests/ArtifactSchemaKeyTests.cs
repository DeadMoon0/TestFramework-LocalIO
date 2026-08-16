using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// Pins the artifact's debug schema key to its canonical value.
/// </summary>
/// <remarks>
/// A schema key tells a consumer which renderer to use, so changing one silently is a breaking
/// change for anything that draws this artifact. This package carries the key as a literal because
/// it builds against the published Core, which means nothing but a test keeps it in step with
/// <c>TestFramework.Core.Debugger.DebugValueSchemaKeys</c>.
/// </remarks>
public class ArtifactSchemaKeyTests
{
    [Fact]
    public void FileArtifactReportsTheCanonicalSchemaKey()
        => Assert.Equal("tf.artifact.file", new FileArtifactDescriber().DebugValueSchemaKey);

    [Fact]
    public void SchemaKeyIsNotTheClrTypeName()
    {
        // The default is GetType().FullName. If the override is ever dropped, the key silently
        // becomes an implementation detail and renaming the class breaks every consumer.
        FileArtifactDescriber describer = new();

        Assert.NotEqual(describer.GetType().FullName, describer.DebugValueSchemaKey);
    }
}
