using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.LocalIO.Tests;

public class FileArtifactDataTests
{
    [Fact]
    public void RecordedContent_DoesNotChangeWhenTheCallersArrayDoes()
    {
        // A version is evidence of what the file held; a caller reusing its buffer must not rewrite it.
        byte[] bytes = [1, 2, 3, 4];
        FileArtifactData data = new(bytes);

        bytes[0] = 99;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, data.Content.ToArray());
    }

    [Fact]
    public void Content_IsReadWithoutCopyingAgain()
    {
        FileArtifactData data = new([1, 2, 3, 4]);

        Assert.True(MemoryMarshal.TryGetArray(data.Content, out ArraySegment<byte> first));
        Assert.True(MemoryMarshal.TryGetArray(data.Content, out ArraySegment<byte> second));

        Assert.Same(first.Array, second.Array);
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
