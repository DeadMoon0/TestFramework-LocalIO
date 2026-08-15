using System;
using System.Text;
using TestFramework.Core.Artifacts;

namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// Represents file artifact content.
/// </summary>
/// <param name="data">The raw file bytes.</param>
public class FileArtifactData(byte[] data) : ArtifactData<FileArtifactData, FileArtifactDescriber, FileArtifactReference>
{
    /// <summary>
    /// Gets the raw file bytes without copying them.
    /// </summary>
    public ReadOnlyMemory<byte> Content => data;

    /// <summary>
    /// Gets a fresh copy of the raw file bytes.
    /// </summary>
    [Obsolete("Data copies the whole file on every access. Use Content instead, or Content.ToArray() when a mutable copy is genuinely required.")]
    public byte[] Data { get => [.. data]; }

    /// <summary>
    /// Gets the file content decoded as UTF-8 text.
    /// </summary>
    public string DataAsUtf8String { get => Encoding.UTF8.GetString(data); }

    /// <inheritdoc />
    public override string ToString() => $"File [{data.Length} bytes]";
}
