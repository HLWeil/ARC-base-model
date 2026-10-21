# ARC Management API

Status: generic ARCManagement and workspace-specific ARCtrl are separated and
verified, including detached YAML workflows and renamed native packages.
`BuildSolution` and repository-wide `RunTests` passed. Project-file codec
integration remains planned. The accepted
[immutable redesign](mutation_safety_immutable.md) will replace the mutable
model, live projections, storage layout and managed YAML format described below.
This document describes the current implementation until its phases land.

This plan tracks the current implementation and remaining work. See the
[management guide](../docs/project/arc-management.md) and
[ARCtrl workspace guide](../docs/project/arctrl.md) for usage and storage details, and
[the project-file specification](../docs/spec/project_file.md) for the intended
workspace format.

## Objective and boundaries

Provide a portable ARC management layer over `ARCBaseModel` with fine-grained
operations, stable object identity, reversible history, and retained SQLite
working state on .NET, JavaScript/TypeScript, and Python.

The responsibilities are:

| Component | Responsibility |
|---|---|
| `ARCBaseModel` | Mutable domain objects without storage, identity assignment, graph management, or filesystem policy. |
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
graph.Process.setInputSample(process, sample) |> ignore
graph.Dataset.addProcess(graph.Model, process) |> ignore
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
  rejected. `openFile` requires a valid schema-version-4 repository and checks
  columns, keys, and relationship constraints. It neither initializes nor
  migrates an existing database.
- `createArc` adopts a supplied graph. Its generated repository `ArcId` is
  independent of the root Dataset ID. Domain IDs are scoped to one ARC, so
  different ARCs can contain equal IDs without sharing objects.
- Opening a repository and listing `ArcInfo` metadata do not hydrate graphs or
  read folders. `openArc` loads only the selected entry and returns its existing
  ManagedGraph and objects until that graph closes.
- Closing a graph releases its context; reopening creates a fresh projection.
  Closing the session invalidates all its handles and releases its connection.
  In-memory storage lasts for that connection. Neither close exports implicitly.
- Shared mutable entities across live ARCs are rejected, including across
  separate repositories. Connection access is synchronous and serialized.

### Entity operations and identity

All thirteen model entity types have operation groups, alongside generic
`Entity` and `History`. Entity groups expose `create`, `add`, `get`, `list`,
`upsert`, and `delete`, plus property setters, optional-value clear methods,
collection replacement, and typed relationship helpers.

- `add(value)` returns the supplied object and recursively adopts its referenced
  graph. It validates the state, assigns omitted IDs, preserves supplied IDs,
  and rejects collisions with distinct instances. Adding the same active
  instance again is a no-op. Registration does not attach an object to the root.
- `upsert(value): unit` uses a detached input for full replacement by ID. An
  existing same-type ID updates the retained canonical instance and replaces its
  collection containers. Empty collections clear values. Unknown IDs adopt the graph.
  Cross-type collisions are rejected. References to managed objects must use
  their registered instances.
- ID assignment belongs to this management layer, never Layer 1. Identity is
  not inferred from names, paths, or equal values. Undo of a new registration
  restores originally omitted IDs; redo restores the assigned IDs.
- Collection edits preserve order, duplicates, and shared references. Removal
  deletes one occurrence. Removing a relationship does not unregister its
  target. Move helpers require at most one membership; replacement setters
  handle shared or repeated members.
- Process endpoints are optional and singular. Typed helpers such as
  `setInputSample`, `setOutputData`, and `clearInput` avoid native union wrappers.
  Other alternatives distinguish text/numbers and entity/URL values, including
  absence, empty text, zero, and fractional numbers.
- Root deletion is rejected. Dataset deletion cascades only to descendants and
  processes without surviving owners. Required Descriptor targets and surviving
  extension references prevent target deletion; undo restores affected links.

`graph.Entity` also manages generic typed objects and extension properties on all
core classes. Typed helpers cover text, finite binary64 numbers, Boolean, null,
canonical base64 blobs, object references, and nested ordered collections.
Missing properties, explicit null, and empty collections remain distinct.
Object cycles are supported; self-containing collection containers are rejected.
Core property names are reserved. Extension changes use the same command,
validation, SQL, and history pipeline as core edits.

### Commands, history, and consistency

Managed edits validate a planned snapshot, then write the difference between the
stored and the new state, the history record, the cursor, and the ARC revision
transactionally. The live projection follows. Failures roll back SQL and restore
the previous projection. Fine-grained mutations return attached
`AppliedOperation` values.

Undo/redo is linear and persisted as recorded before/after rows applied through
the same writer. A new edit after
undo invalidates the redo branch. Hydration and history restoration keep
canonical instances and shared or recursive relationships, and replace
collection containers. Saving preserves history and standalone registered
objects.

Direct property or collection edits on managed objects are unsupported. The
implementation compares the projection with its retained snapshot before
commands and saves, rejecting detected changes. Callers can close and reopen
the ARC to restore committed state. This detection is implemented. The
[immutable redesign](mutation_safety_immutable.md) replaces it by making model
values immutable.

Revisions are ARC-local. Edits to another ARC do not invalidate a handle; stale
writes to the same ARC through another connection require close and reopen.
SQLite still permits only one writer; this is not collaborative editing.

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
mirrors, and full state snapshots in history are gone. The registry and core
tables do not store `SuppliedId`. History change records and archived state do.

Writes apply the difference between two in-memory states in a fixed order: insert
new rows, remove changed association, endpoint and extension rows, update
scalars, clear outgoing references of removed rows, delete descriptors, then other
core rows, then registry rows, insert same-ID kind replacements, and add rows. A
title edit writes three rows (dataset update, history insert, `arc` update).
Association list edits keep the common prefix, so appending one element writes one
row.
Undo and redo apply the recorded before/after rows through the same writer and
keep list positions through ordinals.

Opening checks that every expected object exists in `sqlite_master` with
identical SQL text. Version 4 databases are rejected with a re-import hint, and
there is no migration. A conformance test compares the scoped tables with
`001_core.sql` (columns, keys, foreign keys with actions and deferrability, index
keys, CHECKs with literal case, and the view set). A two-ARC behavioral test covers
every `core_validation_errors` arm and `process_edges`. The runtime still keeps canonical live instances and rejects a stale
handle. Automatic refresh comes with the runtime phase. A public cross-ARC query
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
- `save` requires a binding, checks direct mutation and external file changes,
  and exports only the graph rooted at `arc.Model`. Standalone objects and all
  history remain in SQLite. `IsDirty` and `HasSessionOnlyObjects` track different
  aspects of working state.
- `recoverFolder("auto")` resumes SQL when the file matches its baseline. If IO
  changed, automatic reload is allowed only without pending edits or standalone
  objects; otherwise it reports a conflict. Explicit `"sql"` accepts retained
  working state and the observed baseline; `"yml"` replaces the entry from IO.
- YAML replacement archives only the displaced ARC's metadata, state, and
  history change records inside the repository. It does not rename or replace the shared
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

Managed root export defines shared/circular entities once and uses `$ref`
references. It preserves extensions and numeric/text distinctions, emits safe
keys and numeric scalars plainly, quotes ambiguous keys and text, and uses the
standard binary tag URI. Older scalar tag forms remain readable. This codec
contract does not change normative YAML schemas.

### Detached YAML and managed import

`DatasetYaml.encode(dataset)` and `DatasetYaml.decode(yaml)` are attached public
APIs independent of SQL, filesystem, or ownership. Capture uses object reference
identity and serialization-local keys. Encode emits deterministic anchors and
aliases for sharing/cycles and emits IDs only when supplied, without modifying
the input graph. Decode hydrates references before restoring supplied/absent IDs;
it preserves distinct objects with equal IDs, extensions, scalar alternatives,
collection order, and duplicates.

Documents containing anchors or aliases use local-reference decoding. Register
entity anchors before properties; reject duplicate anchors, unresolved/forward
aliases, and mixed legacy references. The legacy `id`/`$ref` format remains
readable; a literal `$ref` extension on a typed entity remains supported.

`Session.importYaml(yaml)` decodes detached objects, then applies ordinary
management adoption: assign missing IDs, reject distinct-object ID collisions,
and create a fresh unbound entry with empty history. Workspace imports use a
shared path-independent factory to initialize the graph, binding, and baseline
transactionally. `ManagedGraph.toYaml()` retains the root-only legacy output
without changing checkpoints or history. Snapshot codecs remain private.

### Portability and layout

The implementation lives in [src/ARCManagement](../src/ARCManagement/):

| Area | Responsibility |
|---|---|
| Generic repository and public Session | Connection ownership, repository lifecycle, metadata, and graph factories. |
| `Runtime/Session.fs`, `ArcFactory.fs` | ARC context, snapshots, identity, adoption, hydration, and history. |
| `ARCtrl/` | Workspace ARC, binding, export, baselines, recovery, fixed layout paths, and workspace-specific identifier/path helpers. |
| `Operations/`, `ManagedGraph.fs`, `AppliedOperation.fs` | Generic attached facade and operation groups. |
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
| Detached YAML and public text workflows | Implemented and verified | Verified absent/duplicate IDs, sharing/cycles, scalar types, extensions, legacy input, invalid aliases, managed adoption, and checkpoint-free export. |
| Verify the current public API | Passed | `TestARCManagement` and `TestBaseModel`, including declarations and staged/packed consumers. |
| Repository-wide verification | Passed | `BuildSolution` and `RunTests` completed after the solution-path repair; existing ignored/disabled legacy cases are recorded below. |
| Immutable model and simplified management | Phase 1 and the storage part of phases 2-3 implemented. Runtime refresh and remaining phases planned | Phase 1 (immutable Layer 1, with the transitional management adaptation) and the core-shaped schema version 5 with delta history and no journal are done. The [immutable redesign](mutation_safety_immutable.md) still plans a cached-state runtime without live projections and the revision refresh protocol, trimmed typed operation groups, and ProcessCore-shaped YAML. |
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

Storage verification (2026-10-22): `RunTests` passed with FAKE Status Ok. `TestARCManagement` ran 151/151 shared tests on .NET, JavaScript and Python plus native and packed checks before the review fixes. The review fixes add tests and are verified in the following commit.

A cross-runtime database writer/reader test for the version 5 store is planned for the close-out acceptance step.

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

- All entity properties and alternatives, ID assignment/collisions/undo, detached
  upserts, shared references, cycles, collection order, and duplicates.
- SQL/projection equivalence after edits, undo/redo, reopen, and injected
  failures; direct mutation detection and canonical package class identity.
- Memory/file parity, lazy hydration, ownership rejection, per-ARC constraints,
  history/revision isolation, and ARC/session closure.
- Workspace wrappers reuse the exact graph, operation groups, and entities;
  graph closure invalidates wrappers while borrowed sessions/siblings survive.
- Detached YAML absent IDs, equal supplied IDs on distinct objects, aliases,
  cycles, extensions, numeric/text distinctions, legacy documents, and failures.
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
