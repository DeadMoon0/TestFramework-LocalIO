using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// References a file on disk for LocalIO artifact operations.
/// </summary>
public class FileArtifactReference : ArtifactReference<FileArtifactReference, FileArtifactDescriber, FileArtifactData>
{
    private string pinnedPath = "";
    private bool hasPinnedPath;
    private bool removeParentDirectoryIfEmpty;
    private bool setupCreatedParentDirectory;

    private VariableReference<string> path;

    /// <summary>
    /// Creates a file artifact reference from a variable-backed path.
    /// </summary>
    /// <param name="path">The variable reference that resolves to the file path.</param>
    public FileArtifactReference(VariableReference<string> path)
    {
        this.path = path;
        CanDeconstruct = true;
    }

    /// <summary>
    /// Removes the parent directory during cleanup when the artifact file was the last remaining entry.
    /// </summary>
    /// <returns>The same artifact reference for fluent chaining.</returns>
    public FileArtifactReference RemoveParentDirectoryIfEmpty()
    {
        removeParentDirectoryIfEmpty = true;
        return this;
    }

    /// <summary>
    /// Marks the file as merely observed, so cleanup never deletes it.
    /// </summary>
    /// <remarks>Use this for files the run did not create - registering or discovering a file is not a licence to delete it.</remarks>
    /// <returns>The same artifact reference for fluent chaining.</returns>
    public FileArtifactReference Observed()
    {
        CanDeconstruct = false;
        return this;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The first pin wins. Core calls this directly instead of going through <c>PinReference</c>,
    /// so <c>IsPinned</c> cannot be trusted here: without its own flag the reference would keep
    /// re-resolving the variable, and rebinding it mid-run would retarget the cleanup delete onto
    /// a different file than Setup wrote.
    /// </remarks>
    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        if (hasPinnedPath) return;
        pinnedPath = ResolvePath(variableStore);
        hasPinnedPath = true;
    }

    /// <inheritdoc />
    public override async Task<ArtifactResolveResult<FileArtifactDescriber, FileArtifactData, FileArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        string _path = ResolvePath(variableStore);
        if (!File.Exists(_path)) return new ArtifactResolveResult<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
        {
            Found = false,
            Data = null,
        };
        return new ArtifactResolveResult<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
        {
            Found = true,
            Data = new FileArtifactData(await File.ReadAllBytesAsync(_path)) { Identifier = versionIdentifier },
        };
    }

    /// <summary>
    /// Gets the resolved path this reference is pinned to.
    /// </summary>
    /// <remarks>Named <c>FilePath</c> rather than <c>Path</c> so it does not shadow <see cref="System.IO.Path"/> inside this class.</remarks>
    /// <exception cref="FrameworkStateException">Thrown when the reference has not been resolved yet.</exception>
    // Core snapshots references for the debugger by serializing their public properties, so a
    // property that throws before the pin has to stay out of that snapshot.
    [IgnoreDataMember]
    public string FilePath => hasPinnedPath
        ? pinnedPath
        : throw new FrameworkStateException("The file artifact path is not resolved yet. Read FilePath after the run has registered, discovered or set up the artifact.");

    /// <summary>
    /// Returns the pinned path, or null when the reference has not been resolved yet.
    /// </summary>
    /// <remarks>
    /// The non-throwing counterpart to <see cref="FilePath"/>, for callers that must tolerate an
    /// unresolved reference — building a debug payload, above all. A debug view must never be the
    /// reason a run fails.
    /// </remarks>
    internal string? TryGetPinnedPath() => hasPinnedPath ? pinnedPath : null;

    /// <inheritdoc />
    public override void DeclareIO(StepIOContract contract)
    {
        if (path.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(path.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }

    internal string GetPath(VariableStore variableStore)
    {
        if (hasPinnedPath) return pinnedPath;
        return ResolvePath(variableStore);
    }

    private string ResolvePath(VariableStore variableStore)
    {
        string raw = path.GetValue(variableStore) ?? throw new FrameworkStateException("The path to a file cannot be null.");
        return LocalPath.Resolve(raw, variableStore);
    }

    internal bool ShouldRemoveParentDirectoryIfEmpty => removeParentDirectoryIfEmpty;

    /// <summary>
    /// Records that Setup had to create the parent directory of this artifact.
    /// </summary>
    internal void MarkSetupCreatedParentDirectory() => setupCreatedParentDirectory = true;

    /// <summary>
    /// Gets whether Setup created the parent directory of this artifact.
    /// </summary>
    /// <remarks>
    /// Cleanup only removes a parent directory it created itself, so opting into
    /// <see cref="RemoveParentDirectoryIfEmpty"/> cannot take out a directory that was already there.
    /// </remarks>
    internal bool SetupCreatedParentDirectory => setupCreatedParentDirectory;

    /// <inheritdoc />
    public override string ToString() => hasPinnedPath ? $"File: \"{pinnedPath}\"" : "File: (unresolved)";
}