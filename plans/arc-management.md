# ARC Management API

Status: the ARCtrl management layer and multi-ARC repository are implemented.
Project-file codec integration remains planned. Verification after the public
`add(value)` rename and the full repository `RunTests` aggregate is pending.

This plan tracks the current implementation and remaining work. See the
[ARCtrl guide](../docs/project/arctrl.md) for API usage and storage details, and
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
| `ARCSession.Session` | One owned PolyglotSQLite connection, repository creation/opening, ARC enumeration, and connection lifetime. |
| `ARCtrl.ARC` | One managed graph, entity registry, operations, history, and optional folder binding/export/recovery. |
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
open ARCtrl

let session = ARCSession.Session.createInMemory()
// Alternatively: createFile("collection.sqlite") / openFile("collection.sqlite").
let arc = session.createArc(Dataset(["process-provenance"], ["example"]))
let sample = arc.Sample.create("sample")
let process = arc.Process.create("process")
arc.Process.setInputSample(process, sample) |> ignore
arc.Dataset.addProcess(arc.Model, process) |> ignore
arc.History.undo()
arc.History.redo()
arc.bindFolder("./arc-a")
arc.save()
let arcId = arc.ArcId
arc.close()
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
  facade and objects until that ARC closes.
- Closing an ARC releases its context; reopening creates a fresh projection.
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
  existing same-type ID updates the retained canonical instance and collection
  containers; empty collections clear values. Unknown IDs adopt the graph.
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

`arc.Entity` also manages generic typed objects and extension properties on all
core classes. Typed helpers cover text, finite binary64 numbers, Boolean, null,
canonical base64 blobs, object references, and nested ordered collections.
Missing properties, explicit null, and empty collections remain distinct.
Object cycles are supported; self-containing collection containers are rejected.
Core property names are reserved. Extension changes use the same command,
validation, SQL, and history pipeline as core edits.

### Commands, history, and consistency

Managed edits validate a planned snapshot, then update SQL mirrors, the live
projection, persisted history/cursor, journal, and ARC revision transactionally.
Failures roll back SQL and restore the previous projection. The current pipeline
rebuilds rows only for the affected ARC rather than issuing incremental writes.
Fine-grained mutations return attached `AppliedOperation` values.

Undo/redo is linear and persisted with before/after snapshots. A new edit after
undo invalidates the redo branch. Hydration and history restoration preserve
canonical object references, shared relationships, recursive graphs, and
collection containers. Saving preserves history and standalone registered
objects.

Direct property or collection edits on managed objects are unsupported. The
implementation compares the projection with its retained snapshot before
commands and saves, rejecting detected changes. Callers can close and reopen
the ARC to restore committed state. This detection is implemented; stronger
mutation prevention remains a separate API-design question.

Revisions are ARC-local. Edits to another ARC do not invalidate a handle; stale
writes to the same ARC through another connection require close and reopen.
SQLite still permits only one writer; this is not collaborative editing.

### SQLite storage

The management schema is owned by ARCtrl and remains separate from the normative
core SQL profile. ARCtrl uses PolyglotSQLite exclusively; provider code and
provider dependencies stay inside that library.

Schema version **4** scopes entity and typed rows by `(arc_id, id)`, ordered
values and references by ARC/owner/property/position, and extensions additionally
by nested path. Composite foreign keys enforce ARC scope for typed endpoints,
generic references, and extension references. No extra entity surrogate-key
system is required. History, journal, revisions, folder baselines, saved graphs,
and recovery archives are isolated per ARC.

Indexes support entity types, scalar property/value lookup, and reverse
references. A public cross-ARC query API and legacy schema migration are not
implemented.

### Folder export and recovery

Folder operations are independent of repository opening:

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
- YAML replacement archives only the displaced ARC's metadata, state, history,
  and journal inside the repository. It does not rename or replace the shared
  database, or alter other ARCs.
- Compatibility `ARC.create` and `ARC.openFolder` privately own a session at
  `.arc/testing.sqlite`; closing their ARC closes that session. Convenience
  opening requires exactly one ARC with the matching folder binding.

The current baseline compares the observed `arc.yml` text, including absence.
Save stages a replacement file and advances the SQL checkpoint only after
publication. Failed saves retain the last successful checkpoint and pending
status. Filesystem publication and SQLite commit cannot be atomic together:
publication followed by checkpoint failure requires explicit source selection
before retrying. Database-only reopening performs no filesystem reconciliation.

The prototype YAML codec defines shared/circular entities once and uses `$ref`
references. It preserves extensions and numeric/text distinctions, emits safe
keys and numeric scalars plainly, quotes ambiguous keys and text, and uses the
standard binary tag URI. Older scalar tag forms remain readable. This codec
contract does not change normative YAML schemas.

### Portability and layout

The implementation lives in [src/ARCtrl](../src/ARCtrl/):

| Area | Responsibility |
|---|---|
| `ARCSession/Repository.fs`, `Session.fs` | Connection ownership, repository lifecycle, public Session and metadata. |
| `Runtime/Session.fs`, `ArcFactory.fs` | ARC context, snapshots, identity, adoption, hydration, and history. |
| `Runtime/Workspace.fs` | Binding, export, baselines, and explicit folder recovery. |
| `Operations/`, `ARC.fs`, `AppliedOperation.fs` | Attached public facade and operation groups. |
| `Internal/`, `Serialization/`, `Storage/` | Model capture/validation, YAML codecs, and SQLite schema/mirrors. |
| `Helper/` | Internal portable paths, IO, IDs, collections, parsing, and async utilities. |

Native entrypoints are `arc-session` and `arc_session`; domain classes come from
`arc-base-model` / `arc_base_model`. Staging keeps shared class definitions and
curated exports/declarations. Python uses emitted snake_case method names.
Public callers need neither generated implementation imports nor Fable runtime
helpers. Management consumers apply the existing model Python numeric
compatibility pass; PolyglotSQLite retains its own representation boundaries.
Keep platform adapters explicit and preserve the pinned toolchain.

## Remaining work

| Milestone | State | Completion criteria |
|---|---|---|
| Repository, full-model operations, extensions, reversible history, and prototype folder IO | Implemented | Preserve the contracts above and their shared/native regression coverage. |
| Verify the current public API | Pending | Re-run `TestARCSession` after the `add(value)` rename, including declarations and packed consumers. |
| Repository-wide verification | Pending | Complete `RunTests` after the runtime-lockfile and solution-path repairs. |
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
4. Whether additional safeguards against direct mutation are needed beyond the
   current rejection checks, while preserving the mutable Layer 1 contract.

Cross-ARC search, incremental SQL writes, and legacy migrations are separate
future decisions. Shared mutable entities, selective undo, collaborative editing,
automatic persistence of direct mutations, browser SQLite, and remote database
servers remain outside this plan. New production dependencies require approval.

## Verification and completion criteria

The latest recorded full management run, before the `add` rename, passed
`TestARCSession`: **127/127 shared tests on each of .NET, JavaScript, and Python**,
plus handwritten native consumers, declaration checks, and locally packed npm
and Python artifacts. That is the established baseline, not verification of
subsequent changes. After aggregate-build repairs, the recorded `TestCoreSQL`
run passed 63 cases on both Node and Python; full `RunTests` remains pending.

Use the existing targets:

```powershell
.\build.cmd TestARCSession
.\build.cmd RunTests
```

`TestARCSession` aggregates `TestARCSessionDotNet`, `TestARCSessionJS`,
`TestARCSessionPy`, and `TestARCSessionNative`; it is included in `RunTests`.
`TestManagementPrototype` retains compatibility .NET coverage. The consolidated
[shared tests](../tests/ManagementPrototype.Tests/Main.fs) cover helpers,
behavior, core SQL, full-model operations, and the multi-ARC toolbox. Only the
demo walkthrough is .NET-only. Compilation or skipped execution is not a pass.

For implementation changes, retain coverage for:

- All entity properties and alternatives, ID assignment/collisions/undo, detached
  upserts, shared references, cycles, collection order, and duplicates.
- SQL/projection equivalence after edits, undo/redo, reopen, and injected
  failures; direct mutation detection and canonical package class identity.
- Memory/file parity, lazy hydration, ownership rejection, per-ARC constraints,
  history/revision isolation, and ARC/session closure.
- Root-only export, standalone-object retention, source preferences, unavailable
  sources, conflicts, per-ARC archives, and publication/checkpoint failures.
- Handwritten JS/TS and Python callers through public entrypoints, strict
  declarations, native numeric boundaries, and locally packed artifacts.

Project integration additionally requires root/external-child codec round trips,
profile/rule re-resolution, auxiliary-output validation, path collisions,
configuration/resource changes, and partial-write recovery on every runtime.
Update milestone states with checks actually run; keep detailed implementation
and error history in version control rather than appending logs here.
