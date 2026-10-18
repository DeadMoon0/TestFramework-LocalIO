using TestFramework.Core.Steps;

namespace TestFramework.LocalIO;

/// <summary>
/// Step result context for command execution.
/// </summary>
/// <remarks>
/// Built only by <see cref="CmdTrigger"/>, so its constructor is internal and its members are properties: a
/// command's result is the record most likely to learn something new - how long it ran, whether it was cut off -
/// and a positional record would have broken every compiled reader the day it did.
/// </remarks>
public sealed record CmdResultContext : StepResultContext
{
    internal CmdResultContext(int exitCode, string standardOutput, string standardError, string command, string workingDirectory)
    {
        this.ExitCode = exitCode;
        this.StandardOutput = standardOutput;
        this.StandardError = standardError;
        this.Command = command;
        this.WorkingDirectory = workingDirectory;
    }

    /// <summary>
    /// The exit code of the process that was started: the shell for <c>Cmd</c>, the program itself for <c>Exec</c>.
    /// </summary>
    public int ExitCode { get; internal init; }

    /// <summary>
    /// The captured standard output text.
    /// </summary>
    public string StandardOutput { get; internal init; }

    /// <summary>
    /// The captured standard error text.
    /// </summary>
    public string StandardError { get; internal init; }

    /// <summary>
    /// The command line as it ran: the shell line for <c>Cmd</c>, the program and its arguments for <c>Exec</c>.
    /// </summary>
    public string Command { get; internal init; }

    /// <summary>
    /// The resolved working directory used for the command.
    /// </summary>
    public string WorkingDirectory { get; internal init; }
}
