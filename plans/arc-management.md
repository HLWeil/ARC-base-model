# ARC Management API

Status: generic ARCManagement and workspace-specific ARCtrl are separated and
verified, including detached YAML workflows and renamed native packages.
`BuildSolution` and repository-wide `RunTests` passed. Project-file codec
integration remains planned. The
[immutable redesign](mutation_safety_immutable.md) is implemented. The management
runtime keeps no live model objects, the operation groups are trimmed, and one
ProcessCore-shaped codec replaces the `$ref` and anchor formats.

This plan tracks the current implementation and remaining work. See the
[management guide](../docs/project/arc-management.md) and
[ARCtrl workspace guide](../docs/project/arctrl.md) for usage and storage details, and
[the project-file specification](../docs/spec/project_file.md) for the intended
workspace format.

## Objective and boundaries

Provide a portable ARC management layer over `ARCBaseModel` with fine-grained
operations, stable entity IDs, reversible history, and retained SQLite
working state on .NET, JavaScript/TypeScript, and Python.

The responsibilities are:

| Component | Responsibility |
|---|---|
| `ARCBaseModel` | Immutable domain objects without storage, identity assignment, graph management, or filesystem policy. |
| `ARCManagement.Session` | One owned PolyglotSQLite connection, repository creation/opening, ARC enumeration, and connection lifetime. |
| `ARCManagement.ManagedGraph` | One managed graph, entity registry, operations, validation, transactions, history, and graph lifetime. |
| `ARCManagement.DatasetYaml` | Detached Dataset YAML encoding and decoding without identity assignment, ownership, or IO. |
| `ARCtrl.ARC` | One workspace facade over an existing ManagedGraph; folder binding, export, and recovery. |
| Persistent IO | Explicit export of the root graph; currently prototype `arc.yml`, eventually resources selected by `.arc/project.yml`. |

SQLite retains committed working state, standalone objects, and undo/redo
history across restarts. It is independent of folder IO and is not a project-file
codec. Filesystem export represents the saved root graph, not the entire
registry. Losing the database and its archives loses unsaved and session-only
work that cannot be reconstructed from that export.

## Implemented baseline

### Repository and ARC lifecycle

```fsharp
open ARCBaseModel
open ARCManagement

let session = Session.createInMemory()
// Alternatively: createFile("collection.sqlite") / openFile("collection.sqlite").
let graph = session.createArc(Dataset(["process-provenance"], ["example"]))
let sample = graph.Sample.create("sample")
let process = graph.Process.create("process")
let withInput = graph.Process.setInput(process, Some(EntityReference.Sample sample))
graph.Dataset.addProcess(graph.Model, withInput) |> ignore
graph.History.undo()
graph.History.redo()
let yaml = graph.toYaml()
let arcId = graph.ArcId
graph.close()
let reopened = session.openArc(arcId)
let entries = session.listArcs()
session.close()
```

- `createFile` exclusively creates a new database; existing destinations are
  rejected. `openFile` requires a valid schema-version-5 repository and checks
  columns, keys, and relationship constraints. It neither initializes nor
  migrates an existing database.
- `createArc` persists a copy of the supplied graph and never modifies the
  supplied objects. Its generated repository `ArcId` is
  independent of the root Dataset ID. Domain IDs are scoped to one ARC, so
  different ARCs can contain equal IDs without sharing objects.
- Opening a repository and listing `ArcInfo` metadata do not hydrate graphs or
  read folders. `openArc` loads only the selected entry and returns its existing
  ManagedGraph until that graph closes.
- A graph keeps no model objects. Its context caches the stored rows of one entry
  (state, ordinals, cursor, revision, folder, baseline, saved graph) and SQL is
  authoritative. Closing a graph releases its context. Closing the session
  invalidates all its graphs and releases its connection. Values already returned
  stay unchanged after either close. In-memory storage lasts for that connection.
  Neither close exports implicitly.
- Equal domain IDs in different ARCs identify independent entities. Values read
  from another ARC are accepted and resolve by ID in the destination. Connection
  access is synchronous and serialized.

### Entity operations and identity

All thirteen model entity types have operation groups, alongside generic
`Entity` and `History`, about 199 members in all. Entity groups expose `create`,
`add`, `upsert(value, ?recursive)`, `get`, `list`, and `delete`, one
`set<Property>` per property (an option clears), and `add<Item>`/`remove<Item>`
per entity collection. The `Entity` group has CRUD, `setAdditionalTypes`, and
`setProperty`/`addProperty`/`removeProperty`. `AppliedOperation` is removed.
`create`, `add`, and `upsert` return the stored copy with assigned IDs. Setters
and add/remove return the updated immutable value. `delete`, `undo`, and `redo`
return unit. `list` returns an immutable collection in ordinal order (a
read-only collection on .NET, a frozen array on JavaScript, a tuple on Python).
`recursive` is a positional optional argument in JavaScript and a keyword in
Python.

- Subjects are identified by their `Id` and must exist with the group's kind. A
  group rejects a subject whose stored kind differs from its own. The `Entity`
  group accepts any kind. Setters apply to the current stored state, so the setter
  always wins. Relationship targets must already be stored. A relationship or
  extension target whose ID is stored with a different kind is rejected.
- `add` inserts strictly (an existing ID fails) and inserts new nested entities.
- `upsert` inserts an unknown root or replaces the root's own fields. Nested
  entities with stored IDs are links and stay unchanged unless `recursive` is
  true, which writes every reachable submitted entity as the last truth.
  With `recursive`, distinct submitted objects with one ID are accepted when their
  definitions are equal. Equal definitions means equal stored rows, so nested
  objects without IDs must be the same objects, and two distinct ID-less children
  make the definitions differ. Upserting a Dataset never changes the ARC root.
- `add`, non-recursive `upsert`, `createArc`, `importYaml`, and workspace import
  and recovery reject distinct new objects that share one ID. History entries for `add` and `upsert` are named
  `<Kind>.add` and `<Kind>.upsert`.
- Management never modifies supplied objects. It assigns missing IDs to the
  persisted copies. ID assignment belongs to this management layer, never Layer 1.
  Identity is not inferred from names, paths, or equal values. Undo of an add
  removes the added rows, and redo restores them with their assigned IDs.
- Collection edits preserve order, duplicates, and shared references. Removal
  deletes one occurrence and does not unregister the target. Replacement setters
  handle shared or repeated members.
- Process endpoints are optional and singular. `setInput` and `setOutput` take an
  `EntityReference` and accept absence. Other alternatives distinguish
  text/numbers and entity/URL values, including absence, empty text, zero, and
  fractional numbers. Native numbers supplied for Number values become native
  floats at the management boundary, and Booleans are rejected.
- Root deletion is rejected. Dataset deletion cascades only to descendants and
  processes without surviving owners. Required Descriptor targets and surviving
  extension references prevent target deletion; undo restores affected links.

`graph.Entity` also manages generic typed objects and extension properties on all
core classes. Extension values go through `setProperty`, `addProperty`, and
`removeProperty` with Layer 1 value classes: text, finite binary64 numbers,
Boolean, null, canonical base64 blobs, object references, and nested ordered
collections. Missing properties, explicit null, and empty collections remain
distinct. Removing a missing property changes nothing and writes no history.
Object cycles are supported; self-containing collection containers are rejected.
Core property names are reserved. Extension changes use the same command,
validation, SQL, and history pipeline as core edits.

### Commands, history, and consistency

A write runs in one outermost transaction whose first statement is
`UPDATE arc SET revision = revision + 1 WHERE arc_id=$arc RETURNING revision`.
If the cached revision is older, the transaction reloads the state first. The command is a pure state transform. An unchanged result rolls back
without changing revision, history, or redo. A changed result is validated,
written as a diff with one history row, committed, and then published to the
cache. Provider errors such as `SQLITE_BUSY` surface unchanged. There are no
stale-handle errors and no caller revision tokens.

Reads check the revision with one query and reload inside a read transaction when
another connection has committed. They hydrate fresh immutable values on demand
with a per-revision cache. Identity across revisions is not promised. Returned
values never change after they are returned, including after later edits,
undo/redo, and graph or session closure. The ownership registry and direct
mutation detection are gone.

Undo/redo is linear and persisted as recorded before/after rows applied through
the same writer. A new edit after undo invalidates the redo branch. Saving
preserves history and standalone stored objects.

Revisions are ARC-local. Edits to another ARC do not affect a graph. SQLite still
permits only one writer. This is not collaborative editing.

### SQLite storage

The management schema is owned by ARCManagement and remains separate from the normative
core SQL profile. ARCManagement uses PolyglotSQLite exclusively; provider code and
provider dependencies stay inside that library.

Schema version **5** (`CoreSchema.version`, 93 DDL statements in
`Storage/CoreSchema.fs`) holds the tables, indexes and views of
`schemas/sql/001_core.sql` with `arc_id` added. Keys and foreign keys are
composite, and the registry foreign key cascades on delete. Association targets
have extra `(arc_id, target)` indexes. `process_edges` and `core_validation_errors`
are scoped by `arc_id`. The writer checks every write with `Model.validate` and
does not run `core_validation_errors`. `Model.validate` is less strict than that
view, so a stored ARC can report rows there. A Dataset with processes whose
`conformsTo` lacks process-provenance passes the write check and appears in the
view. The view stays available for queries.

Management tables are `repository`, `arc` (root, folder, baseline, saved graph,
cursor, revision, replacing `session`), the registry `entity` (id, type, ordinal),
`entity_additional_type` for non-core typed objects, `entity_extension` (blob
extensions keep canonical base64 text in `value_text`, with value lookup indexes on
`(arc_id, property, value_text)` and `(arc_id, property, value_number)`),
`history`, `arc_archive` and `archived_history`. A `history` row is one operation
with encoded change records. Each record holds the entity ID and ordinal, plus the
before and after rows of one changed entity, so history grows with the size of the
changed entities and is independent of the rest of the graph. The journal, typed payload tables,
`entity_value`, `entity_reference`, the `dataset_part` and `dataset_process`
mirrors, and full state snapshots in history are gone. No table, history record or
archived state stores a supplied ID, because managed entities carry their managed ID.
The row codec ignores a `suppliedId` key in rows written by earlier builds.

Writes apply the difference between two in-memory states in a fixed order: insert
new rows, remove changed association, endpoint and extension rows, update
scalars, clear outgoing references of removed rows, delete descriptors, then other
core rows, then registry rows, insert same-ID kind replacements, and add rows. A
title edit changes four rows (dataset update, history insert, and separate `arc`
revision and cursor updates).
Association list edits keep the common prefix, so appending one element writes one
row.
Undo and redo apply the recorded before/after rows through the same writer and
keep list positions through ordinals.

Opening checks that every expected object exists in `sqlite_master` with
identical SQL text. Version 4 databases are rejected with a re-import hint, and
there is no migration. A conformance test compares the scoped tables with
`001_core.sql` (columns, keys, foreign keys with actions and deferrability, index
keys, CHECKs with literal case, and the view set). A two-ARC behavioral test covers
every `core_validation_errors` arm and `process_edges`. A public cross-ARC query
API is not implemented.

### Folder export and recovery

Folder operations are independent of repository opening:

- `ARC.bind(graph, folder)` binds an existing ManagedGraph and returns a workspace
  facade. `ARC.fromGraph(graph)` requires a retained binding and wraps it without
  inspecting the filesystem. Instance `bindFolder` applies the same binding policy.
- `arc.ManagedGraph` exposes the exact wrapped graph. Model, operation groups,
  history, ArcId, database path, and session-only status forward to it.
  `IsDirty`, binding, saving, and recovery belong only to the workspace facade.
- `ARC.importFolder(session, folder)` reads prototype `arc.yml` into a new bound
  entry and records its baseline. `bindFolder` establishes a destination without
  reading or writing; a new binding requires absent `arc.yml`.
- `save` requires a binding, checks external file changes,
  and exports only the graph rooted at `arc.Model`. Standalone objects and all
  history remain in SQLite. `IsDirty` and `HasSessionOnlyObjects` track different
  aspects of working state.
- `recoverFolder("auto")` resumes SQL when the file matches its baseline. If IO
  changed, automatic reload is allowed only without pending edits or standalone
  objects; otherwise it reports a conflict. Explicit `"sql"` accepts retained
  working state and the observed baseline; `"yml"` replaces the entry from IO.
- YAML replacement archives the displaced ARC's metadata and state, together with
  its history change records, inside the repository. It does not rename or replace the shared
  database, or alter other ARCs.
- `ARC.create` and `ARC.openFolder` privately own a session at
  `.arc/arc.sqlite`; closing their ARC closes that session. Convenience
  opening requires exactly one ARC with the matching folder binding.
- Closing any workspace closes its graph and invalidates every wrapper sharing
  it. Only `create` and `openFolder` also close their privately owned session;
  borrowed sessions and sibling graphs remain usable.

The current baseline compares the observed `arc.yml` text, including absence.
Save stages a replacement file and advances the SQL checkpoint only after
publication. Failed saves retain the last successful checkpoint and pending
status. Filesystem publication and SQLite commit cannot be atomic together:
publication followed by checkpoint failure requires explicit source selection
before retrying. Database-only reopening performs no filesystem reconciliation.

Managed root export uses the inline mode of the ProcessCore-shaped codec. It writes
the managed ID of every entity, repeats an entity with all its fields at each
occurrence, and writes a cycle as an `{"@id": id}` reference. It preserves
extensions and numeric/text distinctions. It quotes Annotation values, extension text,
`intendedUse` text and ambiguous extension keys, quotes core text scalars (IDs
included) that are empty, read as a null or Boolean word of YAML 1.1 or 1.2, or start
like a number, and uses
the standard binary tag URI. This codec contract does not change normative YAML
schemas.

### Detached YAML and managed import

`DatasetYaml.encode(dataset)`, `DatasetYaml.encodeIndexed(dataset)` and
`DatasetYaml.decode(yaml)` are attached public APIs independent of SQL, filesystem,
or ownership. They share `Serialization/ProcessCoreYaml.fs`, which works on state
rows and takes its wire names from the typed property table. The
[management guide](../docs/project/arc-management.md#yaml-representation) holds the
mapping table and the rules. Capture uses object reference identity with
codec-local keys, and a codec-local map records which objects carry an ID. Encode
never modifies the input graph. Decode allocates fresh objects, links cycles through
internal state assignment, and keeps supplied IDs without assigning any.

The codec treats equal `id` values in one document as one entity and fails on
differing definitions. Inline mode repeats every occurrence in full and writes
`{"@id": id}` only for a cycle. Indexed mode moves Annotations, Recipes and Agents that
have IDs into the root sections `annotations`, `recipes` and `agentIndex`. Entities
without IDs are written inline at each occurrence, so their sharing is not
preserved, except for the common fields of a grouped process entry, and a cycle
among them fails on encode. Consecutive processes with equal
non-endpoint row data share one entry with aligned `inputs` and `outputs` arrays. The
decoder accepts the ProcessCore aliases and the lenient forms listed in the guide.
The `$ref` and anchor readers are removed and there is no migration.

`Session.importYaml(yaml)` decodes a detached Dataset, then applies ordinary
management adoption: assign missing IDs, reject distinct-object ID collisions,
and create a fresh unbound entry with empty history. The decoder returns one object
per ID, so a document that repeats an entity inline imports and a document that
repeats an ID with differing fields fails in decode. Workspace imports use a
shared path-independent factory to initialize the graph, binding, and baseline
transactionally. `ManagedGraph.toYaml()` and the workspace save write the inline
mode without changing checkpoints or history. Snapshot codecs remain private.

### Portability and layout

The implementation lives in [src/ARCManagement](../src/ARCManagement/):

| Area | Responsibility |
|---|---|
| Generic repository and public Session | Connection ownership, repository lifecycle, metadata, and graph factories. |
| `Runtime/Session.fs`, `ArcFactory.fs` | ARC context with cached stored rows, revision protocol, identity assignment, hydration, and history. |
| `ARCtrl/` | Workspace ARC, binding, export, baselines, recovery, fixed layout paths, and workspace-specific identifier/path helpers. |
| `Operations/`, `ManagedGraph.fs` | Generic attached facade and operation groups. |
| `Internal/`, `Serialization/`, `Storage/` | Model capture/validation, YAML codecs, and the SQLite core-profile schema and diff writer. |
| `Helper/` | Generic portable filesystem adapters, UUID generation, collections, parsing, and async utilities. |

Generic native entrypoints are `arc-management` and `arc_management`; workspace
ARC is exported from `arc-management/arctrl` and `arc_management.arctrl`.
The single project/assembly is `ARCManagement`; generic facades compile before
workspace code, so `ARC.importFolder(session: Session, folder)` is typed directly.
Domain classes come from
`arc-base-model` / `arc_base_model`. Staging keeps shared class definitions and
curated exports/declarations. JavaScript entrypoints build together with shared
chunks; Python uses one implementation tree and emitted snake_case method names.
No legacy project/package aliases remain.
Public callers need neither generated implementation imports nor Fable runtime
helpers. Management consumers apply the existing model Python numeric
compatibility pass; PolyglotSQLite retains its own representation boundaries.
Keep platform adapters explicit and preserve the pinned toolchain.

## Remaining work

| Milestone | State | Completion criteria |
|---|---|---|
| Repository, full-model operations, extensions, reversible history, and prototype folder IO | Implemented | Preserve the contracts above and their shared/native regression coverage. |
| Separate generic management and workspace APIs | Implemented and verified | Renamed project, tests, targets, and packages; verified exact graph delegation, ownership, both native entrypoints, and class identity. |
| Detached YAML and public text workflows | Implemented and verified | ProcessCore-shaped codec with inline and indexed modes: IDs, shared and cyclic entities, grouped processes, one-or-many keys, type aliases, extensions, ProcessCore and example documents, conflicts, managed adoption, and checkpoint-free export. |
| Verify the current public API | Passed | `TestARCManagement` and `TestBaseModel`, including declarations and staged/packed consumers. |
| Repository-wide verification | Passed | `BuildSolution` and `RunTests` completed after the solution-path repair; existing ignored/disabled legacy cases are recorded below. |
| Immutable model and simplified management | Implemented | Immutable Layer 1, the core-shaped schema version 5 with delta history, the cached-state runtime with the revision protocol, the trimmed operation groups, the ProcessCore-shaped YAML codec and the cross-runtime database test are done. See the [immutable redesign](mutation_safety_immutable.md). |
| Project-file workspace integration | Planned | Resolve `.arc/project.yml`, profiles, rules, and exact codec IDs; prepare root/child bindings and auxiliary outputs before IO. |
| Project-backed save and recovery | Planned | Export/reload selected resources with conflict checks, per-ARC archives, retained history/session-only objects, and failed-write checkpoint coverage. |
| Project-backed native API and documentation | Planned | Run shared and handwritten consumers for the new lifecycle and document its supported guarantees. |

Project-file integration must follow the
[specification](../docs/spec/project_file.md) and
[handling plan](project_file_handling.md). Replace the single-file prototype
boundary without reverting to single-ARC connection ownership or whole-database
archives. Prepare paths, codec inputs, external-child bindings, and collision
checks before writes. Re-resolve configuration before save; do not implicitly
rewrite project/profile documents or dynamically load executable codecs.

Preserve the existing source-selection policy while expanding baselines to the
project, resolved profiles/rules, managed resources, and relevant absence.
Explicit reload/discard must archive only the selected ARC before replacing its
state and history. Export only the root graph through its prepared bindings;
standalone objects remain SQL-only. Failed multi-resource writes must not advance
the successful checkpoint or silently resolve conflicts by timestamps.

Decisions still needed for that work:

1. The fingerprint/checkpoint representation for multiple managed resources.
2. Filesystem staging and recovery guarantees when only some writes succeed.
3. Whether workspace IO stays synchronous or gains explicit native async facades.
Direct mutation is resolved by the [immutable redesign](mutation_safety_immutable.md),
which also makes SQL writes and history incremental and adopts the ProcessCore
YAML shape that the project-file `dataset.yml` codec will reuse.

Cross-ARC search and legacy migrations are separate future decisions. Shared mutable entities, selective undo, collaborative editing,
automatic persistence of direct mutations, browser SQLite, and remote database
servers remain outside this plan. New production dependencies require approval.

## Verification and completion criteria

Storage verification (2026-10-22, after the review fixes): `RunTests` passed with FAKE `Status: Ok`. `TestARCManagement` ran 155/155 shared tests on each of .NET, JavaScript and Python, plus the handwritten native consumers and the locally packed npm and Python artifacts. `TestBaseModel` ran 27/27 shared tests on each runtime with native and packed checks. PolyglotSQLite passed 41 .NET, 36 JavaScript and 36 Python cases, its native and packed checks, and all nine cross-runtime database writer/reader combinations. Core SQL passed 63 cases each on Node and Python. ProcessCore passed 659 JavaScript and 660 .NET cases, with two ignored cases on each runtime. The legacy ProcessCore `runTestsPy` target remains excluded by the aggregate.

Runtime and API verification (2026-10-22): `RunTests` passed with FAKE `Status: Ok`. `TestARCManagement` ran 156/156 shared tests on each of .NET, JavaScript and Python, plus the native and packed checks. `TestBaseModel` ran 27/27 per runtime.

YAML verification (2026-10-22): `TestARCManagement` passed with FAKE `Status: Ok`. It ran 178/178 shared tests on each of .NET, JavaScript and Python, plus the handwritten native consumers and the locally packed npm and Python artifacts. The YAML tests read the example documents and the ProcessCore-generated fixtures from the repository root on all three runtimes. `TestBaseModel` ran 27/27 per runtime with native and packed checks, and `TestCoreSQL` passed 178/178 on the .NET target. `RunTests` then passed with FAKE `Status: Ok`, including PolyglotSQLite, Core SQL on Node and Python, and ProcessCore (659 JavaScript and 660 .NET cases, two ignored on each).

Close-out verification (2026-10-22): `RunTests` passed with FAKE `Status: Ok`. `TestARCManagement` ran 178/178 shared tests on each of .NET, JavaScript and Python, plus the handwritten native consumers and the locally packed npm and Python artifacts. The new `TestARCManagementInterop` wrote one version 5 fixture per runtime and read each with all three runtimes (nine combinations), checking both entries, their history flags and a redo and undo on a copy. `TestBaseModel` ran 27/27 per runtime, `TestCoreSQL` passed 63 cases each on Node and Python, and ProcessCore passed 659 JavaScript and 660 .NET cases with two ignored on each. `BuildDocs` passed.

Historical baseline: before the `add` rename and project/API separation, the
former management aggregate passed:
**127/127 shared tests on each of .NET, JavaScript, and Python**,
plus handwritten native consumers, declaration checks, and locally packed npm
and Python artifacts. After earlier aggregate-build repairs, the recorded
`TestCoreSQL` run passed 63 cases on both Node and Python. These historical
results are superseded by the current management checks above.

Use the existing targets:

```powershell
.\build.cmd TestARCManagement
.\build.cmd TestBaseModel
.\build.cmd RunTests
```

`TestARCManagement` aggregates `TestARCManagementDotNet`, `TestARCManagementJS`,
`TestARCManagementPy`, and `TestARCManagementNative`; it is included in `RunTests`.
`TestCoreSQL` depends on `TestARCManagementDotNet`; the duplicate
`TestManagementPrototype` target is removed. The consolidated
[shared tests](../tests/ARCManagement.Tests/Main.fs) cover helpers,
behavior, core SQL, full-model operations, and the multi-ARC toolbox. Only the
demo walkthrough is .NET-only. Compilation or skipped execution is not a pass.

For implementation changes, retain coverage for:

- All entity properties and alternatives, ID assignment/collisions/undo,
  upserts, shared references, cycles, collection order, and duplicates.
- Stored state after edits, undo/redo, reopen, refresh from other connections,
  and injected failures. Unchanged returned values and package class identity.
- Memory/file parity, on-demand hydration, per-ARC constraints,
  history/revision isolation, and ARC/session closure.
- Workspace wrappers reuse the exact graph and operation groups. Graph closure invalidates wrappers while borrowed sessions/siblings survive.
- Detached YAML absent IDs, equal IDs meaning one entity, `@id` cycles, process
  grouping, one-or-many keys, aliases, extensions, numeric/text distinctions,
  the example and ProcessCore documents, and failures.
- YAML imports assign IDs and reject collisions; text export remains root-only
  and does not advance checkpoints or history.
- Root-only export, standalone-object retention, source preferences, unavailable
  sources, conflicts, per-ARC archives, and publication/checkpoint failures.
- Handwritten JS/TS and Python callers through public entrypoints, strict
  declarations, native numeric boundaries, and locally packed artifacts.

Project integration additionally requires root/external-child codec round trips,
profile/rule re-resolution, auxiliary-output validation, path collisions,
configuration/resource changes, and partial-write recovery on every runtime.
Update milestone states with checks actually run; keep detailed implementation
and error history in version control rather than appending logs here.
