using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

/// <summary>
/// Finds file artifacts from a folder on disk.
/// </summary>
/// <remarks>
/// The references it produces are ordinary, deletable file references, so teardown removes what this
/// finder discovered - the same default every other artifact gets. Chain <c>MarkReadonly()</c> onto the
/// <c>FindArtifact</c> / <c>FindArtifacts</c> call for a folder the run only reads.
/// </remarks>
/// <param name="folderPath">The folder to search.</param>
public class FileArtifactFolderFinder(VariableReference<string> folderPath) : ArtifactFinder<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
{
    /// <summary>
    /// Returns the first file found in the target folder, or <see langword="null"/> when the folder
    /// is empty or does not exist.
    /// </summary>
    public override Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string? folder = ResolveFolder(variableStore, logger);
        if (folder is null) return Task.FromResult<ArtifactFinderResult?>(null);

        // EnumerateFiles stops at the first hit instead of materializing the whole listing.
        string? filePath = Directory.EnumerateFiles(folder).FirstOrDefault();
        if (filePath is null)
        {
            logger.LogWarning($"No files found in folder: {folder}");
            return Task.FromResult<ArtifactFinderResult?>(null);
        }

        return Task.FromResult<ArtifactFinderResult?>(new ArtifactFinderResult(new FileArtifactReference(filePath)));
    }

    /// <summary>
    /// Returns all files found in the target folder, or nothing when the folder does not exist.
    /// </summary>
    /// <remarks>The result order matches <see cref="Directory.EnumerateFiles(string)"/> and should not be treated as a stable semantic ordering.</remarks>
    public override Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        string? folder = ResolveFolder(variableStore, logger);
        if (folder is null) return Task.FromResult(new ArtifactFinderResultMulti([]));

        ArtifactFinderResult[] results = Directory.EnumerateFiles(folder)
            .Select(filePath => new ArtifactFinderResult(new FileArtifactReference(filePath)))
            .ToArray();

        if (results.Length == 0)
            logger.LogWarning($"No files found in folder: {folder}");

        return Task.FromResult(new ArtifactFinderResultMulti(results));
    }

    /// <summary>
    /// Resolves the folder once, or returns <see langword="null"/> after warning when it is missing.
    /// </summary>
    private string? ResolveFolder(VariableStore variableStore, ScopedLogger logger)
    {
        string folder = LocalPath.Resolve(folderPath.GetRequiredValue(variableStore), variableStore);
        if (Directory.Exists(folder)) return folder;

        logger.LogWarning($"The folder \"{folder}\" does not exist, so no file artifacts were found.");
        return null;
    }
}
