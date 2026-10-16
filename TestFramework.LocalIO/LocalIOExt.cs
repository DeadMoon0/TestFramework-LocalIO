using System;
using TestFramework.Core.Variables;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO;

/// <summary>
/// Provides the public LocalIO entry points for artifacts, triggers, and events.
/// </summary>
public static class LocalIOExt
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
public sealed class LocalIOArtifacts
{
    // Reached through LocalIOExt, which holds the one instance.
    internal LocalIOArtifacts()
    {
    }

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
public sealed class LocalIOTrigger
{
    // Reached through LocalIOExt, which holds the one instance.
    internal LocalIOTrigger()
    {
    }

    /// <summary>
    /// Creates a command trigger that runs a shell line in the LocalIO run directory, or - when the timeline
    /// declared none - in the current working directory as it is at run time.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <returns>A command trigger.</returns>
    /// <remarks>
    /// On Windows this uses <c>%ComSpec% /C</c>. On Unix-like systems it prefers <c>/bin/bash -c</c> and falls back to
    /// <c>/bin/sh -c</c>. The line is inserted verbatim, so a run value inside it is shell syntax - quotes, <c>&amp;&amp;</c>,
    /// redirections and all. To pass values as values, use <see cref="Exec(VariableReference{string}, VariableReference{string}[])"/>.
    /// </remarks>
    public CmdTrigger Cmd(VariableReference<string> command)
    {
        return CmdTrigger.Shell(command, null);
    }

    /// <summary>
    /// Creates a command trigger that runs a shell line in the specified working directory.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    /// <param name="workingDirectory">The working directory for the command.</param>
    /// <returns>A command trigger.</returns>
    /// <remarks>
    /// On Windows this uses <c>%ComSpec% /C</c>. On Unix-like systems it prefers <c>/bin/bash -c</c> and falls back to
    /// <c>/bin/sh -c</c>. The line is inserted verbatim, so a run value inside it is shell syntax. To pass values as
    /// values, use <see cref="ExecIn(VariableReference{string}, VariableReference{string}, VariableReference{string}[])"/>.
    /// </remarks>
    public CmdTrigger Cmd(VariableReference<string> command, VariableReference<string> workingDirectory)
    {
        return CmdTrigger.Shell(command, workingDirectory);
    }

    /// <summary>
    /// Creates a trigger that starts a program directly, with no shell, in the LocalIO run directory - or, when the
    /// timeline declared none, in the current working directory as it is at run time.
    /// </summary>
    /// <param name="program">The program to start: a name found on PATH, or a path.</param>
    /// <param name="arguments">Its arguments, each passed as one argument whatever it contains.</param>
    /// <returns>A command trigger.</returns>
    /// <remarks>
    /// Nothing is parsed: a value with spaces, quotes or <c>&amp;&amp;</c> in it reaches the program exactly as it is,
    /// which is what makes this the form for run values. Shell features - pipes, redirections, built-ins - need
    /// <see cref="Cmd(VariableReference{string})"/>.
    /// </remarks>
    public CmdTrigger Exec(VariableReference<string> program, params VariableReference<string>[] arguments)
    {
        return CmdTrigger.Direct(program, arguments, null);
    }

    /// <summary>
    /// Creates a trigger that starts a program directly, with no shell, in the specified working directory.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the program.</param>
    /// <param name="program">The program to start: a name found on PATH, or a path.</param>
    /// <param name="arguments">Its arguments, each passed as one argument whatever it contains.</param>
    /// <returns>A command trigger.</returns>
    /// <remarks>The working directory comes first because the arguments take every value after the program.</remarks>
    public CmdTrigger ExecIn(VariableReference<string> workingDirectory, VariableReference<string> program, params VariableReference<string>[] arguments)
    {
        return CmdTrigger.Direct(program, arguments, workingDirectory);
    }
}

/// <summary>
/// Provides fluent helpers for LocalIO events.
/// </summary>
public sealed class LocalIOEvents
{
    // Reached through LocalIOExt, which holds the one instance.
    internal LocalIOEvents()
    {
    }

    /// <summary>
    /// Creates a polling event that completes when a file exists, checking every 500 ms.
    /// </summary>
    /// <param name="path">The path to watch.</param>
    /// <returns>A file-exists event.</returns>
    public FileExistsEvent FileExists(VariableReference<string> path)
    {
        return new FileExistsEvent(path, null);
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