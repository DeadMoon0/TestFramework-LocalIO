using System;
using System.IO;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Resolves the raw path strings handed to LocalIO steps, events and artifacts against the
/// per-run directory published by <see cref="RunDirectoryStep"/>.
/// </summary>
internal static class LocalPath
{
    /// <summary>
    /// The well-known variable that carries the directory created by <see cref="RunDirectoryStep"/>.
    /// </summary>
    internal const string RunDirectoryVariable = "localIoRunDirectory";

    /// <summary>
    /// Resolves a raw path.
    /// </summary>
    /// <remarks>
    /// Fully qualified paths are used as-is. Relative paths are combined onto the run directory
    /// when one is present. Without a run directory the legacy behaviour applies and the path is
    /// resolved against the process-wide <see cref="Environment.CurrentDirectory"/>.
    /// </remarks>
    internal static string Resolve(string raw, VariableStore variableStore)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        if (Path.IsPathFullyQualified(raw)) return raw;

        string? runDirectory = TryGetRunDirectory(variableStore);
        return runDirectory is null ? Path.GetFullPath(raw) : Path.Combine(runDirectory, raw);
    }

    /// <summary>
    /// Returns the run directory when the timeline declared one, otherwise <see langword="null"/>.
    /// </summary>
    internal static string? TryGetRunDirectory(VariableStore variableStore)
    {
        if (variableStore.TryGetVariable(RunDirectoryVariable, out string? runDirectory)
            && !string.IsNullOrWhiteSpace(runDirectory))
        {
            return runDirectory;
        }

        return null;
    }
}
