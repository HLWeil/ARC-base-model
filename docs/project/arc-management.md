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
let withInput = graph.Process.setInput(proc, Some(EntityReference.Sample sample))
graph.Dataset.addProcess(graph.Model, withInput) |> ignore
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
new database. `openFile` requires an existing schema-version-5 repository and
validates its columns, keys, and constraints; it never initializes or migrates
it. In-memory storage lasts until its session closes. Access is synchronous and
serialized.

`createArc` returns a `ManagedGraph` over a persisted copy of the supplied graph.
It preserves supplied IDs, assigns missing IDs to the copy, and rejects distinct
objects with the same ID. The supplied objects are never modified. Generated
repository `ArcId` is independent of the root Dataset ID. Equal domain IDs in
different entries identify independent objects.

Opening a database and listing `ArcInfo` metadata do not hydrate graphs or read
folders. Metadata contains `ArcId`, `RootEntityId`, and optional `Folder`.
`openArc` loads the selected entry and returns the same active `ManagedGraph`
and operation groups on repeated opens. The graph caches the stored rows of its
entry (state, ordinals, cursor, revision, folder, baseline, and saved graph).
SQL is authoritative. Closing a graph releases its context. Closing the session
invalidates all its graphs and releases its connection. Values already returned
stay valid after either. No close implicitly exports files.

`ManagedGraph` exposes `Model`, `ArcId`, `DatabasePath`,
`HasSessionOnlyObjects`, `History`, the operation groups, `toYaml()`, and
`close()`. Folder binding, saving, recovery, and `IsDirty` belong to
[`ARCtrl.ARC`](arctrl.md).

## Entity operations and history

The entity groups are `Dataset`, `Process`, `Sample`, `Data`, `Recipe`,
`Annotation`, `FormalParameter`, `DefinedTerm`, `DefinedTermSet`, `Descriptor`,
`Agent`, `Organization`, and `ScholarlyArticle`, alongside generic `Entity` and
`History`. Each entity group provides `create`, `add`, `upsert`, `get`, `list`
and `delete`. It has one `set<Property>` method per property, where an option
argument clears an optional value, and `add<Item>` and `remove<Item>` methods per
entity collection, such as `addProcess` and `removePart`. The `Entity` group has
the same CRUD members plus `setAdditionalTypes`, `setProperty`, `addProperty`, and
`removeProperty`. The groups hold about 199 members in all. Dataset creation
defaults to process provenance. Use `add(dataset)` for other profiles. Descriptor
creation requires Sample/Data.

```fsharp
let sample = graph.Sample.create("sample")
let proc = graph.Process.create("process")
let linked = graph.Process.setInput(proc, Some(EntityReference.Sample sample))
let root = graph.Dataset.addProcess(graph.Model, linked)
let cleared = graph.Process.setInput(linked, None)
```

```javascript
const sample = graph.Sample.create("sample");
const proc = graph.Process.create("process");
const linked = graph.Process.setInput(proc, sample);
const root = graph.Dataset.addProcess(graph.Model, linked);
graph.Dataset.upsert(root, true); // recursive is a positional optional argument
```

```python
sample = graph.Sample.create("sample")
proc = graph.Process.create("process")
linked = graph.Process.set_input(proc, sample)
root = graph.Dataset.add_process(graph.Model, linked)
graph.Dataset.upsert(root, recursive=True)  # recursive is a keyword
```

Management keeps no live model objects. Every returned entity is an immutable
Layer 1 value that never changes afterwards. That holds across later edits,
undo and redo, and graph or session closure. `create`, `add`, and `upsert` return
the stored copy with its assigned IDs. Setters and `add<Item>`/`remove<Item>`
return the updated value. `delete`, `undo`, and `redo` return unit. `list`
returns an immutable collection in ordinal order (a read-only collection on .NET, a
frozen array on JavaScript, and a tuple on Python). Identity across revisions is
not promised, so compare entities by their IDs.

Subjects are identified by their `Id` and must exist with the group's kind.
Setters apply to the current stored state, so the setter always wins over a
subject value that has fallen behind. Relationship targets must already be stored.
`add` inserts strictly: an existing ID fails, and new nested entities are inserted
with it. `upsert` inserts an unknown root or replaces the root's own fields.
Nested entities with stored IDs are links and stay unchanged unless `recursive`
is true. Then every reachable submitted entity is written as the last truth.
Distinct submitted objects with one ID must have equal definitions. Upserting a
Dataset never changes the ARC root. Cross-type ID collisions are rejected.

Management never modifies supplied objects. It assigns missing IDs to the
persisted copies. ID assignment belongs to management, never Layer 1.
Identity is not inferred from names, paths, or equal values. Values read from
another ARC are accepted and resolve by ID in the destination. Native numbers
supplied for Number values become native floats at the management boundary, and
Booleans are rejected there.

Collection edits preserve order, duplicates, and shared references. `removePart`
and its siblings delete one occurrence without unregistering the target.
Replacement setters support shared or repeated members. Process endpoints remain
optional and singular. `setInput` and `setOutput` take an `EntityReference` (a
Sample or Data value) and accept absence. Text, absence, empty text, zero, and
fractional numbers remain distinguishable.

Each command is a pure transform of the stored state, run in one outermost
transaction. Its first statement is
`UPDATE arc SET revision = revision + 1 WHERE arc_id=$arc RETURNING revision`.
If the cached revision is older, the transaction reloads the state first. A
command whose result equals the current state rolls back, so revision, history,
and redo stay unchanged. A changed result is validated, written as a row-level
diff with one `history` record, committed, and then published to the cache.
Provider errors such as `SQLITE_BUSY` surface unchanged. There are no stale-handle
errors and callers pass no revision tokens.

Reads check the revision with one query. When another connection has committed,
the read reloads the stored rows inside a read transaction. Reads hydrate fresh
immutable values on demand and cache them per revision. Revisions are entry-local,
so edits to another entry never affect a graph. SQLite permits one writer.

Undo and redo are linear and persisted, and they apply the recorded before and
after rows through the same writer. Editing after undo discards redo. Undoing an
adoption restores originally absent IDs, and redo restores assigned IDs. Saving
preserves history and standalone objects.

Root deletion is rejected. Dataset deletion cascades only to descendants and
processes without surviving owners. Required Descriptor targets and surviving
extension references prevent deletion. Undo restores affected links.

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

Object references resolve by ID and support cycles, including
through collections. Self-containing collection containers are rejected; express
cycles through typed objects. Reachability follows extension references.

Schema version **5** creates the tables and indexes of the normative core SQL
profile ([001_core.sql](../../schemas/sql/001_core.sql)) with an added `arc_id`
column, plus the profile's views. Primary keys and foreign keys are composite
over `arc_id`, and the registry foreign key cascades on delete. Association
targets have extra `(arc_id, target)` indexes. `process_edges` and
`core_validation_errors` are scoped by `arc_id`. The writer checks every write
with `Model.validate` and does not run `core_validation_errors`. `Model.validate`
is less strict than that view, so a stored ARC can report rows there. A Dataset
with processes whose `conformsTo` lacks process-provenance passes the write check
and appears in the view. The view stays available for queries.

Management tables add `repository`, `arc` (root, folder, baseline, saved graph,
cursor, revision), the entity registry `entity` (id, type, ordinal),
`entity_additional_type` for non-core typed objects, `entity_extension`
(blob extensions keep canonical base64 text in `value_text`, and value lookup
indexes cover `(arc_id, property, value_text)` and `(arc_id, property, value_number)`),
`history`, `arc_archive`, and `archived_history`. Each `history` row holds one
operation as encoded change records. A record holds the entity ID and ordinal,
plus the before and after rows of one changed entity. History therefore grows with
the size of the changed entities and is independent of the rest of the graph. One
Dataset with a long process list carries that list in each of its edits. Ordinals
keep list positions through undo and redo.

A write inserts new rows, removes changed association, endpoint, and extension
rows, updates scalars, clears outgoing references of removed rows, deletes
descriptors, then other core rows, then registry rows, inserts same-ID kind
replacements, and adds rows. A title edit changes four rows: the dataset update,
the history insert, and the separate `arc` revision and cursor updates. Association list edits keep the common
prefix, so appending one element writes one row. The store has no journal, typed
payload tables, mirror tables, or full state snapshots in history. The registry
and core tables do not store `SuppliedId`. History change records and archived
state do, so undoing an adoption after reopening restores absent IDs.

Opening compares every expected object in `sqlite_master` with its exact SQL
text. Opening rejects a version 4 database with a hint to re-import the ARC from
`arc.yml`, and there is no migration. There is no extra entity surrogate-key
system or public cross-entry search API. PolyglotSQLite owns all providers.

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
