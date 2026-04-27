using TestFramework.Core.Artifacts;

namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// Identifies the LocalIO file artifact kind.
/// </summary>
public class FileArtifactKind : ArtifactKind<FileArtifactDescriber, FileArtifactData, FileArtifactReference>, IStaticArtifactKind<FileArtifactKind>
{
    /// <summary>
    /// Gets the singleton file artifact kind instance.
    /// </summary>
    public static FileArtifactKind Kind { get; } = new FileArtifactKind();
}