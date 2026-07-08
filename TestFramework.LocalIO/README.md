# TestFramework.LocalIO

`TestFramework.LocalIO` is an extension package for `TestFramework.Core`.

It adds local-machine capabilities such as command execution, file artifacts, and file-based polling events.

The public entry points are exposed through `LocalIOExt.Trigger`, `LocalIOExt.Events`, and `LocalIOExt.Artifacts`.

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
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Const($"echo hello > {outputFileName}")))
    .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(outputPath))).WithTimeOut(TimeSpan.FromSeconds(10))
    .RegisterArtifact("outFile", LocalIOExt.Artifacts.FileRef(Var.Const(outputPath)))
    .Build();

TimelineRun run = await timeline.SetupRun().RunAsync();

run.EnsureRanToCompletion();
string content = run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String;
```

Use the two-argument `LocalIOExt.Trigger.Cmd(command, workingDirectory)` overload when the command should execute inside an isolated temp folder rather than the current process directory.

Scheduling is phase-driven. Local command triggers run in the default `Act` phase, file polling events run in `Observe`, and artifact registration runs in `Materialize`, so the common `Trigger -> WaitForEvent -> RegisterArtifact` flow stays in authored order without extra modifiers. Keep `.DoNotParallelize()` for the rarer cases where a step should still act as an explicit barrier inside its phase.

## Wait Until File Exists

```csharp
using System;
using System.IO;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;

Timeline timeline = Timeline.Create()
    .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(Path.Combine(Environment.CurrentDirectory, "out.txt"))))
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

- `LocalIOExt.Trigger.Cmd(...)` to execute a shell command and return its exit code
- `LocalIOExt.Events.FileExists(...)` to wait until a file appears
- `AddFileArtifact(...)` and `GetFileArtifact(...)` to inject and inspect file artifacts during a run

## Command Output Assertions

Use the command result bindings when you want to assert stdout or stderr directly without redirecting output through temporary files.

```csharp
Timeline timeline = Timeline.Create()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
    .GetStandardOutput("commandStdOut")
    .GetStandardError("commandStdErr")
    .Build();

TimelineRun run = await timeline.SetupRun()
    .AddVariable("cmdCommand", "echo hello&&echo warning 1>&2")
    .RunAsync();

run.EnsureRanToCompletion();

string stdOut = run.VariableStore.GetRequiredVariable<string>("commandStdOut");
string stdErr = run.VariableStore.GetRequiredVariable<string>("commandStdErr");
```

This is the preferred consumer path when the command output itself is the evidence you want to assert.
Use file artifacts only when the system under test already communicates through files.

## Command Behavior (cross-platform)

- `LocalIOExt.Trigger.Cmd(...)` executes a shell command and returns the external process exit code as the step result.
- On Windows this uses `CMD.EXE /C <cmd>`.
- On Unix-like systems it prefers `/bin/bash -c <cmd>` if `bash` is available, otherwise `/bin/sh -c <cmd>` is used as a fallback.
- Treat this as shell-compatible behavior rather than shell-identical behavior: quoting, built-in commands, and environment expansion can still differ between Windows and Unix-like systems.
- The trigger returns the external process exit code as its step result. Non-zero exit codes are not rewritten; assert on them explicitly when failure is expected.
- For long-running commands, prefer timeline timeouts such as `.WithTimeOut(...)` so the run cancels the command instead of hanging indefinitely.
- When the command writes files that later steps consume, prefer a dedicated working directory so the file polling event and file artifact reference both point at a predictable location.

## Platform Support Contract

Treat LocalIO as cross-platform shell-compatible, not shell-identical.

| Host | Support status | Notes |
|---|---|---|
| Windows | supported | uses `CMD.EXE /C ...` |
| Linux | supported | prefers `/bin/bash -c ...`, falls back to `/bin/sh -c ...` |
| macOS | supported when a compatible shell is available | same Unix-like behavior and quoting caveats as Linux |

Practical contract:

- keep commands deterministic and avoid shell-specific quoting tricks unless the test is intentionally platform-specific
- if a scenario is Windows-only, say so in the test and keep that limitation explicit
- there is not yet a full cross-platform CI matrix for LocalIO, so treat the documented behavior as the support contract and validate critical shell scripts on the target host OS

## Lifecycle And Cleanup

LocalIO file artifacts participate in the normal TestFramework artifact lifecycle, but cleanup ownership must stay explicit.

- `AddFileArtifact(...)` and `RegisterArtifact(...)` track file content and make it assertable through the artifact store.
- the framework does not assume it owns every path you point at on disk.
- file cleanup is safest when the test owns an isolated temp directory for the scenario.
- `FileArtifactReference.RemoveParentDirectoryIfEmpty()` is the explicit opt-in when the framework-created file should also clean up an otherwise empty parent directory.

Recommended pattern:

1. create a per-test temp folder
2. run commands inside that folder with `Cmd(command, workingDirectory)`
3. register or discover only files inside that owned folder
4. keep any broader machine path ownership out of the timeline unless the test intentionally targets it

## Failure Handling

- Missing files in `FileExists(...)` keep the event polling until the event-level or timeline-level timeout is reached.
- Command startup failures surface as exceptions from the trigger execution.
- When a timeline timeout cancels a running command, the resulting run failure currently surfaces through the canceled command step rather than as a distinct command-specific exit code.
- Service or test code should treat file-artifact finder ordering as filesystem-dependent. If order matters, sort resolved paths explicitly after collection.

## Deterministic Multi-File Assertions

If a scenario resolves multiple files, do not depend on directory enumeration order as part of the business assertion.

Normalize first, then assert:

```csharp
IReadOnlyList<string> orderedPaths = run.ArtifactStore
    .GetFileArtifacts("exports")
    .Select(x => x.Last.Path)
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToList();
```

Use sorting, stable naming, or explicit projection keys before asserting counts or content order.

## Additional Guidance

- `FileArtifactFolderFinder.FindMultiAsync(...)` returns files in the order reported by `Directory.GetFiles(...)`; do not depend on that order as a stable business rule.
- See the showroom/basic examples in this repository when you want a larger end-to-end usage pattern around file artifacts and timeline runs.

## Target Framework

- .NET 8 (`net8.0`)
