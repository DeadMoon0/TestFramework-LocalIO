<identity>
    <package>TestFramework.LocalIO</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain the local machine and file-system oriented capabilities in TestFramework.LocalIO, including command execution, captured command output, run-scoped paths, file artifacts, folder discovery, and file-based waiting patterns.
</objective>

<package_scope>
    Covers local command execution, command output capture, file artifacts, and file-based polling or wait scenarios.
</package_scope>

<key_concepts>
    LocalIO is for interactions on the machine that runs the tests.
    It is useful for preparation, file production, command execution, and file-based event waiting.
    It combines naturally with Core timelines and can complement other extension packages.
    The public entry points are exposed through LocalIOExt.Trigger, LocalIOExt.Events, and LocalIOExt.Artifacts.
    LocalIO models local files as first-class artifacts instead of treating them as ad-hoc path strings floating around the test.
    UseRunDirectory() gives the run its own directory and makes every relative LocalIO path resolve inside it, so concurrent runs cannot collide.
</key_concepts>

<best_practices>
    Keep local side effects visible and explicit in the timeline.
    Start file-producing timelines with UseRunDirectory() and then use plain relative paths, instead of hand-rolling temp folders and absolute paths.
    Avoid hiding command behavior in large shell strings when readability would suffer.
    Prefer the command result bindings (GetStandardOutput, GetExitCode, ...) when the command output itself is the evidence; use file artifacts when the system under test genuinely communicates through files.
    Prefer artifact registration when the file content is part of what the test needs to inspect later.
    Keep command execution, file waiting, and artifact inspection as separate visible concerns in the timeline whenever possible.
    Mark artifacts the run did not create with MarkReadonly() on the declaring call so cleanup does not delete them.
    Prefer compact shapes such as `Trigger(LocalIOExt.Trigger.Cmd(...))` and `WaitForEvent(LocalIOExt.Events.FileExists(...))` when the command or path still reads clearly.
</best_practices>

<api_hints>
    Important APIs and shapes from the docs:
    - timeline.UseRunDirectory() and UseRunDirectory(root)
    - LocalIOExt.Trigger.Cmd(command) and Cmd(command, workingDirectory)
    - LocalIOExt.Events.FileExists(path) and FileExists(path, pollDelay)
    - LocalIOExt.Artifacts.FileRef(path), plus .RemoveParentDirectoryIfEmpty()
    - MarkReadonly() on the FindArtifact/FindArtifacts/RegisterArtifact call is the protection to reach for
    - result bindings on a Cmd step: GetCommandResult, GetExitCode, GetStandardOutput, GetStandardError, GetCommand, GetWorkingDirectory
    - run.AddFileArtifact(...)
    - run.ArtifactStore.GetFileArtifact("name") and run.ArtifactStore.GetFileArtifacts("baseName")
    - FileArtifactData.Content (non-copying) and the Content()/Bytes()/Utf8Text() handle extensions

    Behavioral hint:
    LocalIO often works best when command execution, file registration, and file wait logic are separate visible steps in the timeline.
</api_hints>

<runtime_behavior>
    Important runtime facts:
    - Cmd(...) runs through CMD.EXE /C on Windows and through /bin/bash -c (falling back to /bin/sh -c) on Unix-like hosts, and returns an exit code instead of throwing on non-zero process exit.
    - Cmd(...) captures stdout and stderr into CmdResultContext; the six binding verbs project them into timeline variables.
    - Both output pipes are drained concurrently, so commands that produce more than a pipe buffer of output do not deadlock.
    - A cancelled or timed-out command has its whole process tree killed, so it cannot keep writing files after the run ended.
    - Without an explicit working directory a command runs in the run directory when the timeline declared one, otherwise in the process working directory as it is at run time.
    - FileExists(...) is a polling event with a default poll delay of 500 ms; on timeout it reports the resolved path it was watching.
    - File artifacts have describers, references, and data objects just like other Core artifacts.
    - Artifact setup creates the parent directory when it is missing, and cleanup only removes a parent directory that setup created.
    - Folder discovery returns one or many file artifacts from the directory at runtime. They are deleted at teardown like any other artifact, so add MarkReadonly() when the run only reads the folder.
</runtime_behavior>

<documentation_notes>
    Guidance the agent should preserve:
    - LocalIO publishes a three-platform support contract: Windows, Linux, and macOS where a compatible shell is available. Treat it as shell-compatible, not shell-identical - quoting, built-ins, and environment expansion still differ.
    - stdout/stderr capture is an existing, first-class capability, and the README names the result bindings the preferred consumer path.
    - README guidance covers the platform contract, failure handling, lifecycle ownership, and artifact-finder semantics.

    Practical recommendation:
    - when a scenario really is platform-specific, say so in the test and keep the limitation explicit rather than implying LocalIO itself is single-platform
</documentation_notes>

<style_guide>
    Keep local preparation steps obvious and deterministic.
    Prefer stable output paths scoped to the test environment.
    Keep shell commands readable; if a command string is too dense, consider extracting the setup around it instead of obscuring the timeline.
    Use artifact identifiers that communicate what the file is, not only where it is.
    Prefer ordinary C# constants or locals for repeated path fragments, and keep `Var.Const(...)` inline when a one-off value does not deserve a separate timeline variable.
</style_guide>

<sample_patterns>
    Command plus artifact pattern:
    - trigger a local command
    - register the expected output file as an artifact
    - inspect the artifact after the run completes

    File wait pattern:
    - wait for LocalIOExt.Events.FileExists(...)
    - place timeout configuration close to the wait
    - assert on the produced file content afterward when needed

    Run-scoped pattern:
    - start with UseRunDirectory()
    - keep every path in the timeline relative
    - let cleanup remove the directory instead of writing teardown code
</sample_patterns>

<decision_rules>
    Recommend LocalIO when:
    - the scenario interacts with the local file system
    - a command-line step prepares or verifies test data
    - file appearance is the observable event boundary

    Recommend additional packages only when the test leaves the local-machine boundary and interacts with remote systems.
</decision_rules>

<anti_patterns>
    Avoid:
    - assuming non-zero exit codes automatically fail the test
    - relying on unstable or machine-specific paths
    - burying complex shell behavior in unreadable command strings
    - waiting for files without an explicit timeout nearby
    - treating a path string as the same thing as a tracked artifact when the content matters later
    - declaring a pre-existing file without MarkReadonly(), which licenses cleanup to delete it
    - reading FileArtifactData.Data in a loop; it copies the whole file on every access, use Content
</anti_patterns>

<important_type_map>
    Common type map for discovery and error interpretation:
    - LocalIOExt: package facade for local machine triggers, events, and artifacts
    - CmdTrigger: command execution step behind LocalIOExt.Trigger.Cmd(...)
    - CmdResultContext: exit code, stdout, stderr, command, and working directory of a finished command
    - FileExistsEvent: polling event behind LocalIOExt.Events.FileExists(...)
    - RunDirectoryStep: the Prepare-phase step behind UseRunDirectory() that also cleans the directory up
    - FileArtifactReference / FileArtifactData / FileArtifactDescriber: tracked file objects used for setup and inspection
    - FileArtifactFolderFinder: runtime folder scan that returns one or more file artifacts

    Discovery heuristics for the agent:
    - If users talk about shell commands, batch execution, or exit codes, they usually mean CmdTrigger.
    - If users talk about waiting for files or polling folders, they usually mean FileExistsEvent or FileArtifactFolderFinder.
    - If users talk about inspecting produced files later in the run, treat them as artifacts rather than plain paths.
    - If users talk about tests stepping on each other's files, they usually want UseRunDirectory().
</important_type_map>

<sources>
    README.md
    TestFramework.LocalIO/README.md
    Documentation/Arc42.md
</sources>

<grounding_files>
    Most important files for expert grounding, relative to the repository root:
    - TestFramework.LocalIO/LocalIOExt.cs
    - TestFramework.LocalIO/CmdTrigger.cs
    - TestFramework.LocalIO/CmdResultContext.cs
    - TestFramework.LocalIO/LocalIOTimelineResultExtensions.cs
    - TestFramework.LocalIO/FileExistsEvent.cs
    - TestFramework.LocalIO/RunDirectoryStep.cs
    - TestFramework.LocalIO/RunDirectoryExtensions.cs
    - TestFramework.LocalIO/LocalPath.cs
    - TestFramework.LocalIO/FileArtifactExtension.cs
    - TestFramework.LocalIO/FileArtifactFolderFinder.cs
    - TestFramework.LocalIO/Artifacts/FileArtifactReference.cs
    - TestFramework.LocalIO/Artifacts/FileArtifactData.cs
    - TestFramework.LocalIO/Artifacts/FileArtifactDescriber.cs
    - UnitTests/TestFramework.LocalIO.Tests/LocalIOAdvancedTests.cs
    - UnitTests/TestFramework.LocalIO.Tests/RunDirectoryTests.cs
</grounding_files>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework.LocalIO/TestFramework.LocalIO.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework.LocalIO/TestFramework.LocalIO.csproj -getProperty:PackageProjectUrl
</repo_resolution>
