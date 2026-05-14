using System;
using System.IO;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
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

    /// <inheritdoc />
    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        pinnedPath = path.GetValue(variableStore) ?? throw new InvalidOperationException("The Path to a File cannot be NULL.");
    }

    /// <inheritdoc />
    public override async Task<ArtifactResolveResult<FileArtifactDescriber, FileArtifactData, FileArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
    {
        string _path = path.GetValue(variableStore) ?? throw new ArgumentNullException();
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
        if (IsPinned) return pinnedPath;
        return path.GetValue(variableStore) ?? throw new InvalidOperationException("The Path to a File cannot be NULL.");
    }

    internal bool ShouldRemoveParentDirectoryIfEmpty => removeParentDirectoryIfEmpty;

    /// <inheritdoc />
    public override string ToString() => IsPinned ? $"File: \"{pinnedPath}\"" : "File: (unresolved)";
}