using System.Text;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

/// <summary>
/// Adds LocalIO-specific artifact helpers for timeline runs and artifact stores.
/// </summary>
public static class FileArtifactExtension
{
    /// <summary>
    /// Gets a strongly typed file artifact from the artifact store.
    /// </summary>
    /// <param name="store">The artifact store.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <returns>The resolved file artifact instance.</returns>
    public static ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> GetFileArtifact(this ArtifactStore store, ArtifactIdentifier identifier)
    {
        return store.GetArtifact(FileArtifactKind.Kind, identifier);
    }

    /// <summary>
    /// Adds a UTF-8 text file artifact to the timeline run.
    /// </summary>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="path">The file path represented by the artifact.</param>
    /// <param name="utf8text">The UTF-8 text content.</param>
    /// <returns>The current run builder.</returns>
    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, string path, string utf8text)
    {
        return run.AddArtifact(identifier, new FileArtifactReference(path), new FileArtifactData(Encoding.UTF8.GetBytes(utf8text)));
    }

    /// <summary>
    /// Adds a binary file artifact to the timeline run.
    /// </summary>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="path">The file path represented by the artifact.</param>
    /// <param name="data">The binary content.</param>
    /// <returns>The current run builder.</returns>
    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, string path, byte[] data)
    {
        return run.AddArtifact(identifier, new FileArtifactReference(path), new FileArtifactData(data));
    }
}
