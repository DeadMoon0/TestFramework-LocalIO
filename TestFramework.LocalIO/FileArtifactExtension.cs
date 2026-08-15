using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
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
    /// Gets every file artifact that <c>FindArtifacts(baseIdentifier, ...)</c> generated, in
    /// identifier order.
    /// </summary>
    /// <param name="store">The artifact store.</param>
    /// <param name="baseIdentifier">The base identifier passed to <c>FindArtifacts</c>.</param>
    /// <returns>The <c>baseIdentifier_0</c>, <c>baseIdentifier_1</c>, ... instances, ordered by index.</returns>
    /// <remarks>
    /// Discovery generates the identifiers by appending <c>_0</c>, <c>_1</c>, ... to the base
    /// identifier. The index order is the discovery order, which is filesystem-dependent - sort by
    /// <see cref="FileArtifactReference.FilePath"/> when the assertion needs a deterministic order.
    /// </remarks>
    public static IReadOnlyList<ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference>> GetFileArtifacts(this ArtifactStore store, string baseIdentifier)
    {
        string prefix = baseIdentifier + "_";
        return store.GetAll()
            .Select(instance => instance.Identifier.Identifier)
            .Where(identifier => identifier.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(identifier.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .OrderBy(identifier => int.Parse(identifier[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture))
            .Select(identifier => store.GetFileArtifact(identifier))
            .ToList();
    }

    /// <summary>
    /// Adds a UTF-8 text file artifact to the timeline run.
    /// </summary>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="path">The file path represented by the artifact.</param>
    /// <param name="utf8text">The UTF-8 text content.</param>
    /// <param name="removeParentDirectoryIfEmpty">Removes the parent directory during cleanup when the file was its last remaining entry.</param>
    /// <returns>The current run builder.</returns>
    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, string path, string utf8text, bool removeParentDirectoryIfEmpty = false)
    {
        FileArtifactReference reference = new FileArtifactReference(path);
        if (removeParentDirectoryIfEmpty)
            reference.RemoveParentDirectoryIfEmpty();

        return run.AddArtifact(identifier, reference, new FileArtifactData(Encoding.UTF8.GetBytes(utf8text)));
    }

    /// <summary>
    /// Adds a binary file artifact to the timeline run.
    /// </summary>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="path">The file path represented by the artifact.</param>
    /// <param name="data">The binary content.</param>
    /// <param name="removeParentDirectoryIfEmpty">Removes the parent directory during cleanup when the file was its last remaining entry.</param>
    /// <returns>The current run builder.</returns>
    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, string path, byte[] data, bool removeParentDirectoryIfEmpty = false)
    {
        FileArtifactReference reference = new FileArtifactReference(path);
        if (removeParentDirectoryIfEmpty)
            reference.RemoveParentDirectoryIfEmpty();

        return run.AddArtifact(identifier, reference, new FileArtifactData(data));
    }

    /// <summary>
    /// Returns an assertion handle for a file artifact in a completed run.
    /// </summary>
    public static ArtifactHandle<FileArtifactData> FileArtifact(this TimelineRun run, ArtifactIdentifier identifier)
        => run.Artifact<FileArtifactData>(identifier);

    /// <summary>
    /// Returns a copy of the latest file bytes as a value handle.
    /// </summary>
    /// <remarks>Prefer <see cref="Content(ArtifactHandle{FileArtifactData})"/>, which does not copy.</remarks>
    public static ValueHandle<byte[]> Bytes(this ArtifactHandle<FileArtifactData> handle)
        => handle.Select(data => data.Content.ToArray());

    /// <summary>
    /// Returns the latest file bytes as a value handle without copying them.
    /// </summary>
    public static ValueHandle<ReadOnlyMemory<byte>> Content(this ArtifactHandle<FileArtifactData> handle)
        => handle.Select(data => data.Content);

    /// <summary>
    /// Returns the latest file content decoded as UTF-8 text.
    /// </summary>
    public static ValueHandle<string> Utf8Text(this ArtifactHandle<FileArtifactData> handle)
        => handle.Select(data => data.DataAsUtf8String);
}
