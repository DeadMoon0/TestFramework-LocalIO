# TestFramework-LocalIO - arc42 Architecture Documentation

> Version: 1.0
> Date: 2026-04-20

## 1. Introduction and Goals

TestFramework-LocalIO is the local-machine extension of the TestFramework ecosystem.
It adds steps, events, artifact helpers, and finder logic for scenarios that happen on the machine running the test.

Primary goals:

- execute local shell commands as timeline steps
- model files as typed artifacts inside the shared TestFramework runtime
- wait for file-system side effects with polling-based events
- keep local IO workflows consistent with the same build-run-assert model used by Core and Azure extensions

The solution is aimed at integration-style tests that need local preparation, file exchange, or command-based orchestration.

## 2. Constraints

- Runtime target is .NET 8 (`net8.0`).
- The package extends `TestFramework.Core` and therefore follows its timeline, step, event, and artifact abstractions.
- Command execution is implemented with `CMD.EXE`, so the current trigger behavior is Windows-oriented.
- The package is distributed as a NuGet package with README/icon metadata in the project file.
- CI currently validates behavior through unit tests only; there is no dedicated E2E pipeline for local machine scenarios.

## 3. System Scope and Context

Inside the ecosystem, TestFramework-LocalIO sits between the generic runtime in Core and concrete test timelines written by consumers.

Relevant collaborators:

- `TestFramework.Core`: provides timeline execution, stores, assertions, and step/event base classes
- test authors: compose local steps and events through `LocalIO.Trigger`, `LocalIO.Events`, and `LocalIO.Artifacts`
- local operating system: executes commands, exposes file system state, and stores file contents
- showroom/examples: demonstrate how LocalIO concepts fit into broader timeline flows

The scope is intentionally narrow: local command execution, file existence polling, file artifact references, and folder-based artifact discovery.

## 4. Solution Strategy

The solution keeps the public surface area small and fluent.

Key strategy decisions:

- expose a facade (`LocalIO`) instead of asking consumers to instantiate concrete classes directly
- reuse Core abstractions (`Step<T>`, `SequentialEvent`, `ArtifactFinder`, typed artifact triples) rather than adding a parallel runtime
- treat files as first-class artifacts so setup, lookup, versioning, and assertions behave like in other extensions
- keep advanced convenience operations as extensions on the run builder and artifact store
- validate behavior through focused unit tests around command execution, event declaration, artifact discovery, and helper extensions

## 5. Building Block View

Main building blocks:

- `LocalIO`: public facade exposing `Trigger`, `Events`, and `Artifacts`
- `CmdTrigger`: executes a shell command and returns the exit code as the step result
- `FileExistsEvent`: polling event that completes when a path exists and optionally uses a caller-provided poll delay
- `FileArtifactReference`, `FileArtifactData`, `FileArtifactDescriber`, `FileArtifactKind`: typed file artifact model
- `FileArtifactFolderFinder`: resolves one or many file artifacts from a directory at runtime
- `FileArtifactExtension`: convenience methods for adding and retrieving file artifacts in typed form
- unit tests: verify API behavior, IO-contract declaration, helper encoding behavior, and happy-path runtime behavior

This yields a small package with one public vertical slice for triggers/events and one for file artifacts.

## 6. Runtime View

Typical runtime scenarios:

1. Command execution
	 A timeline triggers `LocalIO.Trigger.Cmd(...)`. During execution the step launches `CMD.EXE /C ...`, streams stdout/stderr into the framework logger, and returns the process exit code.

2. File-based synchronization
	 A timeline waits for `LocalIO.Events.FileExists(...)`. The event polls until the file appears or the timeline timeout is reached.

3. File artifact registration and lookup
	 A run can add an input file through `AddFileArtifact(...)`, or the timeline can discover one or many files through `FileArtifactFolderFinder`. Once registered, file artifacts flow through the shared artifact store and can be asserted like any other artifact.

The runtime deliberately delegates orchestration concerns such as retries, timeouts, logging, and assertion behavior to `TestFramework.Core`.

## 7. Deployment View

TestFramework-LocalIO is a class library package, not a standalone process.

Deployment/build shape:

- one NuGet package: `TestFramework.LocalIO`
- consumed by test projects through `PackageReference`
- executed inside the host test runner process
- depends on the local machine environment for command shell and filesystem state

There is no service deployment unit. Operational deployment concerns are limited to package publishing and test-host machine compatibility.

## 8. Cross-Cutting Concepts

- Public facade pattern: `LocalIO` centralizes consumer entry points
- typed artifact model: files integrate with the Core artifact lifecycle instead of being handled as raw strings/bytes everywhere
- IO-contract declaration: steps and events declare required inputs, which lets the Core validator reject incomplete runs before execution
- logging: external stdout/stderr is surfaced through the shared logger for traceability
- runtime variables: command text, working directories, paths, and poll delays can be injected per run through `Var.Ref<T>`

## 9. Architecture Decisions

- Use `CMD.EXE` as the default execution mechanism for command steps.
	Rationale: simple integration with the local Windows test environment.

- Model file interactions as artifacts instead of ad-hoc helper methods only.
	Rationale: keeps LocalIO aligned with the ecosystem's cleanup/versioning/assertion semantics.

- Use `SequentialEvent` for file existence waiting.
	Rationale: file polling is naturally sequential and does not require a separate async listener infrastructure.

- Keep the package small and focused.
	Rationale: LocalIO complements Core rather than competing with it; higher-level orchestration belongs in timelines or companion repos.

## 10. Quality Requirements

- Predictability: command steps should honor configured working directories and return deterministic exit codes
- Traceability: external process output should be visible in framework logs
- Type safety: artifact helpers and typed retrieval should reduce casting and misuse
- Early validation: missing variable inputs should be detectable through declared IO contracts
- Ease of use: common scenarios should be reachable through the `LocalIO` facade and run-builder extension methods

## 11. Risks and Technical Debt

- Platform coupling: `CmdTrigger` is Windows-specific because it shells through `CMD.EXE`
- Documentation lag: package README still contains older package/namespace forms and should be aligned with the current public API
- Narrow event coverage: only file existence polling is currently modeled; richer file-system event support is not yet present
- Environment sensitivity: local tests can become brittle if they rely on machine-specific paths, shell state, or permissions
- Limited automated coverage breadth: unit tests exist, but there is no broader multi-platform or full sample-suite validation pipeline

## 12. Glossary

- Timeline: ordered test workflow built with the TestFramework fluent API
- Step: executable unit inside a timeline
- Event: step variant that waits for an external condition
- Artifact: typed representation of an external resource tracked during the run
- Artifact Finder: runtime component that discovers artifacts from an environment query
- IO Contract: metadata describing which variables/artifacts a step requires or produces
