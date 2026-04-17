using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

public class FileArtifactFolderFinder(VariableReference<string> folderPath) : ArtifactFinder<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
{
    public override Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string? filePath = Directory.GetFiles(folderPath.GetRequiredValue(variableStore)).FirstOrDefault();
        if (filePath == null)
        {
            logger.LogWarning("No files found in folder: {FolderPath}", folderPath.GetRequiredValue(variableStore));
            return Task.FromResult<ArtifactFinderResult?>(null);
        }

        var artifactReference = new FileArtifactReference(filePath);
        return Task.FromResult<ArtifactFinderResult?>(new ArtifactFinderResult(artifactReference));
    }

    public override Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ArtifactFinderResultMulti result = new ArtifactFinderResultMulti(Directory.GetFiles(folderPath.GetRequiredValue(variableStore))
            .Select((filePath, index) => new ArtifactFinderResult(new FileArtifactReference(filePath)))
            .ToArray());
        return Task.FromResult(result);
    }
}