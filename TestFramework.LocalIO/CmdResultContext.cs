using TestFramework.Core.Steps;

namespace TestFramework.LocalIO;

/// <summary>
/// Step result context for command execution.
/// </summary>
/// <param name="ExitCode">The shell process exit code.</param>
public sealed record CmdResultContext(int ExitCode) : StepResultContext;