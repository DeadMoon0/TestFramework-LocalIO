using System.Runtime.InteropServices;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class FileArtifactDataTests
{
    [Fact]
    public void Content_HandsOutTheUnderlyingBuffer_WithoutCopying()
    {
        byte[] bytes = [1, 2, 3, 4];
        FileArtifactData data = new(bytes);

        Assert.True(MemoryMarshal.TryGetArray(data.Content, out ArraySegment<byte> first));
        Assert.True(MemoryMarshal.TryGetArray(data.Content, out ArraySegment<byte> second));

        Assert.Same(bytes, first.Array);
        Assert.Same(bytes, second.Array);
    }

    [Fact]
    public async Task ContentHandle_ExposesTheArtifactBytes()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun()
            .AddFileArtifact("payload", Path.Combine(Path.GetTempPath(), $"localio-data-{Guid.NewGuid():N}.bin"), [7, 8, 9])
            .RunAsync();

        run.EnsureRanToCompletion();

        ValueHandle<ReadOnlyMemory<byte>> content = run.FileArtifact("payload").Content();
        content.Should().Match(bytes => bytes.ToArray().SequenceEqual(new byte[] { 7, 8, 9 }), "the handle exposes the artifact bytes");
        content.Select(bytes => bytes.Length).Should().Be(3);
    }
}
