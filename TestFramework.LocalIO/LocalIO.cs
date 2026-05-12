using System;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

/// <summary>
/// Provides the public LocalIO entry points for artifacts, triggers, and events.
/// </summary>
public static class LocalIO
{
    /// <summary>
    /// Gets the LocalIO artifact helpers.
    /// </summary>
    public static LocalIOArtifacts Artifacts { get; } = new LocalIOArtifacts();

    /// <summary>
    /// Gets the LocalIO trigger helpers.
    /// </summary>
    public static LocalIOTrigger Trigger { get; } = new LocalIOTrigger();

    /// <summary>
    /// Gets the LocalIO event helpers.
    /// </summary>
    public static LocalIOEvents Events { get; } = new LocalIOEvents();
}

/// <summary>
/// Provides fluent helpers for LocalIO artifact kinds and references.
/// </summary>
public class LocalIOArtifacts
{
    /// <summary>
    /// Gets the file artifact kind used by LocalIO file artifacts.
    /// </summary>
    public FileArtifactKind FileKind => FileArtifactKind.Kind;

    /// <summary>
    /// Creates a file artifact reference from a variable-backed path.
    /// </summary>
    /// <param name="path">The variable reference that resolves to the file path.</param>
    /// <returns>A file artifact reference.</returns>
    public FileArtifactReference FileRef(VariableReference<string> path)
    {
        return new FileArtifactReference(path);
    }
}

/// <summary>
/// Provides fluent helpers for LocalIO triggers.
/// </summary>
public class LocalIOTrigger
{
    /// <summary>
    /// Creates a command trigger that runs in the current working directory.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <returns>A command trigger.</returns>
    /// <remarks>On Windows this uses <c>CMD.EXE /C</c>. On Unix-like systems it prefers <c>/bin/bash -c</c> and falls back to <c>/bin/sh -c</c>.</remarks>
    public CmdTrigger Cmd(VariableReference<string> command)
    {
        return new CmdTrigger(command, Environment.CurrentDirectory);
    }

    /// <summary>
    /// Creates a command trigger that runs in the specified working directory.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="workingDirectory">The working directory for the command.</param>
    /// <returns>A command trigger.</returns>
    /// <remarks>On Windows this uses <c>CMD.EXE /C</c>. On Unix-like systems it prefers <c>/bin/bash -c</c> and falls back to <c>/bin/sh -c</c>.</remarks>
    public CmdTrigger Cmd(VariableReference<string> command, VariableReference<string> workingDirectory)
    {
        return new CmdTrigger(command, workingDirectory);
    }
}

/// <summary>
/// Provides fluent helpers for LocalIO events.
/// </summary>
public class LocalIOEvents
{
    /// <summary>
    /// Creates a polling event that completes when a file exists.
    /// </summary>
    /// <param name="path">The path to watch.</param>
    /// <returns>A file-exists event.</returns>
    public FileExistsEvent FileExists(VariableReference<string> path)
    {
        return new FileExistsEvent(path);
    }

    /// <summary>
    /// Creates a polling event that completes when a file exists and uses a custom poll delay.
    /// </summary>
    /// <param name="path">The path to watch.</param>
    /// <param name="pollDelay">The delay between polling attempts.</param>
    /// <returns>A file-exists event.</returns>
    public FileExistsEvent FileExists(VariableReference<string> path, VariableReference<TimeSpan> pollDelay)
    {
        return new FileExistsEvent(path, pollDelay);
    }
}