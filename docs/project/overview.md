---
title: Project Overview
category: Project
categoryindex: 2
index: 1
---

# Project Overview

ARC Data Model is the workspace for the unified ARC RDM data model. It contains the markdown specification, derived schemas, example documents, reference material from upstream ARC/RO-Crate work, and F# implementation projects for ProcessCore and the SQL profile.

## Repository Shape

```text
ARC-Data-Model/
|-- docs/                 fsdocs documentation pages
|   `-- spec/             base profiles and domain-specific decoration profiles
|-- schemas/              derived SQL and YAML schema representations
|-- examples/             concrete YAML examples
|-- references/           upstream and prior implementation reference material
|-- src/                  F# libraries
|-- tests/                Pyxpecto test project
`-- build/                FAKE build targets
```

## Main Areas

- [Specification](specification.md) describes the three ARC RDM base profiles and their decoration profiles.
- [Examples and schemas](examples-and-schemas.md) explains the current schema drafts and example status.
- [Implementation](implementation.md) explains the F# projects, SQL profile, runtime adapters, and build commands.
- [Reference material](references.md) lists upstream profiles and preserved implementation notes.
- [Prior art notes](prior-art.md) summarizes the ARCtrl and query model background that shaped this repo.

## Documentation

Documentation is built with fsdocs from the `docs/` directory. Additional template content, including Mermaid support, lives in `docs/_head.html`.

Common commands:

```powershell
dotnet fsdocs watch
.\build.cmd BuildDocs
.\build.cmd WatchDocs
```

## Build And Test

The FAKE build project is invoked through the root wrapper scripts:

```powershell
.\build.cmd BuildSolution
.\build.cmd RunTests
```

`RunTests` runs the configured shared suite, including the .NET, JavaScript, and Python paths.

