using System;
using System.IO;
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

    /// <inheritdoc />
    public override string ToString() => hasPinnedPath ? $"File: \"{pinnedPath}\"" : "File: (unresolved)";
}