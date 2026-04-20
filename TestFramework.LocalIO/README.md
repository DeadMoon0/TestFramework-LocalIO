# TestFramework.LocalIO

`TestFramework.LocalIO` is an extension package for `TestFramework.Core`.

It adds local-machine capabilities such as command execution, file artifacts, and file-based polling events.

The public entry points are exposed through `LocalIO.Trigger`, `LocalIO.Events`, and `LocalIO.Artifacts`.

## Install

```bash
dotnet add package TestFramework.LocalIO
```

## Quick Start

```csharp
using System;
using System.IO;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;

string outputPath = Path.Combine(Environment.CurrentDirectory, "out.txt");

Timeline timeline = Timeline.Create()
    .Trigger(LocalIO.Trigger.Cmd(Var.Const("echo hello > out.txt")))
    .RegisterArtifact("outFile", LocalIO.Artifacts.FileRef(Var.Const(outputPath)))
    .Build();

TimelineRun run = await timeline.SetupRun()
    .RunAsync();

run.EnsureRanToCompletion();
string content = run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String;
```

## Wait Until File Exists

```csharp
using System;
using System.IO;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;

Timeline timeline = Timeline.Create()
    .WaitForEvent(LocalIO.Events.FileExists(Var.Const(Path.Combine(Environment.CurrentDirectory, "out.txt"))))
    .WithTimeOut(TimeSpan.FromSeconds(10))
    .Build();
```

## Add Or Read File Artifacts

```csharp
using TestFramework.Core.Timelines;
using TestFramework.LocalIO;

TimelineRun run = await timeline.SetupRun()
    .AddFileArtifact("inputFile", "input.txt", "hello world")
    .RunAsync();

string content = run.ArtifactStore.GetFileArtifact("inputFile").Last.DataAsUtf8String;
```

## Typical Scenarios

- `LocalIO.Trigger.Cmd(...)` to execute a shell command and return its exit code
- `LocalIO.Events.FileExists(...)` to wait until a file appears
- `AddFileArtifact(...)` and `GetFileArtifact(...)` to inject and inspect file artifacts during a run

## Target Framework

- .NET 8 (`net8.0`)
