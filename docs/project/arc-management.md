---
title: ARCManagement
category: Project
categoryindex: 2
index: 10
---

# ARCManagement

ARCManagement owns SQLite connections, ARC entries, identity assignment, validation, revision checks, history and detached YAML codecs. Domain values are immutable snapshots; workspace policy lives in [ARCtrl](arctrl.md). The [implementation plan](../../plans/mutation_safety_immutable.md) specifies write and deletion scope precisely.

## Sessions and reads

`Session.createInMemory`, `createFile` and `openFile` create/open repositories. `createArc`, `importYaml` and `openArc` return `ManagedGraph`; repeated opening retains the active facade. Entries have independent `ArcId`, ID namespace, revision and history. Input values may be reused across entries because management never mutates them.

```fsharp
use session = Session.createInMemory()
let graph = session.createArc(Dataset(["process-provenance"], ["example"]))
let observed = graph.readModel()
let inserted = graph.Sample.add(observed.Revision, Sample("before"))
let changed = graph.Sample.setName(inserted.Revision, inserted.Value.Id.Value, "after")
```

`readModel`, entity `read(id)` and `readAll()` return a value and opaque `Revision`. Missing entity reads still return a revision with absent Value. `get(id)` requires existence. `Model`, `get` and `list` are convenience reads; use versioned reads when editing.

A result preserves sharing/cycles within its graph. Separate reads have no object-identity guarantee. Old values remain unchanged and usable after edits, history transitions and graph/session closure. Closing a graph invalidates further operations; closing a session invalidates all its graphs.

## Edits and revisions

Every edit takes an expected revision and IDs for its stored targets. Focused setters return `WriteResult` with the updated immutable `Value`, `Revision`, `Changed` and optional `Operation`. Deletion/undo/redo return `CommandResult` without Value. A no-op returns `Changed=false`, no Operation, and preserves revision/history/redo.

Stale or foreign tokens fail; do not refresh a token and blindly resubmit an old edit. Read current state and reapply intent. Revisions remain exact opaque strings across runtimes; SQLite locking failures are distinct from revision conflicts.

`create`/`add` are strict insertion. `upsert` inserts an unknown root ID or fully replaces an existing same-kind entity. Empty collections clear links. A changed/cleared copied ID is insertion intent, not rename. Dataset upsert does not change the ARC's designated root.

```javascript
const read = graph.Sample.read("s");
const edited = Sample.withName(read.Value, "after");
const saved = graph.Sample.upsert(read.Revision, edited);
```

By default, existing nested definitions must equal stored values, including descendants. Changed children fail instead of being silently ignored. Enable `recursive=true` to replace every supplied reachable definition:

```fsharp
graph.Dataset.upsert(read.Revision, edited, recursive = true)
```

```javascript
graph.Dataset.upsert(read.Revision, edited, { recursive: true });
```

```python
graph.Dataset.upsert(read.Revision, edited, recursive=True)
```

Unknown option names, truthy substitutes and native positional Boolean options fail. Newly adopted values receive IDs in persisted copies only. Distinct writable objects sharing an ID fail. For cyclic edits use `ModelBuilder.cloneGraph`, which rewires self-references and sharing. Omitted registered objects remain stored; clearing a relationship does not delete its old target.

## Deletion and history

`delete(revision, id)` rejects incoming references and always protects the ARC root. Outgoing targets remain registered. Optional `cascade=true` detaches optional incoming core links and deletes exclusively contained Dataset/process descendants. Shared descendants survive. Required surviving relationships and extension references block the entire deletion until explicitly edited. See the plan for the precise preservation algorithm.

Every changed command is one atomic history operation. Operation metadata includes `Action` (`apply`, `undo`, or `redo`). Undo/redo require current revision tokens, advance revision, and restore exact ordering, duplicate links, IDs and standalone registrations without changing old values. A changed edit after undo discards redo; a no-op retains it.

## SQL and YAML boundaries

The experimental database uses authoritative normalized entity/property/reference/extension rows and incremental reversible history. There is no schema-version or migration mechanism. Opening validates required tables/constraints and does not initialize unrelated files. The normative core SQL fixture is separate.

Cached reads check SQL revision; remote commits invalidate hydration. Focused edits update affected cells/history rather than rewriting the registry. Returning a complete Dataset still requires materializing its reachable graph. Export and dirty checks do not run on every setter.

`DatasetYaml.encode/decode` are detached and assign no IDs; aliases preserve sharing and cycles, including distinct detached objects with equal IDs. `Session.importYaml` persists a fresh independent entry under normal collision rules. `toYaml()` exports only the reachable root, leaving standalone objects/history in SQL and changing no checkpoint.

## Native packages and verification

Generic entrypoints are `arc-management` / `arc_management`; workspace entrypoints are `arc-management/arctrl` / `arc_management.arctrl`. Model classes come from the base-model package. Python methods use snake_case; properties retain their documented names. Syntax adapters contain no domain/persistence policy.

Run `TestARCManagement`, `TestBaseModel` and `RunTests`; native declarations and consumers are checked against staged and packed artifacts. The management plan records actual verification.

Run `BenchmarkARCManagement` for repeatable .NET, JavaScript and Python measurements at 100, 1,000 and 10,000 processes. CSV files under `build/out/arc-management/benchmarks/` include cold/cached reads, focused edits, no-ops, recursive upserts, large collection appends and undo/redo. Query, row, write, history and hydration counters accompany timings. The target checks all 117 measurement rows with `node build/check-arc-benchmarks.mjs`; that command can also recheck existing artifacts. Python memory measurements enable `tracemalloc`; timings include its overhead and should be compared across sizes within that runtime. Structural tests enforce work bounds without asserting machine-dependent timing limits.
