using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Artifacts;

public class FileArtifactReference : ArtifactReference<FileArtifactReference, FileArtifactDescriber, FileArtifactData>
{
    private string pinnedPath = "";

    private VariableReference<string> path;

    public FileArtifactReference(VariableReference<string> path)
    {
        this.path = path;
        CanDeconstruct = true;
    }

    public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
    {
        pinnedPath = path.GetValue(variableStore) ?? throw new InvalidOperationException("The Path to a File cannot be NULL.");
    }

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

    public override string ToString() => IsPinned ? $"File: \"{pinnedPath}\"" : "File: (unresolved)";
}