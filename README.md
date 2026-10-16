# ARC Core

`ProcessCore` is the F# implementation package for the ARC Core data model. It provides a mutable in-memory model for process graphs, datamap entries, administrative metadata, YAML codecs, SQLite profile helpers, graph traversal queries, and live table views over process data.

Documentation: https://hlweil.github.io/ProcessCore/


## Repository Docs

- [Project overview](docs/project/overview.md)
- [Normative specification](docs/spec/index.md)
- [Specification guide](docs/project/specification.md)
- [Implementation guide](docs/project/implementation.md)
- [ARCManagement guide](docs/project/arc-management.md)
- [ARCtrl workspace guide](docs/project/arctrl.md)
- [Examples and schemas](docs/project/examples-and-schemas.md)
- [Reference material](docs/project/references.md)
- [Prior art notes](docs/project/prior-art.md)

## Install

```powershell
dotnet add package ProcessCore
```

```fsharp
#r "nuget: ProcessCore"
```

## What You Get

- Unified ARC RDM types: `Dataset`, `Process`, `Recipe`, `Sample`, `Data`, `Annotation`, `FormalParameter`, `DefinedTerm`, `Agent`, `Organization`, and `ScholarlyArticle`.
- Query helpers for connected, upstream, downstream, and path-based traversal.
- Fragment-aware `Data` nodes with `path`, `selector`, `selectorFormat`, nested fragments, `DataContext` descriptors, and pluggable selector providers.
- YAML encode/decode support for ARC Core, datamap, and administrative content, including strict and lenient type handling.
- SQLite profile row types, codecs, repository CRUD facades, and .NET SQLite driver support.
- A live tabular projection layer for reading and editing process graphs as tables.
- Fable-oriented project variants for JavaScript and Python output.

## Quick Start

```fsharp
open ProcessCore

let source = Sample("Base Culture", additionalType = "Source")
let sample = Sample("Cultivation Flask RT", additionalType = "Sample")

let growth =
    Process(
        "Growth",
        parameterValue =
            [ Annotation("temperature", value = "25", unit = "degree Celsius") ]
    )

growth.AddInputSample(source)
growth.AddOutputSample(sample)

let assay = Dataset("measurement1", title = "Proteomics assay")
assay.AddProcess(growth)

let roots = assay.RootSamples()
let finals = assay.FinalSamples()
```

## YAML

```fsharp
let yaml =
    ProcessCore.Yaml.Dataset.toYamlString (Some 2) assay

let decoded =
    ProcessCore.Yaml.Dataset.fromYamlString false yaml
```

Use `processCoreOnly = true` for strict type checks, or `false` when reading decorated/profile-shaped ARC YAML that may contain extension fields.

## SQLite Profile

The package includes SQL row types, row codecs, repository CRUD helpers, and a .NET SQLite driver:

```fsharp
open ProcessCore.SQL
open ProcessCore.SQL.DotNet

use driver = Sqlite.openInMemory()

DefinedTerm.insert(
    driver,
    DefinedTermRow("term:cell-growth", "DefinedTerm", "cell growth")
)

let terms = DefinedTerm.list(driver)
```

The executable SQLite schema and seed data live in `schemas/sql/` in the repository.

## Repository Build

```powershell
.\build.cmd BuildSolution
.\build.cmd RunTests
.\build.cmd BuildDocs
```

`RunTests` executes the shared Pyxpecto suite across the configured .NET, Python, and JavaScript paths.

## Status

This is an early implementation release. TypeScript and Python packaging are still evolving, and the data model may still see breaking changes as the profile alignment matures.


