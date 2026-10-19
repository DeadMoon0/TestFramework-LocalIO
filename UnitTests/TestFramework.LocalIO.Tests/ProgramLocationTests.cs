using System;
using System.IO;

namespace TestFramework.LocalIO.Tests;

/// <summary>
/// A program a command names is found the way the command would find it: by path, or on PATH.
/// </summary>
public sealed class ProgramLocationTests : IDisposable
{
    private readonly string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "localio-location-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(this.root, recursive: true);

    [Fact]
    public void AProgramOnThePath_IsFound()
    {
        string bin = Directory.CreateDirectory(Path.Combine(this.root, "bin")).FullName;
        File.WriteAllText(Path.Combine(bin, "tool"), string.Empty);

        Assert.True(ProgramLocation.CanBeFound("tool", this.root, $"{Path.Combine(this.root, "other")}{Path.PathSeparator}{bin}"));
    }

    [Fact]
    public void AProgramOnNoDirectoryOfThePath_IsNotFound()
        => Assert.False(ProgramLocation.CanBeFound("tool", this.root, Path.Combine(this.root, "bin")));

    [Fact]
    public void WithNoPathAtAll_ABareNameIsNotFound()
        => Assert.False(ProgramLocation.CanBeFound("tool", this.root, null));

    [Fact]
    public void ARelativePath_IsFoundFromTheWorkingDirectory_NotFromThePath()
    {
        Directory.CreateDirectory(Path.Combine(this.root, "scripts"));
        File.WriteAllText(Path.Combine(this.root, "scripts", "run.sh"), string.Empty);

        Assert.True(ProgramLocation.CanBeFound(Path.Combine("scripts", "run.sh"), this.root, null));
        Assert.False(ProgramLocation.CanBeFound(Path.Combine("scripts", "missing.sh"), this.root, this.root));
    }

    [Fact]
    public void AnAbsolutePath_IsFoundWhereItPoints()
    {
        string program = Path.Combine(this.root, "absolute-tool");
        File.WriteAllText(program, string.Empty);

        Assert.True(ProgramLocation.CanBeFound(program, Path.GetTempPath(), null));
    }
}
