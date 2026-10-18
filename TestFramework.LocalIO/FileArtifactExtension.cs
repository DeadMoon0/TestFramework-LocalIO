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
    /// <para>
    /// Discovery generates the identifiers by appending <c>_0</c>, <c>_1</c>, ... to the base
    /// identifier. The index order is the discovery order, which is filesystem-dependent - sort by
    /// <see cref="FileArtifactReference.FilePath"/> when the assertion needs a deterministic order.
    /// </para>
    /// <para>
    /// Only the unbroken run from <c>_0</c> is returned, because that is the shape discovery produces. An
    /// artifact of your own that happens to be named <c>baseIdentifier_7</c> after three discovered files is
    /// not one of them, and used to be returned as if it were.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference>> GetFileArtifacts(this ArtifactStore store, ArtifactIdentifier baseIdentifier)
    {
        ArgumentNullException.ThrowIfNull(baseIdentifier);

        HashSet<string> held = [.. store.GetAll().Select(instance => instance.Identifier.Identifier)];
        List<ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference>> discovered = [];

        for (int index = 0; held.Contains(Generated(baseIdentifier, index)); index++)
            discovered.Add(store.GetFileArtifact(Generated(baseIdentifier, index)));

        return discovered;

        static string Generated(ArtifactIdentifier baseIdentifier, int index)
            => baseIdentifier.Identifier + "_" + index.ToString(CultureInfo.InvariantCulture);
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
        => run.AddFileArtifact(identifier, new FileArtifactReference(path), utf8text);

    /// <summary>
    /// Adds a UTF-8 text file artifact at a reference set up beforehand, for example with
    /// <c>LocalIOExt.Artifacts.FileRef(path).RemoveParentDirectoryIfEmpty()</c>.
    /// </summary>
    /// <remarks>
    /// The reference carries its own cleanup options, so there is one way to set each: a flag here beside the
    /// reference's own method would have been a second, and a bare <c>true</c> at the call site says nothing.
    /// </remarks>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="reference">Where the file lives, and how cleanup treats it.</param>
    /// <param name="utf8text">The UTF-8 text content.</param>
    /// <returns>The current run builder.</returns>
    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, FileArtifactReference reference, string utf8text)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return run.AddArtifact(identifier, reference, new FileArtifactData(new OwnedBytes(Encoding.UTF8.GetBytes(utf8text))));
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
        => run.AddFileArtifact(identifier, new FileArtifactReference(path), data);

    /// <summary>
    /// Adds a binary file artifact at a reference set up beforehand, for example with
    /// <c>LocalIOExt.Artifacts.FileRef(path).RemoveParentDirectoryIfEmpty()</c>.
    /// </summary>
    /// <remarks>
    /// The reference carries its own cleanup options, so there is one way to set each: a flag here beside the
    /// reference's own method would have been a second, and a bare <c>true</c> at the call site says nothing.
    /// </remarks>
    /// <param name="run">The timeline run builder.</param>
    /// <param name="identifier">The artifact identifier.</param>
    /// <param name="reference">Where the file lives, and how cleanup treats it.</param>
    /// <param name="data">The binary content.</param>
    /// <returns>The current run builder.</returns>
    public static ITimelineRunBuilder AddFileArtifact(this ITimelineRunBuilder run, ArtifactIdentifier identifier, FileArtifactReference reference, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(reference);

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
