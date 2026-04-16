using System.Text;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFrameworkLocalIO.Artifacts;

namespace TestFrameworkLocalIO;

public static class FileArtifactExtension
{
    public static ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> GetFileArtifact(this ArtifactStore store, ArtifactIdentifier identifier)
    {
        return store.GetArtifact(FileArtifactKind.Kind, identifier);
    }

    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, string path, string utf8text)
    {
        return run.AddArtifact(identifier, new FileArtifactReference(path), new FileArtifactData(Encoding.UTF8.GetBytes(utf8text)));
    }

    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, string path, byte[] data)
    {
        return run.AddArtifact(identifier, new FileArtifactReference(path), new FileArtifactData(data));
    }
}
