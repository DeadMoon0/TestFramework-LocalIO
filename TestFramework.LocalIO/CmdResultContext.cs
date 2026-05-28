using TestFramework.Core.Steps;

namespace TestFramework.LocalIO;

/// <summary>
/// Step result context for command execution.
/// </summary>
/// <param name="ExitCode">The shell process exit code.</param>
/// <param name="StandardOutput">The captured standard output text.</param>
/// <param name="StandardError">The captured standard error text.</param>
/// <param name="Command">The resolved shell command that was executed.</param>
/// <param name="WorkingDirectory">The resolved working directory used for the command.</param>
public sealed record CmdResultContext(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string Command,
    string WorkingDirectory) : StepResultContext;