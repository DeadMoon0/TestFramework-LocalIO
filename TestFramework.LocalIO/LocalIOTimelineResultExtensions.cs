using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Typed result-binding helpers for LocalIO timeline steps.
/// </summary>
public static class LocalIOTimelineResultExtensions
{
    /// <summary>
    /// Binds the command exit code into a variable.
    /// </summary>
    public static ITimelineBuilderModifier<CmdResultContext> GetExitCode(this ITimelineBuilderModifier<CmdResultContext> builder, VariableIdentifier identifier)
        => builder.BindResultProperty(x => x.ExitCode, identifier);
}