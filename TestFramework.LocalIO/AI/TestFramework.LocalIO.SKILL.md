<identity>
    <package>TestFramework.LocalIO</package>
    <role>addon-skill</role>
</identity>

<objective>
    Explain the local machine and file-system oriented capabilities in TestFramework.LocalIO.
</objective>

<package_scope>
    Covers local command execution, file artifacts, and file-based polling or wait scenarios.
</package_scope>

<key_concepts>
    LocalIO is for interactions on the machine that runs the tests.
    It is useful for preparation, file production, command execution, and file-based event waiting.
    It combines naturally with Core timelines and can complement other extension packages.
    The public entry points are exposed through LocalIO.Trigger, LocalIO.Events, and LocalIO.Artifacts.
</key_concepts>

<best_practices>
    Keep local side effects visible and explicit in the timeline.
    Use deterministic file paths in tests.
    Avoid hiding command behavior in large shell strings when readability would suffer.
    Prefer artifact registration when the file content is part of what the test needs to inspect later.
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

<style_guide>
    Keep local preparation steps obvious and deterministic.
    Prefer stable output paths scoped to the test environment.
    Keep shell commands readable; if a command string is too dense, consider extracting the setup around it instead of obscuring the timeline.
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

<sources>
    TestFramework-LocalIO/README.md
    TestFramework-LocalIO/TestFramework.LocalIO/README.md
    TestFramework-Showroom/TestFramework.Showroom.Basic/10_IOContracts.cs
</sources>

<repo_resolution>
    Resolve repository metadata with commands when needed:
    dotnet msbuild TestFramework-LocalIO/TestFramework.LocalIO/TestFramework.LocalIO.csproj -getProperty:RepositoryUrl
    dotnet msbuild TestFramework-LocalIO/TestFramework.LocalIO/TestFramework.LocalIO.csproj -getProperty:PackageProjectUrl
</repo_resolution>