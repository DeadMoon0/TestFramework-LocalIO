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

const string outputFileName = "out.txt";
string outputPath = Path.Combine(Environment.CurrentDirectory, outputFileName);

Timeline timeline = Timeline.Create()
    .Trigger(LocalIO.Trigger.Cmd(Var.Const($"echo hello > {outputFileName}")))
    .WaitForEvent(LocalIO.Events.FileExists(Var.Const(outputPath))).WithTimeOut(TimeSpan.FromSeconds(10))
    .RegisterArtifact("outFile", LocalIO.Artifacts.FileRef(Var.Const(outputPath)))
    .Build();

TimelineRun run = await timeline.SetupRun().RunAsync();

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

TimelineRun run = await timeline.SetupRun().AddFileArtifact("inputFile", "input.txt", "hello world").RunAsync();

string content = run.ArtifactStore.GetFileArtifact("inputFile").Last.DataAsUtf8String;
```

## Typical Scenarios

- `LocalIO.Trigger.Cmd(...)` to execute a shell command and return its exit code
- `LocalIO.Events.FileExists(...)` to wait until a file appears
- `AddFileArtifact(...)` and `GetFileArtifact(...)` to inject and inspect file artifacts during a run

## Windows Command Behavior

- `LocalIO.Trigger.Cmd(...)` uses `CMD.EXE /C` and is therefore supported on Windows only.
- The trigger returns the external process exit code as its step result. Non-zero exit codes are not rewritten; assert on them explicitly when failure is expected.
- For long-running commands, prefer timeline timeouts such as `.WithTimeOut(...)` so the run cancels the command instead of hanging indefinitely.

## Failure Handling

- Missing files in `FileExists(...)` keep the event polling until the event-level or timeline-level timeout is reached.
- Command startup failures surface as exceptions from the trigger execution.
- When a timeline timeout cancels a running command, the resulting run failure currently surfaces through the canceled command step rather than as a distinct command-specific exit code.
- Service or test code should treat file-artifact finder ordering as filesystem-dependent. If order matters, sort resolved paths explicitly after collection.

## Additional Guidance

- `FileArtifactFolderFinder.FindMultiAsync(...)` returns files in the order reported by `Directory.GetFiles(...)`; do not depend on that order as a stable business rule.
- See the showroom/basic examples in this repository when you want a larger end-to-end usage pattern around file artifacts and timeline runs.

## Target Framework

- .NET 8 (`net8.0`)
