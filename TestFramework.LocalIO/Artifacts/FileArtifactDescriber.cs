using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFrameworkLocalIO.Artifacts;

public class FileArtifactDescriber : ArtifactDescriber<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
{
    public override Task Deconstruct(IServiceProvider serviceProvider, FileArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        if (File.Exists(reference.GetPath(variableStore))) File.Delete(reference.GetPath(variableStore));
        return Task.CompletedTask;
    }

    public override Task Setup(IServiceProvider serviceProvider, FileArtifactData data, FileArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        return File.WriteAllBytesAsync(reference.GetPath(variableStore), data.Data);
    }

    public override string ToString() => "File";
}