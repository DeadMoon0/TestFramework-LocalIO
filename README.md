![Icon](https://raw.githubusercontent.com/DeadMoon0/TestFramework-Common/96ef4240c1e55ba95a20b99285219a61407c6355/Assets/Icon.svg)
[![NuGet Version](https://img.shields.io/nuget/v/TestFramework.LocalIO?label=nuget%20TestFramework.LocalIO)](https://www.nuget.org/packages/TestFramework.LocalIO)

# TestFramework-LocalIO

TestFramework is a timeline-based framework for integration-style tests.
This solution extends that model with local-machine and file-system oriented capabilities.

## Packages

- `TestFramework.LocalIO`: local command execution, file artifacts, and file-based polling events

## Install Via NuGet

Install the package into your test project:

```bash
dotnet add package TestFramework.LocalIO
```

## Quickstart

`UseRunDirectory()` gives the run its own directory, resolves every relative LocalIO path inside it,
and removes it again during cleanup - so the sample leaves nothing behind.

```csharp
using System;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.LocalIO;
using Xunit;

public class LocalIoSample
{
	[Fact]
	public async Task CanExecuteCommandAndWaitForFile()
	{
		Timeline timeline = Timeline.Create()
			.UseRunDirectory()
			.Trigger(LocalIOExt.Trigger.Cmd(Var.Const("echo hello > out.txt")))
			.WaitForEvent(LocalIOExt.Events.FileExists(Var.Const("out.txt")))
			.WithTimeOut(TimeSpan.FromSeconds(10))
			.Build();

		TimelineRun run = await timeline.SetupRun().RunAsync();

		run.EnsureRanToCompletion();
	}
}
```

## What This Solution Covers

This repository contains the local IO extension package for the ecosystem.
It focuses on interactions that happen on the machine running the test, such as command execution, file references, and file-based polling events.

## What You Can Do With It

With this solution you can:

- execute local commands as part of a timeline
- register and inspect file artifacts created during a run
- wait until files appear as part of polling-based workflows
- combine local setup steps with the same timeline engine used across the rest of the ecosystem

## Documentation Map

- Architecture overview: [Documentation/Arc42.md](./Documentation/Arc42.md)
- Package guide: [TestFramework.LocalIO/README.md](./TestFramework.LocalIO/README.md)

## Related Repositories

- [TestFramework-Core](https://github.com/DeadMoon0/TestFramework-Core) for the runtime engine this solution extends
- [TestFramework-Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) for sample workflows and first examples
- [TestFramework-Azure](https://github.com/DeadMoon0/TestFramework-Azure) if your tests mix local preparation with cloud-side validation

## Where To Start

- Read the package-level overview in [TestFramework.LocalIO/README.md](./TestFramework.LocalIO/README.md)
- Use that package README as the support-contract source of truth for host-platform behavior and cleanup ownership
- Then use the Showroom repository and look for `TestFramework.Showroom.Basic/10_IOContracts.cs` as the most relevant entry example for file-oriented flows
- Pair this solution with TestFramework-Core first, then layer in other extensions only when the test workflow needs them

## CI Pull Requests

- Pull requests run unit tests through the GitHub Actions workflow `unit-tests`.
- If branch protection requires status checks, `unit-tests` must pass before merge.

Local pre-PR test command:

```bash
dotnet test UnitTests/TestFramework.LocalIO.Tests/TestFramework.LocalIO.Tests.csproj --configuration Release
```
