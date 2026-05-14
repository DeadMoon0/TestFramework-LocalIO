using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// Sets up and tears down file artifacts on disk.
/// </summary>
public class FileArtifactDescriber : ArtifactDescriber<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
{
    /// <inheritdoc />
    public override Task Deconstruct(IServiceProvider serviceProvider, FileArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        string path = reference.GetPath(variableStore);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        if (reference.ShouldRemoveParentDirectoryIfEmpty)
        {
            string? parentDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parentDirectory)
                && Directory.Exists(parentDirectory)
                && !Directory.EnumerateFileSystemEntries(parentDirectory).Any())
            {
                Directory.Delete(parentDirectory, false);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Setup(IServiceProvider serviceProvider, FileArtifactData data, FileArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        return File.WriteAllBytesAsync(reference.GetPath(variableStore), data.Data);
    }

    /// <inheritdoc />
    public override string ToString() => "File";
}