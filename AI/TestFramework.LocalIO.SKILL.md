<identity>
    <package>TestFramework.LocalIO</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain the local machine and file-system oriented capabilities in TestFramework.LocalIO, including command execution, file artifacts, folder discovery, and file-based waiting patterns.
</objective>

<package_scope>
    Covers local command execution, file artifacts, and file-based polling or wait scenarios.
</package_scope>

<key_concepts>
    LocalIO is for interactions on the machine that runs the tests.
    It is useful for preparation, file production, command execution, and file-based event waiting.
    It combines naturally with Core timelines and can complement other extension packages.
    The public entry points are exposed through LocalIO.Trigger, LocalIO.Events, and LocalIO.Artifacts.
    LocalIO models local files as first-class artifacts instead of treating them as ad-hoc path strings floating around the test.
</key_concepts>

<best_practices>
    Keep local side effects visible and explicit in the timeline.
    Use deterministic file paths in tests.
    Avoid hiding command behavior in large shell strings when readability would suffer.
    Prefer artifact registration when the file content is part of what the test needs to inspect later.
    Keep command execution, file waiting, and artifact inspection as separate visible concerns in the timeline whenever possible.
</best_practices>

<api_hints>
    Important APIs and shapes from the docs:
    - LocalIO.Trigger.Cmd(...)
    - LocalIO.Events.FileExists(...)
    - LocalIO.Artifacts.FileRef(...)
    - run.AddFileArtifact(...)
    - run.ArtifactStore.GetFileArtifact("name")

    Behavioral hint:
    LocalIO often works best when command execution, file registration, and file wait logic are separate visible steps in the timeline.
</api_hints>

<runtime_behavior>
    Important runtime facts:
    - Cmd(...) executes through CMD.EXE /C and returns an exit code instead of throwing on non-zero process exit.
    - FileExists(...) is a polling event with a default poll delay and relies on timeline timeout for upper bounds.
    - File artifacts have describers, references, and data objects just like other Core artifacts.
    - Folder discovery returns one or many file artifacts from the directory at runtime.
</runtime_behavior>

<style_guide>
    Keep local preparation steps obvious and deterministic.
    Prefer stable output paths scoped to the test environment.
    Keep shell commands readable; if a command string is too dense, consider extracting the setup around it instead of obscuring the timeline.
    Use artifact identifiers that communicate what the file is, not only where it is.
</style_guide>

<sample_patterns>
    Command plus artifact pattern:
    - trigger a local command
    - register the expected output file as an artifact
    - inspect the artifact after the run completes

    File wait pattern:
    - wait for LocalIO.Events.FileExists(...)
    - place timeout configuration close to the wait
    - assert on the produced file content afterward when needed
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
</anti_patterns>

<important_type_map>
    Common type map for discovery and error interpretation:
    - LocalIO: package facade for local machine triggers, events, and artifacts
    - CmdTrigger: command execution step behind LocalIO.Trigger.Cmd(...)
    - FileExistsEvent: polling event behind LocalIO.Events.FileExists(...)
    - FileArtifact / FileArtifactReference: tracked file objects used for setup and inspection
    - FileArtifactFolderFinder: runtime folder scan that returns one or more file artifacts

    Discovery heuristics for the agent:
    - If users talk about shell commands, batch execution, or exit codes, they usually mean CmdTrigger.
    - If users talk about waiting for files or polling folders, they usually mean FileExistsEvent or FileArtifactFolderFinder.
    - If users talk about inspecting produced files later in the run, treat them as artifacts rather than plain paths.
</important_type_map>

<sources>
    TestFramework-LocalIO/README.md
    TestFramework-LocalIO/TestFramework.LocalIO/README.md
    TestFramework-Showroom/TestFramework.Showroom.Basic/10_IOContracts.cs
    TestFramework-LocalIO/Documentation/Arc42.md
</sources>

<grounding_files>
    Most important files for expert grounding:
    - TestFramework-LocalIO/TestFramework.LocalIO/LocalIO.cs
    - TestFramework-LocalIO/TestFramework.LocalIO/CmdTrigger.cs
    - TestFramework-LocalIO/TestFramework.LocalIO/FileExistsEvent.cs
    - TestFramework-LocalIO/TestFramework.LocalIO/FileArtifactExtension.cs
    - TestFramework-LocalIO/TestFramework.LocalIO/FileArtifactFolderFinder.cs
    - TestFramework-LocalIO/UnitTests/TestFramework.LocalIO.Tests/LocalIOAdvancedTests.cs
    - TestFramework-Showroom/TestFramework.Showroom.Basic/10_IOContracts.cs
</grounding_files>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-LocalIO/TestFramework.LocalIO/TestFramework.LocalIO.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-LocalIO/TestFramework.LocalIO/TestFramework.LocalIO.csproj -getProperty:PackageProjectUrl
</repo_resolution>