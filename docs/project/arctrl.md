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
├── Operations/                    All thirteen entity APIs, Entity, and History
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

The management store uses session schema version **3**, with an entity registry,
ordered scalar/reference rows, per-type payload mirrors, and separate session,
history, and journal tables. This schema belongs to ARCtrl; it does not add
management storage to the core profile under `schemas/sql`. Both reuse PolyglotSQLite.
Version 2 sessions upgrade transactionally by adding the extension table and updating the version, retaining working state and history. Version 1 sessions are rejected, without implicit migration. An explicit `"yml"`
open reloads the saved graph and archives the previous database. Unsaved version 1
working state is not converted by this revision.

The prototype still reads/writes a single `arc.yml`, rather than implementing the
project-file codec dispatch planned in the management design. Its graph codec
writes shared or circular references as `{ $ref: id }`, defines entities once,
and distinguishes Annotation numbers and text using plain numeric scalars and quoted text. This is the prototype codec contract, not a change to normative profiles or
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

## Extension properties

`arc.Entity` manages extension properties on every core class and generic typed `EntityObject`. Convenience methods live here so each change uses the same SQL transaction, snapshot, journal, and undo/redo pipeline as core edits.

```fsharp
let quality = arc.Entity.create("QualityAssessment")
arc.Entity.setNumberProperty(quality, "score", 0.95) |> ignore
arc.Entity.setBoolProperty(quality, "accepted", false) |> ignore
arc.Entity.setNullProperty(quality, "missing") |> ignore
arc.Entity.setBlobProperty(quality, "bytes", "AA==") |> ignore
arc.Entity.setObjectProperty(arc.Model, "lab:quality", quality) |> ignore
arc.History.undo()
arc.History.redo()
```

`create`, `register`, `set`, `get`, `list`, and `delete` operate through the session registry. Generic objects use custom type names; a core discriminator requires its actual core class. Management assigns omitted IDs as before. Generic registration and full replacement adopt referenced graphs as one command; property setters require object references to be registered in this session first.

`setProperty`, `addProperty`, and `removeProperty` operate on named extension values. `getProperty` and `hasProperty` check ownership and direct mutation. Typed `setNumberProperty`, `setTextProperty`, `setBoolProperty`, `setObjectProperty`, `setCollectionProperty`, `setNullProperty`, and `setBlobProperty` have corresponding `add` methods. Add rejects duplicates; set replaces or inserts; missing removal is a reversible command with no property effect. Core names are reserved and comparisons are case-sensitive. Property enumeration has no guaranteed order.

Numbers must be finite binary64 values. Blob helpers validate canonical padded base64; SQL stores decoded bytes as BLOB. Explicit null and empty collections have present typed rows, distinct from missing properties. Collections are captured as ordered values, including duplicates and nesting; management restores their containers from snapshots. Typed entity references preserve canonical identity and can form cycles. Self-containing collection containers are rejected; express cycles through typed objects. Direct edits to managed objects or their nested collections are detected before committing.

`entity_extension` contains one root row per property (`path=''`) and one row per nested collection element. `owner_id`, `property`, and `path` identify a value; `parent_path` and `position` encode collection nesting. `storage` distinguishes text, number, Boolean, null, blob, object, and collection. Object rows use `target_id` foreign keys. These tables mirror the session snapshots used for recovery and history; this remains a session schema, separate from the normative core SQL profile.

Deleting an entity referenced by a surviving extension is rejected; remove that property first. Reachability and session-only detection follow object references even inside collections. `save` emits reachable extensions alongside core YAML fields, with plain numeric/Boolean/null scalars, quoted text, a standard binary tag URI, and existing `$ref` identity encoding. Reload preserves types, text versus numbers/booleans/null, blobs, shared entities, and cycles. This prototype codec support does not revise the derived YAML schemas or core SQL DDL.

Simple extension keys such as `Gender`, `altitude`, and `lab:altitude` are emitted without quotes. Unsafe or implicitly typed keys (for example `a: b`, `$ref`, `true`, and `1000`) are quoted to preserve their string names and support the pinned reader. Numbers use ordinary YAML numeric scalars without explicit float tags, including integer-valued binary64 numbers. Numeric-looking text remains quoted. Binary values use the valid standard tag URI `!<tag:yaml.org,2002:binary>`; other values do not need explicit built-in tags. Previously emitted scalar tags remain readable.
