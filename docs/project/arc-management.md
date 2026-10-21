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
let inserted = graph.Sample.add(Sample("before"))
let changed = graph.Sample.setName(inserted.Value, "after")
```

`readModel()` and `Model` return the current Dataset. Entity `read(id)` returns an optional value; `get(id)` rejects absence. `readAll()` and `list()` return an immutable collection. Reads check SQL freshness internally; callers do not handle revision tokens.

A result preserves sharing/cycles within its graph. Separate reads have no object-identity guarantee. Old values remain unchanged and usable after edits, history transitions and graph/session closure. Closing a graph invalidates further operations; closing a session invalidates all its graphs.

## Focused edits and complete replacements

Focused commands take an entity value and update the current stored entity with its ID. For example, `graph.Sample.setName(sample, "after")` changes only its name. Older values remain valid subjects after other edits; their other properties are never written back. Subjects require a registered ID and the correct entity kind. Values known to have been read from another ARC are rejected. Detached values with explicit IDs select entities in the graph receiving the command.

Relationship commands also take typed objects. Collection setters accept iterables of those objects, preserving order and duplicates. Targets must already be registered; older target values link to their current stored state without replacing any target properties. Missing IDs, unknown entities, wrong kinds and known foreign-ARC values fail without changing the graph. The same rules cover single-link setters, `Descriptor.create` and object-valued extensions. `Entity.getProperty(value, key)` and `hasProperty(value, key)` also take entity subjects. `get(id)` and `read(id)` remain ID lookups.

```fsharp
let process = graph.Process.create("measurement").Value
let sample = graph.Sample.create("leaf").Value
graph.Dataset.addProcess(graph.Model, process) |> ignore
graph.Dataset.setProcesses(graph.Model, [process; process]) |> ignore
graph.Process.setInputSample(process, sample) |> ignore
graph.Process.setInput(process, EntityReference.Sample sample) |> ignore
graph.Dataset.removeProcess(graph.Model, process) |> ignore
```

JavaScript and Python pass Sample/Data objects directly to the erased alternatives:

```javascript
graph.Process.setInput(process, sample);
graph.Descriptor.create(sample);
graph.Entity.setObjectProperty(graph.Model, "process", process);
```

```python
graph.Process.set_input(process, sample)
graph.Descriptor.create(sample)
graph.Entity.set_object_property(graph.Model, "process", process)
```

Focused setters and upserts return `WriteResult` with the updated immutable `Value`, `Changed` and optional `Operation`. Deletion/undo/redo return `CommandResult` without Value. A no-op returns `Changed=false`, no Operation, and preserves internal revision/history/redo.

Collection setters replace the named collection with the supplied ordered values. Collection add/remove commands apply to its current membership, preserving other entries and duplicates according to that command's semantics.

`create`/`add` are strict insertion. `upsert` inserts an unknown root ID or fully replaces an existing same-kind entity. Replacing an existing entity requires `baseline=original`, the value observed before editing. The stored definition must still equal that baseline. Otherwise `SnapshotConflictException` identifies the conflicting `EntityId`; read current state and reapply the intended changes. Unrelated edits and workspace saves do not invalidate a baseline. Empty collections clear links. A changed/cleared copied ID is insertion intent, not rename; omit the old baseline for insertion. Dataset upsert does not change the ARC's designated root.

```javascript
const original = graph.Sample.get("s");
const edited = Sample.withName(original, "after");
const saved = graph.Sample.upsert(edited, { baseline: original });
```

By default, existing nested definitions must equal stored values, including descendants. Changed children fail instead of being silently ignored. Enable `recursive=true` to replace every supplied reachable definition:

```fsharp
graph.Dataset.upsert(edited, baseline = original, recursive = true)
```

```javascript
graph.Dataset.upsert(edited, { baseline: original, recursive: true });
```

```python
graph.Dataset.upsert(edited, baseline=original, recursive=True)
```

Recursive replacement compares every submitted existing entity against its definition in the original baseline graph, including core and extension references. Each existing writable entity must be present in that baseline; newly introduced entities need none. A missing baseline or a deleted baseline entity fails. Use focused commands to edit existing entities outside the baseline graph before attaching them.

Unknown option names, truthy substitutes and native positional Boolean options fail. Newly adopted values receive IDs in persisted copies only. Distinct writable objects sharing an ID fail. For cyclic edits use `ModelBuilder.cloneGraph`, which rewires self-references and sharing. Omitted registered objects remain stored; clearing a relationship does not delete its old target.

Commands read consistent current state, then conditionally claim its internal revision before writing. A writer racing between those steps causes `ConcurrentWriteException`, distinct from provider locking errors. Nothing from the failed command commits, and the library never retries automatically. Internal revisions use exact signed int64 arithmetic on all runtimes.

## Deletion and history

`delete(value)` deletes the current stored entity identified by the value, rejects incoming references and always protects the ARC root. Outgoing targets remain registered. Optional `cascade=true` detaches optional incoming core links and deletes exclusively contained Dataset/process descendants. Shared descendants survive. Required surviving relationships and extension references block the entire deletion until explicitly edited. See the plan for the precise preservation algorithm.

Every changed command is one atomic history operation. Operation metadata includes `Action` (`apply`, `undo`, or `redo`). `History.undo()` and `redo()` act on the current history cursor and restore exact ordering, duplicate links, IDs and standalone registrations without changing old values. A changed edit after undo discards redo; a no-op retains it.

## SQL and YAML boundaries

The experimental database uses authoritative normalized entity/property/reference/extension rows and incremental reversible history. There is no schema-version or migration mechanism. Opening validates required tables/constraints and does not initialize unrelated files. The normative core SQL fixture is separate.

Cached reads check SQL revision; remote commits invalidate hydration. Focused edits update affected cells/history rather than rewriting the registry. Returning a complete Dataset still requires materializing its reachable graph. Export and dirty checks do not run on every setter.

`DatasetYaml.encode/decode` are detached and assign no IDs; aliases preserve sharing and cycles, including distinct detached objects with equal IDs. `Session.importYaml` persists a fresh independent entry under normal collision rules. `toYaml()` exports only the reachable root, leaving standalone objects/history in SQL and changing no checkpoint.

## Native packages and verification

Generic entrypoints are `arc-management` / `arc_management`; workspace entrypoints are `arc-management/arctrl` / `arc_management.arctrl`. Model classes come from the base-model package. Python methods use snake_case; properties retain their documented names. Syntax adapters contain no domain/persistence policy.

Run `TestARCManagement`, `TestBaseModel` and `RunTests`; native declarations and consumers are checked against staged and packed artifacts. The management plan records actual verification.

Run `BenchmarkARCManagement` for repeatable .NET, JavaScript and Python measurements at 100, 1,000 and 10,000 processes. CSV files under `build/out/arc-management/benchmarks/` include cold/cached reads, focused edits, no-ops, recursive upserts, large collection appends and undo/redo. Query, row, write, history and hydration counters accompany timings. The target checks all 117 measurement rows with `node build/check-arc-benchmarks.mjs`; that command can also recheck existing artifacts. Python memory measurements enable `tracemalloc`; timings include its overhead and should be compared across sizes within that runtime. Structural tests enforce work bounds without asserting machine-dependent timing limits.
