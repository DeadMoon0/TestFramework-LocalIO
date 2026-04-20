using TestFramework.Core.Artifacts;

namespace TestFramework.LocalIO.Artifacts;

public class FileArtifactKind : ArtifactKind<FileArtifactDescriber, FileArtifactData, FileArtifactReference>, IStaticArtifactKind<FileArtifactKind>
{
    public static FileArtifactKind Kind { get; } = new FileArtifactKind();
}