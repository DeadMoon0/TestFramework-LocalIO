using TestFramework.Core.Steps;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
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
    /// Returns the one file in the target folder, or <see langword="null"/> when the folder is empty
    /// or does not exist.
    /// </summary>
    /// <exception cref="FrameworkConfigurationException">The folder holds more than one file.</exception>
    /// <remarks>
    /// Two candidates for one artifact is a stated error, never a coin toss: this used to take the
    /// first file in filesystem enumeration order, so a stray <c>.tmp</c> or <c>desktop.ini</c> beside
    /// the expected file silently bound the wrong artifact and the failure surfaced in whatever
    /// asserted on the content. A folder with several wanted files is what <see cref="FindMultiAsync"/>
    /// is for.
    /// </remarks>
    public override Task<ArtifactFinderResult?> FindAsync(RunContext context)
    {
        string? folder = ResolveFolder(context.Variables, context.Logger);
        if (folder is null) return Task.FromResult<ArtifactFinderResult?>(null);

        // Six is enough to refuse with names without materializing a huge listing.
        string[] files = [.. Directory.EnumerateFiles(folder).Take(6)];
        if (files.Length == 0)
        {
            context.Logger.LogWarning($"No files found in folder: {folder}");
            return Task.FromResult<ArtifactFinderResult?>(null);
        }

        if (files.Length > 1)
        {
            string names = string.Join(", ", files.Take(5).Select(Path.GetFileName)) + (files.Length > 5 ? ", ..." : string.Empty);
            throw new FrameworkConfigurationException(
                $"The folder \"{folder}\" holds more than one file ({names}), so which one is the artifact is ambiguous.",
                [
                    "Point the finder at a folder that holds the one file.",
                    "Or discover them all with FindArtifacts, which takes every file.",
                ]);
        }

        return Task.FromResult<ArtifactFinderResult?>(new ArtifactFinderResult(new FileArtifactReference(files[0])));
    }

    /// <summary>
    /// Returns all files found in the target folder, or nothing when the folder does not exist.
    /// </summary>
    /// <remarks>The result order matches <see cref="Directory.EnumerateFiles(string)"/> and should not be treated as a stable semantic ordering.</remarks>
    public override Task<ArtifactFinderResultMulti> FindMultiAsync(RunContext context)
    {
        string? folder = ResolveFolder(context.Variables, context.Logger);
        if (folder is null) return Task.FromResult(new ArtifactFinderResultMulti([]));

        ArtifactFinderResult[] results = Directory.EnumerateFiles(folder)
            .Select(filePath => new ArtifactFinderResult(new FileArtifactReference(filePath)))
            .ToArray();

        if (results.Length == 0)
            context.Logger.LogWarning($"No files found in folder: {folder}");

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
