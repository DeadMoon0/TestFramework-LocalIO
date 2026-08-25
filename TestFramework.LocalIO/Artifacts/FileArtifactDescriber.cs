using TestFramework.Core.Steps;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO.Artifacts;

/// <summary>
/// Sets up and tears down file artifacts on disk.
/// </summary>
public class FileArtifactDescriber : ArtifactDescriber<FileArtifactDescriber, FileArtifactData, FileArtifactReference>
{
    /// <summary>
    /// The canonical key mirrored from <c>TestFramework.Core.Debugger.DebugValueSchemaKeys.File</c>.
    /// </summary>
    /// <remarks>
    /// A literal rather than a reference because this package builds against the published Core.
    /// <c>FileArtifactSchemaTests</c> pins it to the canonical value so the two cannot drift.
    /// </remarks>
    internal const string SchemaKey = "tf.artifact.file";

    /// <summary>
    /// Identifies this artifact's debug shape, so a consumer renders it as a file rather than
    /// falling back to a generic view keyed on the CLR type name.
    /// </summary>
    public override string DebugValueSchemaKey => SchemaKey;

    /// <inheritdoc />
    public override JToken? CreateDebugValueCustomPayload(ArtifactInstanceGeneric instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.Reference is not FileArtifactReference reference)
            return null;

        // Read through the pinned path only. Asking a reference for its path before setup pinned it
        // throws by design, and a debug payload must never be the thing that fails a run.
        string? path = reference.TryGetPinnedPath();
        if (path is null)
            return null;

        return new JObject
        {
            ["path"] = path,
            ["fileName"] = Path.GetFileName(path),
            ["extension"] = Path.GetExtension(path),
            ["length"] = File.Exists(path) ? new FileInfo(path).Length : null
        };
    }

    /// <inheritdoc />
    public override Task Deconstruct(RunContext context, FileArtifactReference reference)
    {
        string path = reference.GetPath(context.Variables);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        // Only ever remove a directory this run created. Otherwise the opt-in would silently take
        // out a pre-existing directory that merely happened to be empty afterwards.
        if (reference.ShouldRemoveParentDirectoryIfEmpty && reference.SetupCreatedParentDirectory)
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
    public override async Task Setup(RunContext context, FileArtifactData data, FileArtifactReference reference)
    {
        string path = reference.GetPath(context.Variables);

        // Setup used to fail with a raw DirectoryNotFoundException while cleanup was already able
        // to remove a directory - create the parent so the two sides are symmetric.
        string? parentDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parentDirectory) && !Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
            reference.MarkSetupCreatedParentDirectory();
            context.Logger.LogInformation($"Created the parent directory \"{parentDirectory}\" for the file artifact.");
        }

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