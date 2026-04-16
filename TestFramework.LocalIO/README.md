# TestFrameworkLocalIO

## Introduction

TestFrameworkLocalIO is an extension package for TestFrameworkCore.

If you are new: TestFrameworkCore is the runtime engine that executes your timeline.
This package adds local machine capabilities such as command execution and file-based artifacts/events.

Local machine IO helpers for timeline tests.

TestFrameworkLocalIO adds:
- command execution trigger
- file artifact references
- file-exists polling event

## Install

```bash
dotnet add package TestFrameworkLocalIO
```

## Quick Start

```csharp
using TestFrameworkCore.Timelines;
using TestFrameworkCore.Variables;
using TestFrameworkLocalIO;

Timeline timeline = Timeline.Create()
    .Trigger(LocalIO.Trigger.Cmd("echo hello > out.txt", Environment.CurrentDirectory))
    .RegisterArtifact("outFile", LocalIO.Artifacts.FileRef(Var.Const("./out.txt")))
    .Build();

TimelineRun run = await timeline.SetupRun()
    .RunAsync();

run.EnsureRanToCompletion();
```

## Wait Until File Exists

```csharp
Timeline timeline = Timeline.Create()
    .WaitForEvent(LocalIO.Events.FileExists(Var.Const("./out.txt")))
    .WithTimeOut(TimeSpan.FromSeconds(10))
    .Build();
```

## Add Or Read File Artifacts

```csharp
using TestFrameworkLocalIO;

TimelineRun run = await timeline.SetupRun()
    .AddFileArtifact("inputFile", "input.txt", "hello world")
    .RunAsync();

string content = run.ArtifactStore.GetFileArtifact("inputFile").First.DataAsUtf8String;
```

## Target Framework

- .NET 8 (`net8.0`)
