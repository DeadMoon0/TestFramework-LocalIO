using System;
using System.IO;
using System.Linq;

namespace TestFramework.LocalIO;

/// <summary>
/// Whether a program a command names can be found, asked before it is started through <c>setsid</c>.
/// </summary>
/// <remarks>
/// Started directly, a missing program is refused by the operating system before anything runs. Started through
/// <c>setsid</c> - which is how a command's children are kept in one group that can be stopped - <c>setsid</c> itself
/// starts fine and then exits 127, so the step completed with an exit code instead of saying the program does not
/// exist. Looking the program up first gives both platforms the same refusal.
/// </remarks>
internal static class ProgramLocation
{
    /// <summary>
    /// Whether <paramref name="program"/> names a file that exists: a path, relative to the working directory, or a
    /// name found on <paramref name="searchPath"/>.
    /// </summary>
    /// <param name="program">The program as the command names it.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="searchPath">The <c>PATH</c> the command would search, or null when there is none.</param>
    /// <returns>True when the program can be found.</returns>
    public static bool CanBeFound(string program, string workingDirectory, string? searchPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);

        if (program.Contains(Path.DirectorySeparatorChar) || program.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(Path.Combine(workingDirectory, program));
        }

        return (searchPath ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, program)));
    }
}
