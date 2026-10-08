# ARC Management API

Status: ARC toolbox implemented; shared and native verification complete.

## Helper consolidation (2026-10-08)

Copied the ProcessCore Helper folder into internal `ARCtrl.Helper` modules and
moved `IO/FileSystem.fs` functions into `Path` and `Identifier`. Filesystem
adapters are shared by synchronous and async helpers, graph adoption, repository
lifecycle, YAML loading, folder export/recovery, and the shared test suites.
Collection restoration uses `ResizeArray.replace`; fixture directories and
portable test file operations no longer duplicate platform adapters.

Added shared helper checks for UTF-8, file/directory listings, exclusive creation,
replacement, binary values, async IO, collection reference/duplicate preservation,
IDs, paths and ontology parsing. Existing ARC behavior stays covered. ProcessCore
is unchanged; spreadsheet adapters needing FsSpreadsheet are omitted, and HTTP
helpers use platform APIs without new dependencies. CrossAsync uses F# Async
on every target, avoiding ProcessCore's extra Promise package. Copied legacy identifier
parsers are not applied to supplied domain IDs.

Python IO adapters preserve exact UTF-8 bytes (including CRLF), qualify the
built-in byte conversion to avoid caller-name shadowing, and materialize F#
arrays from native filesystem lists and bytes. These boundaries are checked by
the shared helper tests.

Verification: `TestARCSession` passed 127/127 shared tests on each of .NET,
JavaScript and Python, plus handwritten native consumers, declaration checks,
and locally packed npm/Python packages. The final aggregate completed in 8m45s.
Local documentation links and `git diff --check` passed.

## ARC toolbox refactor (2026-10-08)

The current implementation replaces single-workspace resource ownership with two
public components in the existing ARCtrl assembly:

| Component | Ownership |
|---|---|
| `ARCSession.Session` | One PolyglotSQLite connection, memory/file storage, repository schema, ARC creation/opening, lightweight metadata and connection lifetime. |
| `ARCtrl.ARC` | One working graph, identity map, entity operations, history and optional folder binding/export/recovery. |

```fsharp
let session = ARCSession.Session.createInMemory()
// Or createFile("collection.sqlite") / openFile("collection.sqlite").
let first = session.createArc(firstDataset)
let second = session.createArc(secondDataset)
first.Process.setInputSample(process, sample) |> ignore
first.History.undo()
first.bindFolder("./arc-a")
first.save()
let arcId = first.ArcId
first.close()
let reopened = session.openArc(arcId)
let entries = session.listArcs()
session.close()
```

### Implemented contracts

- `createFile` exclusively reserves a new destination; existing files are
  rejected. `openFile` requires an existing version-4 repository and validates
  columns, primary keys and relationship foreign keys without hydrating graphs.
  No legacy migration, initialization-on-open or database replacement occurs.
- `createArc` adopts a supplied graph with a generated repository `ArcId`,
  preserving domain IDs and the current missing-ID policy. Repeated `openArc`
  calls share a facade/projection until it closes. `listArcs` materializes attached
  `ArcInfo` metadata containing ArcId, root entity ID and optional folder only.
- Repository metadata scopes entities and typed rows by `(arc_id,id)`, ordered
  values/references by ARC/owner/property/position, and extensions additionally
  by path. History/journal, revision, baseline and saved graph are ARC-local;
  schema version is database-wide. Composite foreign keys enforce ARC scope for
  generic and typed endpoints, extensions and parent extension paths.
- Snapshot commands rebuild only the affected ARC's rows. Entity type, scalar
  property/value, and reverse-reference indexes retain ARC/entity keys. No
  cross-ARC search API or extra entity surrogate key is introduced.
- Each ARC retains its own objects, undo/redo stream and revision checks.
  Sharing live entities across ARCs is rejected, including across repositories.
  Other-ARC edits do not invalidate handles. Stale same-ARC writes require close
  and reopen. Connection access stays synchronous and serialized.
- Closing an ARC releases its context; closing the session invalidates every
  handle and closes the one owned connection. In-memory storage lasts for that
  connection. Neither close exports implicitly. ARCtrl and its tests exclusively
  use PolyglotSQLite; provider code remains encapsulated in that library.
- `ARC.importFolder(session,folder)` creates a bound entry and baseline from
  prototype `arc.yml`. New `bindFolder` destinations require absent `arc.yml` and
  do not load/write. `save` explicitly exports the root graph and preserves
  standalone objects and history in the database. Database-only reopening does
  no filesystem reconciliation; `recoverFolder` invokes explicit IO recovery.
- Folder source selection and external-change checks are retained. Reload
  archives only the displaced ARC's metadata/state/history/journal inside the
  repository. Shared database files are never renamed or replaced. Failed saves
  retain the previous successful SQL checkpoint and pending status; publication
  followed by checkpoint failure requires explicit source selection.
- Compatibility `ARC.create` and `ARC.openFolder` privately own a session at the
  existing `.arc/testing.sqlite` location. Their close releases that session.
  Convenience opening requires a single matching ARC rather than selecting one
  from a collection. Entity replacement uses `upsert(value)`; other public
  operation-group names and call shapes remain intact.

### Structure and verification

Latest verification: `TestARCSession` passed all 127 tests on each of .NET,
JavaScript and Python, plus native consumers, declarations and packed packages.
Behavior and SQL suites now use portable file access; no suite is excluded from
Fable builds. Explicit assertions replace empty literal-pattern success branches
that Fable Python emitted incorrectly, resolving the two failures recorded below
without changing production code. Python test output is unbuffered. Local
documentation links and `git diff --check` passed.

Entity replacement methods are now named `upsert(value)` across all 14 groups,
with callers, native declarations and documentation updated. Method bodies and
persisted history labels are unchanged. At rename verification, `TestARCSession` compiled the shared
Python suite: .NET passed 120/120, JavaScript 24/24, native/packed/declaration
checks passed, and Python passed 22/24. The aggregate failed on two separate
Boolean extension checks (`Undo bool` and `Expected Boolean`), replacing the
previous compiler block. Local documentation links and `git diff --check` passed.

`ARCSession/Internal` storage responsibilities are implemented by
`Storage/SQLiteStore.fs` and `ARCSession/Repository.fs`; `ARCSession/Session.fs`
contains the public facade and metadata. The internal ARC context remains in
`Runtime/Session.fs`. `Runtime/ArcFactory.fs` adopts/hydrates graphs and
`Runtime/Workspace.fs` handles ARC-local folders. Contexts and operation groups
compile before ARC, then the public Session. Internal factories avoid a
recursive public type group. Native entrypoints are `arc-session` and
`arc_session`, with shared model/SQLite class definitions and curated checked
declarations. The F# importFolder compile-order bridge accepts the session
through an internal owner interface; native declarations specify Session.

Added `TestARCSession` with independent .NET/JS/Python/native targets and retained
`TestManagementPrototype`. Shared tests cover identical IDs, ownership and SQL
constraint rejection, lazy hydration, history isolation, ARC closure, memory/file
parity, per-ARC archives, revisions, rollback and checkpoint failures. Full-model
tests retain cycles, duplicates, extensions and numeric alternatives.

Consolidated the test projects into `ManagementPrototype.Tests.fsproj` and one
runner; all suites are retained, with compile guards for .NET-only files.
Rechecked: `TestManagementPrototype` passed 120/120 and `TestARCSessionJS` passed
24/24. At consolidation, `TestARCSessionPy` still failed on the Fable Python `set` compiler
error described below.

Verification so far: `TestManagementPrototype` passed 119/119 before the final
shared checkpoint regression; shared JavaScript passed 23/23 before that added
case. Handwritten JavaScript/TypeScript and Python consumers passed, including
native `set(value)` calls. `TestPolyglotSQLite` passed 41 .NET / 36 JS / 36 Python
tests, native/packed/declaration checks and all nine interoperability cases.
Final results: `TestManagementPrototype` passed 120/120, including the new
shared checkpoint failure case. Final `TestARCSessionJS` passed 24/24, including
the typed numeric and repeated-folder import checks.
`TestARCSessionNative` passed with strict TypeScript checks, handwritten JS/TS
and Python consumers, and independent local npm archives/Python wheels. Native
checks include integer/fractional values, Boolean rejection, standalone objects,
cycles, duplicates, extensions, memory/file sessions, closure and reopen.
`TestBaseModel` passed 25/25 on each runtime plus native, declarations and local
archive consumers after adapting the existing numeric compatibility pass.
`TestARCSession` failed at `BuildARCSessionPyTests`; its .NET and native targets
passed, while shared JS/Python execution was skipped in that aggregate run.
Independent JS coverage is reported separately. Targeted checks passed all 12
local links in seven updated documents, and `git diff --check` passed. Existing
NuGet audit/version-constraint and YAMLicious numeric-provider warnings remain.

Before the public `upsert` rename, Fable 5.20.0's Python shared-test compiler failed
with `The input list was empty` on one-argument methods named `set`: the backend
mistakes them for two-argument collection assignment. The temporary public-method
workaround was removed at the user's request and remains removed. Library Python
compilation and handwritten native calls succeeded; shared Python compilation and
the aggregate did not pass at that point. Native checks also exposed and repaired
Python UUID formatting, root validation emission, the existing model numeric
compatibility pass for 5.20, and JavaScript BLOB buffer conversion. No new
dependency or compiler upgrade was made by this refactor.

### Follow-up boundary

The prototype still uses `arc.yml`. `.arc/project.yml` dispatch, incremental SQL
writes, legacy migrations, cross-ARC search and shared mutable entities are
outside this refactor. The earlier design below is retained as historical
project-file planning; its single-ARC ownership and whole-database archive
assumptions are superseded by the contracts above. See the updated
[project guide](../docs/project/arctrl.md) for current usage.

## YAML formatting repair (2026-10-07)

Removed blanket extension-key quoting: safe keys remain plain; unsafe, implicitly typed, and pinned-reader-special keys are quoted. Numbers now emit plain numeric scalars, Annotation/extension text uses quotes instead of explicit string tags, Boolean/null values are plain, and binary values use the valid standard tag URI. Retained reading of older scalar tag forms. Added the exact Agent/Helicopter reproduction and SQL/YAML round-trip coverage of every core class plus generic objects, numeric/text alternatives, collections, safe and unsafe keys, and binary values. Verification: `dotnet run --no-restore --project build/build.fsproj -- TestManagementPrototype` passed 109/109 tests after the final fix; targeted documentation links and `git diff --check` passed. The initial scalar-edge run exposed the pinned reader's handling of empty quoted binary values with standard URI tags; emitting canonical base64 as plain content fixes the round trip. One direct sandbox run encountered an intermittent existing filesystem replacement error; the final FAKE run passed.

## Extension session implementation (2026-10-07)

Added `ARC.Entity` for generic typed objects and session-owned extension property operations, including typed convenience methods. Snapshots/history, direct-mutation detection, reachability, deletion checks, typed SQL extension nodes, BLOB storage, and prototype YAML round trips now cover extensions. Session schema version 3 upgrades version 2 transactionally without discarding state/history; version 1 retains explicit reload behavior. Collections are ordered value containers; typed objects retain canonical identity, including cycles. Self-containing collection containers are rejected safely. Core profile DDL and derived schemas are unchanged.

Verification: `dotnet run --no-restore --project build/build.fsproj -- TestManagementPrototype` passed 107/107 tests after the final fixes, covering extension history, SQL and YAML recovery, version upgrade, failed SQL rollback, quoted reference-like keys, empty blobs, invalid numbers, reserved names, detached references, and safe rejection of collection cycles. Targeted documentation links and `git diff --check` passed. Added native JavaScript/TypeScript and Python extension consumer cases; these native session checks have not been executed and remain a separate milestone. No dependency or toolchain upgrades. Initial edge-case testing exposed the YAML reader's unquoted `$ref` key handling; snapshot and graph output now quotes extension keys. A concurrently run suite also hit an existing filesystem-test collision; the final required target ran alone and passed.

Update this document as the management API is refined and implemented. Record
the checks actually run alongside implementation milestones; a transpiled build
alone is not evidence of a usable native API.

## 1. Objective

Add a Fable-compatible ARC management layer on top of `ARCBaseModel`. The layer
provides an instantiable ARC session, fine-grained reversible operations, and a
portable SQLite-backed session store while leaving the Layer 1 model free of
storage, identity, graph-management, and filesystem policy.

The intended caller shape is:

```fsharp
let arc = ARC.openWorkspace(workspaceRoot)

arc.Process.setInputSample(process, sample)
arc.Process.setOutputData(process, data)
arc.History.undo()
arc.History.redo()

arc.save()
arc.close()
```

JavaScript and Python callers use the same object hierarchy. The apparent
nested API is implemented through attached operation-group classes returned by
the ARC session, not through F# type extensions.

## 2. Authority and layer boundaries

The three representations have distinct responsibilities:

```text
Durable workspace state
  .arc/project.yml + codec-managed filesystem resources
                         |
                         | load / save
                         v
Retained SQLite session state
  graph and registry, indexes, revision, operation journal
                         |
                         | identity map / projection
                         v
Live ARCBaseModel object graph
  application access, validation, codecs, graph algorithms
```

- The filesystem representation selected by
  [`docs/spec/project_file.md`](../docs/spec/project_file.md) is the durable
  source of truth between sessions.
- SQLite is retained session memory. It preserves working state, standalone
  registered objects, and undo/redo history across restarts, and supports
  transactions, queries, and crash recovery. The saved workspace graph can be
  reconstructed from persistent IO, but pending edits and session-only objects
  cannot. SQLite is not a project-file codec or the durable ARC representation.
- `ARCBaseModel` objects form the live, session-scoped projection used by
  application code and codecs. The identity map preserves shared object
  references within the session.
- The ARC management session coordinates these pieces. `ARCBaseModel` must not
  reference the management or SQLite libraries.

During an active session, successfully committed SQLite state is the
recoverable working state and the object graph is its synchronized projection.
Saving explicitly publishes the graph rooted at `arc.Model` through the
project-file rules and establishes a new filesystem checkpoint. Objects
outside that graph remain in the session database. Saving does not clear
undo/redo history, and closing does not implicitly save.

Distinguish changes awaiting filesystem persistence from objects retained only
in session storage. A successfully saved root graph does not imply that every
registered object has a filesystem representation.

## 3. Public API shape

Create a separate management library and namespace. Its public native-facing
surface uses `[<AttachMembers>]` classes, explicit properties, small
constructors or named factories, tupled arguments, and ordinary primitive or
`ARCBaseModel` values.

The root `ARC` class owns the session resources and caches one instance of each
operation group:

```fsharp
[<AttachMembers>]
type ARC =
    member Model: Dataset
    member Dataset: DatasetOperations
    member Process: ProcessOperations
    member History: HistoryOperations
    member Query: QueryOperations

    static member openWorkspace: workspaceRoot: string -> ARC
    member save: unit -> unit
    member discardChanges: unit -> unit
    member close: unit -> unit
```

`arc.Model` exposes the root Dataset, while `arc.Dataset` exposes Dataset
operations. Fine-grained operations accept entity objects and verify session
ownership; IDs remain the internal bridge to SQL identities. Explicit
registration brings independently created entities into the session.

The session has a top-level registry for any supported entity type, independent
of containment in the root Dataset graph. Registration alone does not attach
an object to a Dataset or create a persistent-IO representation.

Add explicit ID-based value upserts such as `arc.Sample.upsert(sample): unit`.
An unknown ID registers the supplied object; a missing ID is assigned by Layer 2.
An existing same-type ID replaces values in the registered instance, preserving
references, rather than replacing the instance. Use detached inputs for updates:
this is full value replacement (including empty collections), not a partial patch
or permission to mutate live objects directly. Validate before changes; SQL,
projection and undo/redo history advance transactionally as one command.

Opening defaults to automatic source selection and accepts an explicit
preference for SQLite session state or persistent IO, as described in section 7.

Process operations encode both endpoint direction and entity type:

```fsharp
arc.Process.setInputSample(process, sample)
arc.Process.setInputData(process, data)
arc.Process.clearInput(process)
arc.Process.setOutputSample(process, sample)
arc.Process.setOutputData(process, data)
arc.Process.clearOutput(process)
```

Endpoint setters replace the optional singular endpoint and record its complete
previous value. Typed operations keep native callers from constructing erased union
cases. Additional entity operations should follow the same fine-grained naming
pattern.

Fine-grained mutations return a small attached `AppliedOperation` value suitable
for logging and inspection. Do not expose ordinary F# unions, records, generic
`Result`, or callback-heavy command interfaces as the native public contract.

## 4. Reversible operation model

Every mutation is implemented as an internal planned command containing:

- a stable operation ID and kind;
- resolved target identities and the expected session revision;
- the complete before-image needed for reversal;
- the intended after-image;
- model apply and revert behavior; and
- SQL forward and inverse behavior.

Operation execution must:

1. resolve targets and validate all preconditions without side effects;
2. capture the complete before-image;
3. begin a `PolyglotSQLite` transaction;
4. apply the normalized SQL changes;
5. update the in-memory projection;
6. append the operation journal entry, persist the updated undo/redo position
   and invalidated redo branch, and advance the session revision;
7. commit the SQLite transaction; and
8. push the operation onto the undo stack and clear the redo stack.

On failure, roll back SQLite and revert any in-memory change already applied.
If projection repair itself fails, invalidate the current projection and
rehydrate it from the committed SQLite state before accepting more operations.

Initial undo/redo is linear and LIFO. Undo applies the recorded inverse inside
a new transaction and appends an inverse journal entry rather than deleting
history. Redo reapplies the original operation after verifying its
preconditions. Persist sufficient before/after data, operation identities, and
history position to reconstruct undo/redo after reopening; in-memory callbacks
alone are insufficient. Saving preserves this history and closing retains it
in SQLite. Arbitrary selective undo is outside the initial scope.

For `setInputSample`, the before-image distinguishes all relevant transitions:

```text
absent   -> Sample B -> undo restores absence
Sample A -> Sample B -> undo restores Sample A
Data A   -> Sample B -> undo restores Data A
```

## 5. SQLite session store

Build the ARC-specific session store on `PolyglotSQLite`; do not add ARC schema
or identity behavior to that generic library. The management layer owns:

- an ARC-aligned normalized schema and its version;
- row codecs and repositories;
- entity ID assignment and collision policy;
- an ID-keyed identity map;
- a registry of supported entities, including objects outside the root graph;
- relationship hydration;
- session revision, pending filesystem changes, and session-only object state;
- persistent-resource baselines and successful-save checkpoints;
- the reversible operation journal and persisted undo/redo position; and
- query indexes used by management operations.

The schema represents the current normative model rather than filesystem codec
layouts. In particular, Process has at most one input and one output, so a
process endpoint should be keyed by `(process_id, direction)` rather than an
unbounded position. Collection-valued model properties require ordered
association tables where order and duplicates are significant.

The existing `schemas/sql/001_core.sql` is design input but is not directly
suitable as the session schema: it contains older singular collection columns,
positional multi-endpoint process IO, and protocol-era names. Define the
session schema against the current `ARCBaseModel` and normative entity tables.

SQLite relationships use domain IDs. When an entity without an ID first enters
managed persistence, Layer 2 may assign one. It must preserve supplied IDs,
reject collisions between different objects except intentional same-type value
upserts, and never infer identity from a name, path, or other property. Upserts
reject cross-type ID collisions. ID assignment performed by a reversible
operation belongs to that operation's before/after image.

## 6. Object and row connection

There is no active-record or automatic ORM link in an `ARCBaseModel` object.
Explicit row mappers and a session identity map connect the representations.

When persistent IO is selected, workspace loading proceeds in phases:

1. Resolve the project and decode its resources into a root `Dataset` graph.
2. Archive any existing session database before creating the replacement
   session schema.
3. Walk the graph, assign required session identities, and insert scalar entity
   rows and ordered relationship rows.
4. Register each `(entity type, ID) -> object instance` in the identity map.
5. Expose the graph and operation groups through the ARC session.

When SQLite is selected, recovery proceeds in phases:

1. Decode scalar rows into one object instance per entity identity.
2. Register those instances in the identity map.
3. Resolve relationship rows in a second pass so shared references remain
   shared.
4. Assemble the root Dataset and its ordered collections, retaining standalone
   registered objects outside that graph.
5. Restore the operation journal and undo/redo position.

Mappers must explicitly handle erased alternatives such as Sample/Data and
text/number. They must use the same staged `ARCBaseModel` class definitions as
the session and its native consumers.

## 7. Workspace lifecycle

### Open

`ARC.openWorkspace` discovers exactly `.arc/project.yml` and supports automatic
selection, explicit SQLite preference, or explicit persistent-IO preference.
Persistent IO means resources selected by the project rules and their codecs;
it is not tied to a particular serialization format.

For automatic selection:

1. Resume the SQLite session when persistent resources match its recorded
   baseline, restoring its working objects and undo/redo history.
2. If persistent IO changed and the SQL session contains neither pending edits
   nor session-only objects, archive the old database and load persistent IO.
3. If external changes would displace pending edits or session-only objects,
   report a conflict and require explicit source selection.
4. If no session database exists, load persistent IO and initialize a session.

Explicit SQLite preference continues the retained working state even when
persistent IO changed. Explicit persistent-IO preference decodes the selected
resources and archives the existing database under a unique name before
initializing fresh session state and history. Neither choice performs an
implicit save or merge. The selected source must exist and be usable; an
unavailable source or incompatible session schema is an error, not permission
to silently discard retained state.

Record the observed persistent-resource baseline when accepting a source so
later saves can detect further external changes. Keep this accepted baseline
distinct from the last successful-save checkpoint. For persistent-IO loading,
resolve project profiles and rules, read declared resources, invoke the exact
codecs, and assemble the root Dataset as specified by the project-file contract.

### Edit

All supported managed mutations go through `arc.*` operations. Successful
operations update SQLite and the live projection, including independently
registered objects. Track pending filesystem changes separately from objects
retained only in session storage.

Direct mutation of the exposed Layer 1 objects cannot be intercepted portably
and is unsupported for managed state. The implementation should consider a
consistency check before save to detect accidental out-of-band mutation.

### Save

`arc.save()` is explicit. It exports only the graph rooted at `arc.Model`
through the configured project codecs. Objects outside that graph remain in
SQLite; they are not implicitly serialized, deleted, or attached to the root.
History remains available across successful saves.

Re-read and resolve the authoritative destination project file, then:

1. compare project configuration and managed resources against the accepted
   persistent-IO baseline; reject an outdated baseline before writing;
2. validate the current root graph;
3. prepare all target bindings, codec inputs, output paths, and collision
   checks before filesystem mutation;
4. invoke the configured bidirectional codecs;
5. validate codec-managed auxiliary outputs;
6. write the prepared filesystem resources, using staged replacement where
   practical; and
7. record the new successful-save checkpoint and accepted baseline, and clear
   pending filesystem changes only after the project write succeeds. Retain
   session-only objects and their separate status.

A SQLite transaction cannot make multiple filesystem writes atomic. A failed
project write must not advance the successful-save checkpoint or clear pending
filesystem changes. Earlier resources may already have changed, so do not
assume a failed write leaves the workspace untouched. The project file and
referenced profiles are not implicitly rewritten.

### Discard, close, and recovery

Explicit discard selects persistent IO: archive the current database and
initialize fresh working state and history from the workspace. Close releases
the SQLite connection explicitly on every runtime, retains session state and
history on disk, and never implicitly saves to persistent IO.

Reopening or crash recovery follows the same source-selection policy. External
workspace changes do not automatically destroy recoverable SQL edits; a
conflict requires explicit selection, and persistent-IO selection retains the
previous session in its archive.

## 8. Consistency and concurrency

Use a monotonically increasing session revision for optimistic checks between
operation planning and commit. Persistent-resource baselines and successful-save
checkpoints cover the project file, resolved profiles and rules, and managed
resources, including relevant absence, sufficiently to detect external edits
before resume or save. A source preference resolves which working state to
use; further external changes still invalidate its accepted save baseline.
The exact fingerprint algorithm and filesystem write/recovery guarantees
remain design decisions. Automatic conflicts must not be resolved by timestamps
or by silently choosing a winner.

The filesystem is the saved ARC representation, while SQLite retains the
current working session. Rebuilding from persistent IO recovers the saved root
graph, not unsaved edits or standalone session objects. Preserve the old SQL
session before replacing it. Losing its retained database and archives loses
session-only work by design.

## 9. Fable and native-package requirements

- Compile shared management and mapping logic to .NET, JavaScript, and Python.
- Use attached facade and operation-group classes instead of type extensions,
  inheritance from model entities, runtime monkeypatching, or proxy-based
  interception.
- Keep platform filesystem and lifecycle adaptation at explicit boundaries.
- Prefer synchronous SQLite operations, matching `PolyglotSQLite`.
- Review async workspace IO separately because JavaScript promises, Python
  awaitables, and .NET async values do not have one automatic native shape.
- Curate JavaScript/TypeScript and Python exports and declarations. Native
  callers must not import generated implementation modules or Fable runtime
  helpers.
- Apply the existing ARCBaseModel Python numeric compatibility pass to any
  transpiled management consumer that pattern-matches the model's numeric
  erased alternative.

## 10. Preliminary milestones

| Milestone | State | Completion evidence |
|---|---|---|
| Confirm API, authority, and lifecycle decisions | Agreed baseline | Root graph and operation-group naming, entity-object arguments, retained SQL history/registry, explicit save, source selection, and conflict/archive policy agreed; technical questions remain below. |
| Establish management library and native facade | Not started | ARC session and operation groups compile and are callable from F#, JS/TS, and Python. |
| Define ARC session schema and mappings | Not started | Current Layer 1 values round-trip with shared identity, order, duplicates, alternatives, and optional IDs. |
| Implement reversible command pipeline | Not started | Fine-grained operations, SQL rollback, model rollback, LIFO undo, redo, and history recovery across restarts pass on all runtimes. |
| Integrate project-backed open/save/discard | Not started | Project codecs export the root graph; explicit save preserves SQL-only objects/history; discard archives the replaced session. |
| Add recovery and conflict checks | Not started | Source preference, automatic conflicts, unique session archives, retained standalone objects, and failed-save checkpoints verified. |
| Complete native artifacts and documentation | Not started | Handwritten JS, TypeScript, and Python consumers pass against staged packages. |

## 11. Verification strategy

Add shared behavioral tests compiled to .NET, JavaScript, and Python, plus
handwritten native consumers. At minimum verify:

- exact `arc.Process.setInputSample(...)`-style call shapes;
- every absent/Sample/Data endpoint transition and its inverse;
- SQL and object-graph equivalence after apply, undo, redo, and failure;
- transaction rollback after SQL, mapping, journal, and commit failures;
- stable shared references through identity-map hydration;
- rejection of mutation targets belonging to a different session;
- optional ID assignment, undo, collision rejection, and no inferred IDs;
- value upsert insertion/replacement, preserved references, empty collections,
  SQL rollback, and undo/redo recovery after reopening;
- collection order and duplicate preservation;
- session rebuild from project-backed filesystem resources;
- retained session state and reconstructed undo/redo after reopening;
- saving and closing preserve history; closing does not implicitly save;
- standalone objects survive SQL recovery but remain outside root-graph export;
- pending filesystem changes and session-only objects have separate status;
- automatic SQL resume when persistent resources match the recorded baseline;
- external changes reload only when no pending edits or session-only objects
  would be displaced; otherwise automatic selection reports a conflict;
- explicit SQLite and persistent-IO preferences, including unavailable sources;
- persistent-IO selection and discard archive the old database under unique
  names before replacing session state;
- save through root and external child project bindings;
- project re-resolution, collision checks, external-change rejection, and failed
  writes preserving the previous successful-save checkpoint; and
- curated native exports, declarations, and package-level class identity.

Use fault injection around command and filesystem stages so recovery behavior is
tested rather than inferred. Add a named aggregate FAKE target for the complete
management contract and include it in `RunTests` once stable.

## 12. Open decisions

1. The persistent-resource checkpoint/fingerprint algorithm.
2. The initial validation boundary: per operation, before save, or both.
3. Whether the first release exposes synchronous workspace IO only or adds
   target-specific native async facades.
4. The precise filesystem staging and recovery guarantees for multi-resource
   project writes.
5. Optional shared per-user SQLite storage across ARCs, with keys, relationships,
   history, revisions, and checkpoints isolated by session. This enables central
   queries/backups but increases write contention and failure impact. If adopted,
   archive individual sessions rather than the whole database; filesystem saves
   stay ARC-local. Sharing mutable entities requires a separate decision.

## 13. Out of scope for the first implementation

- Changing `ARCBaseModel` entities into active records or tracked proxies.
- Treating SQLite as a project-file codec or permanent ARC serialization.
- Arbitrary selective undo or collaborative multi-writer editing.
- Automatically persisting direct property or collection mutations.
- Inferring or merging identity from names, paths, or equal field values.
- Dynamically loading executable codecs from project documents.
- Browser SQLite support, remote database servers, or new production
  dependencies without a separate decision.
