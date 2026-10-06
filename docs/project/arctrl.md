# ARCtrl

ARCtrl is the experimental ARC management library over ARCBaseModel and
PolyglotSQLite. Public classes remain in the `ARCtrl` namespace;
implementation details are internal to `ARCtrl.Internal`.
The [general management plan](../../plans/arc-management.md) describes the
intended architecture beyond this .NET-first prototype.

## Project structure

```text
src/ARCtrl/
├── ARC.fs                         Public session facade
├── AppliedOperation.fs            Committed-operation result
├── Operations/                    All thirteen entity APIs and History
├── Runtime/
│   ├── Session.fs                 Identity, transactions, undo/redo and save
│   └── Workspace.fs               Open/create and source-conflict resolution
├── Internal/
│   ├── State.fs                   Registry snapshots
│   └── Model.fs                   Model capture, validation and reachability
├── Serialization/YamlCodec.fs     Snapshot and root Dataset YAML codecs
├── IO/FileSystem.fs               .NET/Node/Python filesystem boundaries
└── Storage/SQLiteStore.fs          SQLite schema, connection and state mirrors
```

The [project file](../../src/ARCtrl/ARCtrl.fsproj) lists dependencies in F# compile
order. Each operation group depends on the internal session, and the public ARC
facade is compiled last; no mutually recursive public type group is needed.

## Use and verification

Use `open ARCtrl` and `ARC.create(folder, rootDataset)` or
`ARC.openFolder(folder, "auto")`. The operation shape is unchanged:
`arc.Process.setInputSample(proc, sample)` and `arc.History.undo()`.
`save()` writes the root Dataset and its referenced objects to `arc.yml`;
standalone registered objects and undo/redo history remain in `.arc/testing.sqlite`.
Closing does not implicitly save. Explicit `"sql"` and `"yml"` source preferences
resolve conflicts with external YAML changes.

## Full base-model operations

ARC exposes `Dataset`, `Process`, `Sample`, `Data`, `Recipe`, `Annotation`,
`FormalParameter`, `DefinedTerm`, `DefinedTermSet`, `Descriptor`, `Agent`,
`Organization`, and `ScholarlyArticle` operation groups. Every group has
`create`, `register`, `get`, `list`, `set`, and `delete` methods. Dataset creation
uses a process-provenance profile by default; register a supplied Dataset to
choose other profiles. Descriptor creation requires a Sample/Data reference.

Each group provides setters for every editable model property, clear methods for optional
scalar properties, and replacement setters for collections. Entity collections
also have typed add/remove methods. Collection replacement preserves order and
duplicates; remove deletes one matching occurrence. Dataset and Data containment
may be shared or recursive. Removing a relationship does not unregister its target.
`movePart`/`moveProcess` require at most one membership; use collection setters
when moving shared or repeated members. The fixed `Type` and registered `Id` are
not editable through property setters; identity changes use registration/upserts.

`set(value)` returns `unit` and performs full replacement by ID using a detached
input. Unknown IDs register the supplied graph; missing IDs are assigned only by
this management layer. Existing same-type IDs update the retained canonical
instance and its collection containers. Empty collections clear their values.
References to already managed entities must use the registered instances;
new referenced graphs are registered as part of the same command. Cross-type ID
collisions and direct edits to managed values are rejected before committing.

```fsharp
let annotation = arc.Annotation.create("temperature")
arc.Annotation.setValueNumber(annotation, 22.5) |> ignore
let recipe = arc.Recipe.create("extraction")
arc.Recipe.addComponent(recipe, annotation) |> ignore
arc.Process.setExecutesRecipe(proc, recipe) |> ignore
let data = arc.Data.create("results.csv")
arc.Process.setOutputData(proc, data) |> ignore
arc.History.undo()
arc.History.redo()
```

Typed alternative helpers include `setInputSample`, `setInputData`,
`setOutputSample`, `setOutputData`, `setDescribesSample`, `setDescribesData`,
`setValueText`, `setValueNumber`, `setIntendedUseText`, `setIntendedUseTerm`,
`setInDefinedTermSetUrl`, and `setInDefinedTermSetEntity`. Supplied text remains
text even when it matches a managed ID. Optional values distinguish absence,
empty text, numeric zero, and fractional numbers.

All changes use the existing transactional snapshot/history pipeline. Undo/redo
retains shared object identity and survives SQL recovery. Undoing a new registration
restores originally omitted IDs to absence; redo restores the assigned IDs. Keep
an ID before undo when using it to reactivate a registration on a new history branch.
Dataset deletion cascades to descendants and processes only when no surviving
owner references them. Other deletions detach optional references and collection
members; a required Descriptor target prevents deletion. The root Dataset cannot
be deleted. Undo restores all affected relationships.

## Persistence boundaries

The management store uses session schema version **2**, with an entity registry,
ordered scalar/reference rows, per-type payload mirrors, and separate session,
history, and journal tables. This schema belongs to ARCtrl; it does not add
management storage to the core profile under `schemas/sql`. Both reuse PolyglotSQLite.
Version 1 sessions are rejected, without implicit migration. An explicit `"yml"`
open reloads the saved graph and archives the previous database. Unsaved version 1
working state is not converted by this revision.

The prototype still reads/writes a single `arc.yml`, rather than implementing the
project-file codec dispatch planned in the management design. Its graph codec
writes shared or circular references as `{ $ref: id }`, defines entities once,
and distinguishes Annotation numbers and text with explicit `!!float`/`!!str`
tags. This is the prototype codec contract, not a change to normative profiles or
YAML schemas. Registered objects outside the root graph remain SQL-only.

```powershell
dotnet build src/ARCtrl/ARCtrl.fsproj -c Release
.\build.cmd TestManagementPrototype
dotnet run --project tests/ManagementPrototype.Tests -c Release -- --demo ./build/out/my-arc
```

The [walkthrough](../../tests/ManagementPrototype.Tests/Walkthrough.fs) illustrates
creation, mutation, undo/redo, export and reopening. Choose a new demo folder.
The full-model behavior tests are in
[FullModel.fs](../../tests/ManagementPrototype.Tests/FullModel.fs).
The library remains .NET-first. Portable compilation and native staging are
verification work; they do not add entity-management logic. Native runtime
verification is not yet passing evidence for this revision.
