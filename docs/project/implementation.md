---
title: Implementation Guide
category: Project
categoryindex: 2
index: 3
---

# Implementation Guide

The repository now includes F# implementation projects in addition to the markdown specification. The implementation is intentionally small and follows the schema/spec work rather than replacing it.

## Projects

| Project | Role | Runtime |
|---------|------|---------|
| `src/ProcessCore` | In-memory unified ARC RDM model, YAML codecs, SQL profile, graph traversal, and table projection helpers | .NET, JavaScript, Python |
| `tests/ProcessCore.Tests` | Shared Pyxpecto tests for core, YAML, and SQL behavior | .NET, JavaScript, Python |

## ProcessCore User Documentation

The F# object model, YAML codec, graph traversal helpers, fragment selector providers, and table views are documented in the [ProcessCore user guide](../core-implementation/overview.md). The public model is unified rather than split into separate runtime models for each base or decoration profile.

## SQL Profile

The SQL profile artifacts live in `schemas/sql/`:

- `001_core.sql` contains the current executable SQLite DDL for the process graph profile. Datamap and administrative SQL storage are intentionally out of scope for this transition step.
- `seed_example.sql` contains a small seeded process graph.
- `seeded_core.sqlite` is the generated SQLite database.
- `design.md` explains the relational design.

`ProcessCore.SQL` inside the consolidated `ProcessCore` project mirrors the SQL profile:

- `Tables.fs` defines row types for tabular representation of the underlying processes.
- `RowCodecs.fs` converts between `SqlRow` values and row types.
- `Repository.fs` defines table metadata and CRUD facades.
- `Sql.fs` defines the portable SQL value shape and `ISqliteDriver`.
- `Platform/Driver.fs` records runtime adapter choices.

## Runtime Adapters

Runtime-specific SQL drivers live in target-specific files under `src/ProcessCore/SQL/`:

- .NET uses `Microsoft.Data.Sqlite`.
- JavaScript uses `better-sqlite3` after Fable transpilation.
- Python uses the standard-library `sqlite3` module after Fable transpilation.

The JavaScript and Python adapter projects compile as .NET stubs outside their Fable runtime so the solution can build while still exposing useful runtime code after transpilation.

## Commands

```powershell
.\build.cmd BuildSolution
.\build.cmd RunTests
```

Repo-level Node tooling is already present:

```powershell
npm run test:js
```

Python test execution uses `uv` through the FAKE `RunTests` target.


