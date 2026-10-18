using System;
using System.Text;
using TestFramework.Core.Artifacts;

namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// Represents file artifact content.
/// </summary>
public class FileArtifactData : ArtifactData<FileArtifactData, FileArtifactDescriber, FileArtifactReference>
{
    private readonly byte[] data;

    /// <summary>
    /// Creates file content from a copy of <paramref name="data"/>.
    /// </summary>
    /// <remarks>
    /// Copied once, here: an artifact version is evidence of what the file held, and a version that shared the
    /// caller's array would change whenever the caller reused it - even after the run that recorded it had
    /// finished.
    /// </remarks>
    /// <param name="data">The raw file bytes.</param>
    public FileArtifactData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        this.data = (byte[])data.Clone();
    }

    /// <summary>
    /// Creates file content from bytes nobody else holds, without copying them.
    /// </summary>
    /// <param name="owned">The bytes, handed over.</param>
    internal FileArtifactData(OwnedBytes owned)
    {
        ArgumentNullException.ThrowIfNull(owned.Bytes);

        this.data = owned.Bytes;
    }

    /// <summary>
    /// Gets the raw file bytes, without copying them again; read-only, so they stay what was recorded.
    /// </summary>
    public ReadOnlyMemory<byte> Content => data;

    /// <summary>
    /// Gets the file content decoded as UTF-8 text.
    /// </summary>
    public string DataAsUtf8String { get => Encoding.UTF8.GetString(data); }

    /// <inheritdoc />
    public override string ToString() => $"File [{data.Length} bytes]";
}
