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
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;

Timeline timeline = Timeline.Create()
    .UseRunDirectory()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello > out.txt")))
    .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const("out.txt"))).WithTimeOut(TimeSpan.FromSeconds(10))
    .RegisterArtifact("outFile", LocalIOExt.Artifacts.FileRef(Var.Const("out.txt")))
    .Build();

TimelineRun run = await timeline.SetupRun().RunAsync();

run.EnsureRanToCompletion();
string content = run.ArtifactStore.GetFileArtifact("outFile").Last.DataAsUtf8String;
```

`UseRunDirectory()` creates `tf-localio-<guid>` under the system temp directory, publishes it as the
run directory, and removes it during cleanup. Every relative LocalIO path - command working
directory, `FileExists(...)` target, artifact reference - then resolves inside it, so concurrent runs
cannot read, overwrite, or delete each other's files. Pass a root with
`UseRunDirectory(Var.Const(myRoot))` when the directory must live somewhere specific.

Without `UseRunDirectory()`, relative paths keep resolving against the process-wide
`Environment.CurrentDirectory` at run time. That is the documented legacy fallback, not a
recommendation. Use the two-argument `LocalIOExt.Trigger.Cmd(command, workingDirectory)` overload
when a single command needs a different directory than the rest of the run.

Scheduling is phase-driven. Local command triggers run in the default `Act` phase, file polling events run in `Observe`, and artifact registration runs in `Materialize`, so the common `Trigger -> WaitForEvent -> RegisterArtifact` flow stays in authored order without extra modifiers. Keep `.DoNotParallelize()` for the rarer cases where a step should still act as an explicit barrier inside its phase.

## Wait Until File Exists

```csharp
using System;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;

Timeline timeline = Timeline.Create()
    .WaitForEvent(LocalIOExt.Events.FileExists(Var.Const(outputPath)))
    .WithTimeOut(TimeSpan.FromSeconds(10))
    .Build();
```

On timeout the event reports the resolved path it watched, so a mismatched working directory is easy
to spot.

## Add Or Read File Artifacts

```csharp
using TestFramework.Core.Timelines;
using TestFramework.LocalIO;

TimelineRun run = await timeline.SetupRun().AddFileArtifact("inputFile", inputPath, "hello world").RunAsync();

string content = run.ArtifactStore.GetFileArtifact("inputFile").Last.DataAsUtf8String;
```

Run-builder artifacts are set up before the timeline itself starts, so they cannot see the run
directory - give `AddFileArtifact(...)` a fully qualified path.

## Typical Scenarios

- `LocalIOExt.Trigger.Cmd(...)` to execute a shell command and return its exit code
- `LocalIOExt.Events.FileExists(...)` to wait until a file appears
- `AddFileArtifact(...)` and `GetFileArtifact(...)` to inject and inspect file artifacts during a run

## Command Output Assertions

Use the command result bindings when you want to assert stdout or stderr directly without redirecting output through temporary files.

```csharp
Timeline timeline = Timeline.Create()
    .UseRunDirectory()
    .Trigger(LocalIOExt.Trigger.Cmd(Var.Ref<string>("cmdCommand")))
    .GetStandardOutput("commandStdOut")
    .GetStandardError("commandStdErr")
    .Build();

TimelineRun run = await timeline.SetupRun()
    .AddVariable("cmdCommand", "echo hello&&echo warning 1>&2")
    .RunAsync();

run.EnsureRanToCompletion();

string? stdOut = run.VariableStore.GetVariable<string>("commandStdOut");
string? stdErr = run.VariableStore.GetVariable<string>("commandStdErr");
```

`GetCommandResult`, `GetExitCode`, `GetCommand`, and `GetWorkingDirectory` bind the remaining parts
of `CmdResultContext` the same way.

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
- CI runs the unit tests on both `ubuntu-latest` and `windows-latest`; still validate critical shell scripts on the target host OS, because the suite cannot cover every shell nuance
- platform-specific tests carry `[Trait("Category", "WindowsOnly")]` or `[Trait("Category", "UnixOnly")]`, and CI filters them per runner

## Lifecycle And Cleanup

LocalIO file artifacts participate in the normal TestFramework artifact lifecycle, but cleanup ownership must stay explicit.

- `AddFileArtifact(...)` and `RegisterArtifact(...)` track file content and make it assertable through the artifact store.
- the framework does not assume it owns every path you point at on disk.
- file cleanup is safest when the test owns an isolated temp directory for the scenario.
- `FileArtifactReference.RemoveParentDirectoryIfEmpty()` is the explicit opt-in when the framework-created file should also clean up an otherwise empty parent directory.
- Teardown deletes every file artifact, including the ones `FileArtifactFolderFinder` discovered. Chain `MarkReadonly()` onto the `FindArtifact` / `FindArtifacts` / `RegisterArtifact` call for a file the run did not create - it is the only opt-out, and nothing downstream can overrule it.
- artifact setup creates the parent directory when it is missing, and `RemoveParentDirectoryIfEmpty()` only removes a directory that setup itself created.

Recommended pattern:

1. start the timeline with `.UseRunDirectory()`
2. keep every path in the timeline relative so it lands inside that directory
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

`FindArtifacts("exports", ...)` generates the identifiers `exports_0`, `exports_1`, ... in discovery
order. `GetFileArtifacts("exports")` returns exactly those instances, ordered by that index, and
`Reference.FilePath` is the resolved path each one was pinned to.

```csharp
IReadOnlyList<string> orderedPaths = run.ArtifactStore
    .GetFileArtifacts("exports")
    .Select(x => x.Reference.FilePath)
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToList();
```

Use sorting, stable naming, or explicit projection keys before asserting counts or content order.

## Additional Guidance

- `FileArtifactFolderFinder.FindMultiAsync(...)` returns files in the order reported by `Directory.EnumerateFiles(...)`; do not depend on that order as a stable business rule.
- See the showroom/basic examples in this repository when you want a larger end-to-end usage pattern around file artifacts and timeline runs.

## Target Frameworks

- .NET 8 (`net8.0`)
- .NET 10 (`net10.0`)
