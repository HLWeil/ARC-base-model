# ARC Management API

Status: the [immutable incremental redesign](mutation_safety_immutable.md) is implemented and verified across all runtimes (2026-10-18). It replaces the mutable canonical-object API and experimental schema-v4 descriptions below. There are no schema versions or migrations in the new layout. Earlier implementation and verification sections remain historical.

See the [management guide](../docs/project/arc-management.md) and [workspace guide](../docs/project/arctrl.md) for the current API. Project-file integration remains future work.

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

`graph.Entity` also manages generic typed objects and extension properties on all
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

The management schema is owned by ARCManagement and remains separate from the normative
core SQL profile. ARCManagement uses PolyglotSQLite exclusively; provider code and
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
- YAML replacement archives only the displaced ARC's metadata, state, history,
  and journal inside the repository. It does not rename or replace the shared
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
| `Internal/`, `Serialization/`, `Storage/` | Model capture/validation, YAML codecs, and SQLite schema/mirrors. |
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

Current separation verification (2026-10-16): `TestARCManagement` passed
**145/145 shared tests on each of .NET, JavaScript, and Python**, plus handwritten
TypeScript/JavaScript/Python consumers, declaration checks, packed npm artifacts,
and Python wheels. Emitted Python classes/stubs were inspected for leaked
internals and imprecise `Any` signatures; JavaScript declarations passed staging
checks. `TestBaseModel` passed **25/25 shared tests on each runtime**, plus native
consumers, declarations, and packed artifacts. Relative links across ten updated
Markdown documents resolve.

`BuildSolution` passed with the corrected solution path. The repository-wide
`RunTests` aggregate completed with exit code 0 and `Status: Ok`, including the
management and base-model checks above. PolyglotSQLite passed 41 .NET, 36
JavaScript, and 36 Python cases, native/packed checks, and all nine cross-runtime
database writer/reader combinations. Core SQL passed 63 cases each on Node and
Python. ProcessCore passed 659 JavaScript and 660 .NET cases, with two ignored
cases on each runtime. The legacy ProcessCore `runTestsPy` target remains excluded
by the existing aggregate; it was not run and is not passing Python evidence.

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

## Immutable incremental redesign implementation (2026-10-18)

The accepted [immutable redesign](mutation_safety_immutable.md) supersedes retained canonical-object behavior and schema-v4 descriptions above. The implementation uses immutable values, explicit revisioned writes, safe optional recursion/cascades, normalized authoritative rows and incremental history. There are no existing databases to migrate and no schema-version mechanism. Scalar and extension subtree edits write only their changed cells; result wrappers are constructed before the writer transaction. Cold reads copy consistent rows before hydration, and cached reads check SQL revision without hydration. Native options adapt syntax only; all behavior remains shared.

Verification on 2026-10-18: `RunTests` completed with exit code 0 and `Status: Ok` in 5m 40s. `TestARCManagement` passed **127/127 shared tests on each of .NET, JavaScript and Python**, with no skips or errors, plus handwritten native consumers, TypeScript declarations, packed npm artifacts and Python wheels. All **nine management database writer/reader combinations** passed. `TestBaseModel` passed 14/14 shared cases on each runtime and its native/packed checks. PolyglotSQLite passed 41 .NET and 36 cases on each native runtime, plus native/packed checks and all nine interchange combinations. Core SQL passed 63 cases on each of Node/Python. ProcessCore passed 659 JavaScript and 660 .NET cases, retaining two existing ignored cases on each; its disabled legacy Python target was not run. Earlier failed iterations are not acceptance evidence; the successful final output is `build/out/immutable-verification-complete.log`.

Structural acceptance covers one revision query per cached read with no hydration, one scalar storage/history cell beside 2,000 Process links, one changed extension subtree beside 2,000 sibling subtrees, no full-registry read on focused edits, and no persistent changes for no-ops. Safety checks cover stale/foreign/exhausted tokens, nested-definition rejection, cycles, cascade blockers/sharing, undo restoration, external commits, reentrancy, SQL/commit rollback, pre-commit result-construction failure, and workspace publication/checkpoint recovery. Targeted local Markdown links and Git whitespace checks passed; no documentation build is claimed.

### Benchmark measurements

`BenchmarkARCManagement` measures 13 operations at 100, 1,000 and 10,000 Processes, sequentially on each runtime. CSV artifacts are written to `build/out/arc-management/benchmarks/`. The measurements below are medians in milliseconds for 10,000 Processes; cached reads/pure copies use 100 repetitions, focused Sample writes five, and Dataset title writes three. Python enables `tracemalloc`, whose substantial overhead is included: compare size scaling within that runtime rather than comparing its absolute times with .NET/JavaScript.

| Runtime | Cached root read | Pure title copy | Focused Sample write | Dataset title write with complete returned graph |
| --- | ---: | ---: | ---: | ---: |
| .NET | 0.005 | 0.001 | 0.735 | 693.016 |
| JavaScript | 0.014 | 0.011 | 1.259 | 631.121 |
| Python, memory tracing enabled | 0.581 | 0.156 | 38.090 | 44,972.469 |

At all measured sizes and on all runtimes, a cached read used exactly one revision query and materialized no values. Each focused Sample/title edit wrote one normalized value/reference cell and one history change; no-op edits wrote neither. Title edits materialized the expected `Processes + 2` distinct values per result while retaining unchanged relationship rows and excluding those collections from history. Pure title copies performed no SQL or hydration. Cold reads, collection appends, recursive upserts and history timings are retained in the CSVs alongside query/row/cell/hydration counters.

Benchmark verification completed 2026-10-19: `BenchmarkARCManagement` exited 0 with `Status: Ok` (17m 02s including builds; 14m 56s of measurements). All three runtime CSVs contain all 39 measurements. `node build/check-arc-benchmarks.mjs` passed **117/117 work-count checks**, including the 10,000-Process recursive upsert, append and undo/redo cases. The build target now invokes this checker automatically; the final FAKE tooling compiled successfully with `dotnet build build/build.fsproj -c Release`. The successful benchmark log is `build/out/immutable-benchmarks.log`. Python memory tracing accounts for the long profiling run; no timing threshold is asserted.

## Value-based management API (2026-10-20)

This revision supersedes the caller-managed revision contract above. Focused setters and deletes take typed entity values and operate on current stored state. Older subjects remain usable and never write back their other fields. Known foreign-ARC subjects, wrong kinds, absent IDs and unknown entities fail. `create`/`add`, reads and history require no tokens. Reads return domain values/options/immutable collections directly; result wrappers retain `Value` (writes only), `Changed` and optional `Operation`. `RevisionToken`, `ReadResult` and the public `Revision` properties are removed.

Complete `upsert` replacements require `baseline=original` for existing writable definitions. Baseline comparison covers the original root or, with recursion, every submitted existing entity in its core/extension closure. It ignores unrelated registered entities and metadata-only workspace saves. Stale/deleted definitions raise `SnapshotConflictException` identifying the entity. Missing baselines fail with focused-command guidance. Insertion, nested-definition checking, root protection, safe cascade behavior and exact incremental history remain as specified in the [immutable plan](mutation_safety_immutable.md).

Internal exact int64 revisions still guard each writer transaction. A competing commit between preparation and writing raises `ConcurrentWriteException` without automatic retry; provider locks remain separate. The handwritten JavaScript package consumer injects real second-connection writes at that boundary for both a focused command and undo. Shared and native tests cover older subjects, stale baselines, descendant conflicts, scope/kind rejection, no-ops, rollback and workspace saves. Benchmarks call the new API and continue asserting storage/history/hydration work counts.

Verification on 2026-10-20: `RunTests` exited 0 with `Status: Ok` in 6m 30s (`build/out/api-regression-verification.log`). **132/132 management cases and 14/14 base-model cases passed on each of .NET, JavaScript and Python**, with no ignored cases in either suite. Native JS/TS/Python consumers, declaration checks, packed npm/Python artifacts and all nine management database writer/reader combinations passed. PolyglotSQLite passed its 41 .NET and 36 cases per native runtime plus packages/interchange; ProcessCore passed 660 .NET and 659 JavaScript cases with its two existing ignored cases per runtime. Core SQL passed on Node/Python. The disabled legacy ProcessCore Python suite was not run and is not passing evidence. Targeted Markdown links passed; no full documentation build is claimed. No dependency, compiler, schema or workspace path change is introduced.

Benchmark verification on 2026-10-20 also passed: the already-built .NET, JavaScript and Python runners completed sequentially at 100, 1,000 and 10,000 Processes, and `node build/check-arc-benchmarks.mjs build/out/arc-management/value-api-benchmarks` passed **117/117 work-count checks**. This used the same updated benchmark implementation and checker as `BenchmarkARCManagement`, without another package rebuild. CSVs are under `build/out/arc-management/value-api-benchmarks/`; the successful log is `build/out/value-api-benchmarks.log`.

On all runtimes and sizes, cached reads used one revision query without hydration, scalar edits wrote one storage/history cell, no-ops made no persistent writes, and no operation used a full-registry scan. Dataset title edits and recursive upserts retained unchanged collection rows and history, materializing only their returned reachable graph. Removing the caller's revision fetch reduced focused scalar queries from eight to seven per edit compared with the previous benchmark. At 10,000 Processes, scalar-edit medians were 0.630 ms (.NET), 1.490 ms (JavaScript) and 24.428 ms (Python with memory tracing); Dataset title edits including full returned-graph construction were 628.123 ms, 743.949 ms and 34,833.938 ms respectively. Python timings include `tracemalloc` overhead and are not direct runtime comparisons. No timing threshold is asserted.
