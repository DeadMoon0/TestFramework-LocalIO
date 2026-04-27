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
    /// Gets the raw file bytes.
    /// </summary>
    public byte[] Data { get => [.. data]; }

    /// <summary>
    /// Gets the file content decoded as UTF-8 text.
    /// </summary>
    public string DataAsUtf8String { get => Encoding.UTF8.GetString(data); }

    /// <inheritdoc />
    public override string ToString() => $"File [{data.Length} bytes]";
}
