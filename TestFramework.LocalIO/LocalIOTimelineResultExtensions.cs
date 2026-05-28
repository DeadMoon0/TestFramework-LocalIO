using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Typed result-binding helpers for LocalIO timeline steps.
/// </summary>
public static class LocalIOTimelineResultExtensions
{
    /// <summary>
    /// Binds the full command result context into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetCommandResult(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x, identifier);

    /// <summary>
    /// Binds the command exit code into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetExitCode(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ExitCode, identifier);

    /// <summary>
    /// Binds the captured standard output into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetStandardOutput(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StandardOutput, identifier);

    /// <summary>
    /// Binds the captured standard error into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetStandardError(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.StandardError, identifier);

    /// <summary>
    /// Binds the resolved command text into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetCommand(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.Command, identifier);

    /// <summary>
    /// Binds the resolved working directory into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetWorkingDirectory(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.WorkingDirectory, identifier);
}