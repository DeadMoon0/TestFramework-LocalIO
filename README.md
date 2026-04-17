# TestFramework-LocalIO

## What TestFramework Is

TestFramework is a timeline-based test framework for building integration-style test workflows.
It gives you a structured execution pipeline with triggers, waits, variables, artifacts, and assertions.

This solution extends that model with local machine and file-system oriented capabilities.

## What This Solution Covers

TestFramework-LocalIO contains the local IO extension package for the ecosystem.
It is focused on interactions that happen on the machine running the test, such as command execution, file references, and file-based polling events.

## What You Can Do With It

With this solution you can:

- execute local commands as part of a timeline
- register and inspect file artifacts created during a run
- wait until files appear as part of polling-based workflows
- combine local setup steps with the same timeline engine used across the rest of the ecosystem

## Related Repositories

- [TestFramework-Core](https://github.com/DeadMoon0/TestFramework-Core) for the runtime engine this solution extends
- [TestFramework-Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) for sample workflows and first examples
- [TestFramework-Azure](https://github.com/DeadMoon0/TestFramework-Azure) if your tests mix local preparation with cloud-side validation

## Where To Start

- Read the package-level overview in [TestFramework.LocalIO/README.md](./TestFramework.LocalIO/README.md)
- Then use the Showroom repository and look for `TestFramework.Showroom.Basic/10_IOContracts.cs` as the most relevant entry example for file-oriented flows
- Pair this solution with TestFramework-Core first, then layer in other extensions only when the test workflow needs them
