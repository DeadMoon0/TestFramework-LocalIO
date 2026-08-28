using System;
using System.IO;
using TestFramework.Core.Exceptions;
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

        // Rooted but not fully qualified - '\results\out.txt' or 'C:out.txt' on Windows - is refused
        // by name: Path.Combine would return it unchanged, so it silently escaped the run directory
        // onto the current drive's root and two "isolated" runs shared the same file. Where such a
        // path points depends on the current drive and per-drive directory, which is not a thing a
        // test should ever depend on.
        if (Path.IsPathRooted(raw))
        {
            throw new FrameworkConfigurationException(
                $"The path \"{raw}\" is rooted but not fully qualified, so where it points depends on the current drive.",
                [
                    "State the fully qualified path, drive letter included.",
                    "Or use a relative path, which resolves against the LocalIO run directory.",
                ]);
        }

        string? runDirectory = TryGetRunDirectory(variableStore);
        return runDirectory is null ? Path.GetFullPath(raw) : Path.Combine(runDirectory, raw);
    }

    /// <summary>
    /// Whether a directory has the shape only <see cref="RunDirectoryStep"/> creates.
    /// </summary>
    /// <remarks>
    /// The run directory travels on a plain variable that anything can overwrite, so the recursive
    /// cleanup delete never trusts the variable alone: only a <c>tf-localio-&lt;guid&gt;</c> leaf - a name
    /// this step and nothing else produces - is ever removed. A forged value can at worst point the
    /// delete at another run's litter, never at a directory the framework did not create.
    /// </remarks>
    internal static bool IsRunDirectoryShaped(string path)
    {
        string leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (leaf.Length != "tf-localio-".Length + 32 || !leaf.StartsWith("tf-localio-", StringComparison.Ordinal))
            return false;

        for (int i = "tf-localio-".Length; i < leaf.Length; i++)
        {
            if (!char.IsAsciiHexDigitLower(leaf[i]))
                return false;
        }

        return true;
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
