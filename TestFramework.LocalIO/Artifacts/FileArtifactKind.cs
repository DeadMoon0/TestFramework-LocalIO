using TestFramework.Core.Artifacts;

namespace TestFrameworkLocalIO.Artifacts;

public class FileArtifactKind : ArtifactKind<FileArtifactDescriber, FileArtifactData, FileArtifactReference>, IStaticArtifactKind<FileArtifactKind>
{
    public static FileArtifactKind Kind { get; } = new FileArtifactKind();
}