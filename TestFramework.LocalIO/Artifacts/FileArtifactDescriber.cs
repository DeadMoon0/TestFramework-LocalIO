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
    public override async Task Setup(IServiceProvider serviceProvider, FileArtifactData data, FileArtifactReference reference, VariableStore variableStore, ScopedLogger logger)
    {
        string path = reference.GetPath(variableStore);

        // Stream the content instead of File.WriteAllBytesAsync: the ReadOnlyMemory overload of
        // that method only exists from .NET 9, and this package also targets net8.0.
        FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(data.Content).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override string ToString() => "File";
}