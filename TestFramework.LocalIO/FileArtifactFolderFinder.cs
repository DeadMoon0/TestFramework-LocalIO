using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

/// <summary>
/// Finds file artifacts from a folder on disk.
/// </summary>
/// <param name="folderPath">The folder to search.</param>
public class FileArtifactFolderFinder(VariableReference<string> folderPath) : ArtifactFinder<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
{
    /// <summary>
    /// Returns the first file found in the target folder, or <see langword="null"/> when the folder is empty.
    /// </summary>
    public override Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string? filePath = Directory.GetFiles(folderPath.GetRequiredValue(variableStore)).FirstOrDefault();
        if (filePath == null)
        {
            logger.LogWarning($"No files found in folder: {folderPath.GetRequiredValue(variableStore)}");
            return Task.FromResult<ArtifactFinderResult?>(null);
        }

        var artifactReference = new FileArtifactReference(filePath);
        return Task.FromResult<ArtifactFinderResult?>(new ArtifactFinderResult(artifactReference));
    }

    /// <summary>
    /// Returns all files found in the target folder.
    /// </summary>
    /// <remarks>The result order matches <see cref="Directory.GetFiles(string)"/> and should not be treated as a stable semantic ordering.</remarks>
    public override Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ArtifactFinderResultMulti result = new ArtifactFinderResultMulti(Directory.GetFiles(folderPath.GetRequiredValue(variableStore))
            .Select((filePath, index) => new ArtifactFinderResult(new FileArtifactReference(filePath)))
            .ToArray());
        return Task.FromResult(result);
    }
}