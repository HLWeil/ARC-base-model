# ARCtrl

ARCtrl is the experimental ARC management library over ARCBaseModel and
PolyglotSQLite. `ARCSession.Session` owns a database and its connection;
`ARCtrl.ARC` manages one graph, its identity map, operations, history, and optional
folder binding. Both namespaces belong to the existing ARCtrl assembly.
The [general management plan](../../plans/arc-management.md) describes the
toolbox contracts and the separate future project-file dispatch work.

## Project structure

```text
src/ARCtrl/
├── ARC.fs                         One ARC's public facade
├── ARCSession/
│   ├── Repository.fs              Connection ownership and active ARC contexts
│   └── Session.fs                 Public session facade and ARC metadata
├── AppliedOperation.fs            Committed-operation result
├── Operations/                    All thirteen entity APIs, Entity, and History
├── Runtime/
│   ├── Session.fs                 ARC context, identity, transactions and undo/redo
│   ├── ArcFactory.fs              Graph adoption and lazy hydration
│   └── Workspace.fs               Binding, export and explicit folder recovery
├── Internal/
│   ├── State.fs                   Registry snapshots
│   └── Model.fs                   Model capture, validation and reachability
├── Serialization/YamlCodec.fs     Snapshot and root Dataset YAML codecs
├── Helper/                        Shared paths, IO, IDs, collections, regex and async utilities
└── Storage/SQLiteStore.fs          SQLite schema, connection and state mirrors
```

The [project file](../../src/ARCtrl/ARCtrl.fsproj) lists dependencies in F# compile
order. The internal context and operation groups precede ARC; the public Session
facade follows ARC. Compatibility factories delegate through internal factories.

The internal `ARCtrl.Helper` modules are adapted copies of the ProcessCore
Helper folder. `Path` owns .NET/Node/Python filesystem adapters; its synchronous
and async functions share one implementation per runtime. Async helpers use
F# Async on every target rather than requiring the ProcessCore Promise package.
Native filesystem joining uses `combineNative`, while `combine` builds
slash-separated ARC paths.
`Identifier.newId` provides the existing generated IDs, and `ResizeArray.replace`
restores collection contents while preserving references, order and duplicates.
Folder binding, save baselines and recovery policy remain in `Runtime/Workspace`.
Shared tests use these helpers for fixtures and file access.

Legacy identifier and ontology parsers are available internally for codec work;
they do not validate or normalize domain IDs. Spreadsheet adapters requiring
FsSpreadsheet are omitted. HTTP downloads use the existing platform APIs
(HttpClient, fetch and urllib), with no new dependency or public API.

## Use and verification

Select storage independently of folder IO:

```fsharp
open ARCBaseModel
open ARCtrl

let session = ARCSession.Session.createInMemory()
// Alternatively: ARCSession.Session.createFile("collection.sqlite")
// or ARCSession.Session.openFile("collection.sqlite").
let first = session.createArc(Dataset(["process-provenance"], ["first"]))
let second = session.createArc(Dataset(["process-provenance"], ["second"]))
let sample = first.Sample.create("sample")
let proc = first.Process.create("process")
first.Process.setInputSample(proc, sample) |> ignore
first.Dataset.addProcess(first.Model, proc) |> ignore
first.History.undo()
first.bindFolder("./arc-a")
first.save()
let arcId = first.ArcId
first.close()
let reopened = session.openArc(arcId)
let metadata = session.listArcs()
session.close()
```

`createFile` exclusively creates a new file and rejects existing destinations.
`openFile` requires an existing version-4 repository and validates its columns,
primary keys and composite foreign keys; it never initializes or migrates it.
`createArc` adopts the supplied graph and preserves supplied domain IDs while
assigning missing IDs under the existing management policy. `ArcId` is generated
repository metadata, independent of the root Dataset ID. Equal domain IDs in
different ARCs identify independent objects. Sharing a mutable managed entity
between live ARCs is rejected, including across separate repositories.

Opening a database and listing metadata do not hydrate graphs or read folders.
`openArc` hydrates only the selected entry and returns the same active facade and
object instances on repeated opens. Metadata contains `ArcId`, `RootEntityId`
and optional `Folder`. Closing an ARC evicts its context; opening it again creates
a fresh projection. Closing the session invalidates all its ARC handles and
releases its one owned PolyglotSQLite connection. The connection keeps an
in-memory database alive. Operations are synchronous and serialized through it.

`ARC.importFolder(session, folder)` imports the current prototype `arc.yml` into a
new entry and records a filesystem baseline. `bindFolder` establishes an export
destination without loading or writing it; a new binding requires an absent
`arc.yml`. `save` requires a binding and exports only the root graph. Standalone
objects and all history remain in the selected repository. Closing either
component never exports implicitly.

Compatibility `ARC.create(folder, rootDataset)` and
`ARC.openFolder(folder, "auto")` privately own a session at `.arc/testing.sqlite`.
Closing such an ARC also closes its session. A convenience opener requires
exactly one ARC entry and the matching folder binding. Explicit `"sql"` and
`"yml"` source preferences retain the existing conflict policy.

## Full base-model operations

ARC exposes `Dataset`, `Process`, `Sample`, `Data`, `Recipe`, `Annotation`,
`FormalParameter`, `DefinedTerm`, `DefinedTermSet`, `Descriptor`, `Agent`,
`Organization`, and `ScholarlyArticle` operation groups. Every group has
`create`, `register`, `get`, `list`, `upsert`, and `delete` methods. Dataset creation
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

`upsert(value)` returns `unit` and performs full replacement by ID using a detached
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

Repository schema version **4** uses `arc_id` to scope entity rows, typed rows,
ordered scalar/reference values, extensions, history, journals, revisions,
folder baselines and saved graphs. Composite foreign keys require owner and
target to belong to the same ARC, including typed Process endpoints and extension
references. PolyglotSQLite enables enforcement on each connection; ARCtrl has no
direct Microsoft.Data.Sqlite dependency or provider use. The normative core SQL
profile under `schemas/sql` remains independent.

Commands retain the snapshot pipeline but rebuild only their ARC's rows. Each
ARC has its own revision and undo/redo cursor. Edits to another ARC do not make a
handle stale; edits through another connection to the same ARC require closing
and reopening that ARC. Indexes on types, scalar properties/values and reverse
references provide a queryable cross-ARC storage backbone; there is no search API.
SQLite still permits only one writer.

There is no legacy migration. Database reopening performs no filesystem
reconciliation. Use `arc.recoverFolder("auto")`, `"sql"`, or `"yml"` explicitly, or
the compatibility folder opener. Replacing an ARC from YAML archives that
entry's metadata, state, history and journal inside the repository; it preserves
all other entries and the shared database file. External-change checks apply
before export. Failed saves retain the last successful SQL checkpoint and
pending status. File replacement and SQLite commit cannot be one atomic
transaction; if publication succeeds but checkpoint commit fails, explicit
source selection is required before retrying.

The prototype still reads/writes a single `arc.yml`, rather than implementing the
project-file codec dispatch planned in the management design. Its graph codec
writes shared or circular references as `{ $ref: id }`, defines entities once,
and distinguishes Annotation numbers and text using plain numeric scalars and quoted text. This is the prototype codec contract, not a change to normative profiles or
YAML schemas. Registered objects outside the root graph remain SQL-only.

```powershell
dotnet build src/ARCtrl/ARCtrl.fsproj -c Release
.\build.cmd TestManagementPrototype
.\build.cmd TestARCSession
dotnet run --project tests/ManagementPrototype.Tests -c Release -- --demo ./build/out/my-arc
```

The [walkthrough](../../tests/ManagementPrototype.Tests/Walkthrough.fs) illustrates
creation, mutation, undo/redo, export and reopening. Choose a new demo folder.
The full-model behavior tests are in
[FullModel.fs](../../tests/ManagementPrototype.Tests/FullModel.fs); portable helper
checks are in [Helpers.fs](../../tests/ManagementPrototype.Tests/Helpers.fs).
One test project runs all 127 helper, behavior, core SQL, full-model and toolbox tests
on .NET, JavaScript and Python using portable file access. Only the demo
walkthrough remains .NET-only. `TestManagementPrototype` retains compatibility coverage.
`TestARCSession` aggregates .NET, JavaScript, Python, native consumers and
declaration checks. Independent targets are `TestARCSessionDotNet`,
`TestARCSessionJS`, `TestARCSessionPy`, and `TestARCSessionNative`.

Staged entrypoints are `arc-session` and `arc_session`; import model classes from
`arc-base-model` / `arc_base_model`. Staging preserves common class definitions,
hides internal constructors and derives declarations from generated signatures.
Python methods follow the emitted snake_case convention. Management consumers
apply the existing model numeric compatibility pass, including the updated
Fable 5.20 representation; PolyglotSQLite keeps its own numeric boundaries.

Current verification is recorded in the [management plan](../../plans/arc-management.md).
Compilation and skipped checks are not passing runtime evidence.

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

`create`, `register`, `upsert`, `get`, `list`, and `delete` operate through the session registry. Generic objects use custom type names; a core discriminator requires its actual core class. Management assigns omitted IDs as before. Generic registration and full replacement adopt referenced graphs as one command; property setters require object references to be registered in this session first.

`setProperty`, `addProperty`, and `removeProperty` operate on named extension values. `getProperty` and `hasProperty` check ownership and direct mutation. Typed `setNumberProperty`, `setTextProperty`, `setBoolProperty`, `setObjectProperty`, `setCollectionProperty`, `setNullProperty`, and `setBlobProperty` have corresponding `add` methods. Add rejects duplicates; set replaces or inserts; missing removal is a reversible command with no property effect. Core names are reserved and comparisons are case-sensitive. Property enumeration has no guaranteed order.

Numbers must be finite binary64 values. Blob helpers validate canonical padded base64; SQL stores decoded bytes as BLOB. Explicit null and empty collections have present typed rows, distinct from missing properties. Collections are captured as ordered values, including duplicates and nesting; management restores their containers from snapshots. Typed entity references preserve canonical identity and can form cycles. Self-containing collection containers are rejected; express cycles through typed objects. Direct edits to managed objects or their nested collections are detected before committing.

`entity_extension` contains one root row per property (`path=''`) and one row per nested collection element. `arc_id`, `owner_id`, `property`, and `path` identify a value; `parent_path` and `position` encode collection nesting. `storage` distinguishes text, number, Boolean, null, blob, object, and collection. Object rows use ARC-qualified `target_id` foreign keys. These tables mirror the snapshots used for recovery and history; this remains a management schema, separate from the normative core SQL profile.

Deleting an entity referenced by a surviving extension is rejected; remove that property first. Reachability and session-only detection follow object references even inside collections. `save` emits reachable extensions alongside core YAML fields, with plain numeric/Boolean/null scalars, quoted text, a standard binary tag URI, and existing `$ref` identity encoding. Reload preserves types, text versus numbers/booleans/null, blobs, shared entities, and cycles. This prototype codec support does not revise the derived YAML schemas or core SQL DDL.

Simple extension keys such as `Gender`, `altitude`, and `lab:altitude` are emitted without quotes. Unsafe or implicitly typed keys (for example `a: b`, `$ref`, `true`, and `1000`) are quoted to preserve their string names and support the pinned reader. Numbers use ordinary YAML numeric scalars without explicit float tags, including integer-valued binary64 numbers. Numeric-looking text remains quoted. Binary values use the valid standard tag URI `!<tag:yaml.org,2002:binary>`; other values do not need explicit built-in tags. Previously emitted scalar tags remain readable.
