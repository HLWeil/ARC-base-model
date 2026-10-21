---
title: ARCManagement
category: Project
categoryindex: 2
index: 10
---

# ARCManagement

ARCManagement manages portable ARCBaseModel graphs in memory or in a retained
SQLite repository. It owns connections, graph mutation, identity, validation,
history, and YAML codecs. Workspace operations live in the nested ARCtrl part of
the same assembly; see the [workspace guide](arctrl.md). The
[management plan](../../plans/arc-management.md) records actual verification.

## Repository and graph lifecycle

```fsharp
open ARCBaseModel
open ARCManagement

let session = Session.createInMemory()
// Alternatively: Session.createFile("collection.sqlite")
// or Session.openFile("collection.sqlite").
let graph = session.createArc(Dataset(["process-provenance"], ["example"]))
let sample = graph.Sample.create("sample")
let proc = graph.Process.create("process")
graph.Process.setInputSample(proc, sample) |> ignore
graph.Dataset.addProcess(graph.Model, proc) |> ignore
graph.History.undo()
graph.History.redo()
let yaml = graph.toYaml()
let arcId = graph.ArcId
graph.close()
let reopened = session.openArc(arcId)
let metadata = session.listArcs()
session.close()
```

`Session` owns one PolyglotSQLite connection. `createFile` exclusively creates a
new database. `openFile` requires an existing schema-version-4 repository and
validates its columns, keys, and constraints; it never initializes or migrates
it. In-memory storage lasts until its session closes. Access is synchronous and
serialized.

`createArc` returns a `ManagedGraph` and adopts the supplied graph: it preserves
supplied IDs, assigns missing IDs, and rejects distinct objects with the same
ID. Generated repository `ArcId` is independent of the root Dataset ID. Equal
domain IDs in different entries identify independent objects. Sharing a mutable
entity between live entries is rejected, including across separate repositories.

Opening a database and listing `ArcInfo` metadata do not hydrate graphs or read
folders. Metadata contains `ArcId`, `RootEntityId`, and optional `Folder`.
`openArc` loads the selected entry and returns the same active `ManagedGraph`,
objects, and operation groups on repeated opens. Closing a graph releases its
context; reopening creates a fresh projection. Closing the session invalidates
all its graphs and releases its connection. No close implicitly exports files.

`ManagedGraph` exposes `Model`, `ArcId`, `DatabasePath`,
`HasSessionOnlyObjects`, `History`, the operation groups, `toYaml()`, and
`close()`. Folder binding, saving, recovery, and `IsDirty` belong to
[`ARCtrl.ARC`](arctrl.md).

## Entity operations and history

The entity groups are `Dataset`, `Process`, `Sample`, `Data`, `Recipe`,
`Annotation`, `FormalParameter`, `DefinedTerm`, `DefinedTermSet`, `Descriptor`,
`Agent`, `Organization`, and `ScholarlyArticle`, alongside generic `Entity` and
`History`. Entity groups provide `create`, `add`, `get`, `list`, `upsert`, and
`delete`, property setters, optional-value clear methods, collection replacement,
and typed relationship helpers. Dataset creation defaults to process provenance;
use `add(dataset)` for other profiles. Descriptor creation requires Sample/Data.

`add(value)` recursively adopts a supplied graph and returns its supplied
instance. It validates state, assigns omitted IDs, and rejects collisions with
distinct instances. Adding the same active instance again is a no-op.
Registration does not attach an object to the root. Standalone registered
objects survive SQL recovery and contribute to `HasSessionOnlyObjects`.

`upsert(value): unit` performs full replacement by ID using detached input.
Unknown IDs adopt the supplied graph. Existing same-type IDs update the retained
canonical instance and replace its collection containers. Empty collections clear
values.
References to managed entities must use registered instances. Cross-type
collisions are rejected. ID assignment belongs to management, never Layer 1;
identity is not inferred from names, paths, or equal values.

Transitional note: until the management runtime phase of the immutable redesign,
managed graphs update adopted instances in place. They write IDs into supplied
graphs and reassign internal state on edits and undo.

Collection edits preserve order, duplicates, and shared references. Removal
deletes one occurrence without unregistering its target. `movePart` and
`moveProcess` require at most one membership; replacement setters support shared
or repeated members. Process endpoints remain optional and singular. Typed
helpers such as `setInputSample`, `setOutputData`, `setValueText`,
`setValueNumber`, `setIntendedUseTerm`, and `setInDefinedTermSetUrl` avoid native
union wrappers. Text, absence, empty text, zero, and fractional numbers remain
distinguishable.

Edits validate a planned snapshot, then transactionally update SQL mirrors, the
live projection, history/cursor, journal, and graph revision. Failures restore
SQL and the previous projection. Mutations return attached `AppliedOperation`
values. Undo/redo is linear and persisted; editing after undo discards redo.
Hydration and history restoration keep canonical instances and replace
collection containers. Undoing a registration restores originally absent IDs, and redo restores
assigned IDs. Saving preserves history and standalone objects.

Root deletion is rejected. Dataset deletion cascades only to descendants and
processes without surviving owners. Required Descriptor targets and surviving
extension references prevent deletion. Undo restores affected links.

Direct property or collection edits on managed objects are unsupported. Commands
and exports compare the projection with its retained snapshot and reject detected
changes. Close and reopen to restore committed state. Revisions are entry-local:
another entry's edits do not invalidate a graph; stale writes to the same entry
through another connection require close and reopen. SQLite permits one writer.

## YAML workflows

```fsharp
let detached = Dataset(["process-provenance"], ["detached"])
let text = DatasetYaml.encode(detached)
let decoded = DatasetYaml.decode(text)
let imported = session.importYaml(text)
let managedText = imported.toYaml()
```

`DatasetYaml.encode` and `decode` perform no database/filesystem work and assign
no domain IDs. Encoding does not modify supplied objects. Each distinct entity
receives a deterministic serialization-local YAML anchor; repeated references
and cycles use aliases. `id` is emitted only when supplied. Decode preserves
absent IDs, distinct objects with equal supplied IDs, order, duplicates, shared
references, cycles, extensions, and scalar alternatives.

Documents with anchors or aliases use local-reference decoding. Entity anchors
are registered before reading their properties. Duplicate anchors, unresolved or
forward aliases, and mixed legacy reference syntax are rejected. Legacy prototype
`id`/`$ref` documents remain readable. A literal `$ref` extension property on a
typed entity is supported.

`session.importYaml(yaml)` decodes detached objects, then adopts them through
ordinary management rules: missing IDs are assigned and distinct-object ID
collisions rejected. It creates a fresh unbound entry with empty history.

`graph.toYaml()` exports only the graph reachable from `Model` using the
prototype `id`/`$ref` format also used by workspace saves. Shared/circular
entities are defined once; standalone registered objects remain SQL-only.
Exporting text changes no files, save checkpoints, or history. Snapshot codecs
remain private. These prototype formats do not revise normative YAML schemas.

## Extensions and storage

`graph.Entity` manages extensions on core classes and generic typed
`EntityObject` values through the same transaction/history pipeline. Typed
helpers cover text, finite binary64 numbers, Boolean, null, canonical padded
base64 blobs, object references, and nested ordered collections. Missing
properties, explicit null, and empty collections are distinct. Core property
names are reserved and comparisons are case-sensitive. Set replaces or inserts;
add rejects duplicate properties. Removing a missing property is reversible with
no property effect. Property enumeration has no guaranteed order.

Object references preserve canonical identity and support cycles, including
through collections. Self-containing collection containers are rejected; express
cycles through typed objects. Reachability follows extension references.

Schema version **4** remains separate from the normative core SQL profile.
Entity and typed rows use `(arc_id, id)`. Values/references retain order;
extensions additionally use nested paths. Composite foreign keys enforce entry
scope for typed, generic, and extension references. History, journals, revisions,
baselines, saved graphs, and archives are entry-local. Commands currently rebuild
only their entry's rows. There is no extra entity surrogate-key system, public
cross-entry search API, or legacy migration. PolyglotSQLite owns all providers.

## Layout, packages, and verification

The [project](../../src/ARCManagement/ARCManagement.fsproj) builds one
`ARCManagement` assembly. Generic facades, `Operations/`, `Runtime/`, `Internal/`,
`Serialization/`, `Storage/`, and portable `Helper/` code compile before workspace
APIs in `ARCtrl/`. Workspace policy, fixed paths, and workspace-specific
identifier/path helpers stay in that nested folder. UUID generation and general
filesystem adapters remain generic.

| Runtime | Generic APIs | Workspace API |
|---|---|---|
| F#/.NET | `ARCManagement.Session`, `ManagedGraph`, `DatasetYaml` | `ARCtrl.ARC` |
| JavaScript/TypeScript | `arc-management` | `arc-management/arctrl` |
| Python | `arc_management` | `arc_management.arctrl` |

Import domain classes from `arc-base-model` / `arc_base_model`. Entrypoints share
one implementation and canonical model/SQLite classes; callers need no generated
implementation imports or runtime helpers. Python methods use emitted snake_case
names such as `create_in_memory`, `create_arc`, `import_yaml`, and `to_yaml`.
Staging keeps curated exports and precise declarations. Management consumers use
the existing model Python numeric compatibility pass; PolyglotSQLite retains its
own representation boundaries. Keep the pinned toolchain.

```powershell
.\build.cmd TestARCManagement
.\build.cmd TestBaseModel
.\build.cmd RunTests
```

`TestARCManagement` aggregates `TestARCManagementDotNet`, `TestARCManagementJS`,
`TestARCManagementPy`, and `TestARCManagementNative`, including handwritten
JS/TS/Python consumers, declaration checks, and packed artifacts. The
[shared tests](../../tests/ARCManagement.Tests/Main.fs) are portable; only the
[demo walkthrough](../../tests/ARCManagement.Tests/Walkthrough.fs) is .NET-only.
See the [plan](../../plans/arc-management.md) for actual results. Compilation and
skipped execution are not passing runtime evidence.
