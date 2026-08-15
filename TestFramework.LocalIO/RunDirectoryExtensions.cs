using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.LocalIO;

/// <summary>
/// Adds the LocalIO run directory to a timeline.
/// </summary>
public static class RunDirectoryExtensions
{
    /// <summary>
    /// Creates a unique directory for the run under the system temp directory, resolves every
    /// relative LocalIO path against it and removes it again during cleanup.
    /// </summary>
    /// <param name="builder">The timeline builder.</param>
    /// <returns>The builder modifier for the run directory step.</returns>
    public static ITimelineBuilderModifier<EmptyStepResultContext> UseRunDirectory(this ITimelineBuilder builder)
        => builder.Trigger(new RunDirectoryStep());

    /// <summary>
    /// Creates a unique directory for the run under the given root, resolves every relative
    /// LocalIO path against it and removes it again during cleanup.
    /// </summary>
    /// <param name="builder">The timeline builder.</param>
    /// <param name="root">The directory the run directory is created in.</param>
    /// <returns>The builder modifier for the run directory step.</returns>
    public static ITimelineBuilderModifier<EmptyStepResultContext> UseRunDirectory(this ITimelineBuilder builder, VariableReference<string> root)
        => builder.Trigger(new RunDirectoryStep(root));
}
