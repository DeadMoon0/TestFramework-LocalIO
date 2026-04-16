using System.Text;
using TestFramework.Core.Artifacts;

namespace TestFrameworkLocalIO.Artifacts;

public class FileArtifactData(byte[] data) : ArtifactData<FileArtifactData, FileArtifactDescriber, FileArtifactReference>
{
    public byte[] Data { get => [.. data]; }
    public string DataAsUtf8String { get => Encoding.UTF8.GetString(data); }

    public override string ToString() => $"File [{data.Length} bytes]";
}
