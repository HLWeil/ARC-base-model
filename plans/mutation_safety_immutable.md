# Immutable model with safe, incremental persistence

Status: implemented and verified across .NET, JavaScript and Python. On 2026-10-20 the caller-managed revision API was replaced with value-based commands and explicit full-replacement baselines. Verification for that change is recorded separately below. This is an intentional breaking redesign of the experimental API and database layout. Existing databases are not supported or migrated. No schema-version mechanism, production dependency or compiler upgrade is introduced. The pinned compiler is Fable 5.20.0.

## Portable immutable model

Public domain values remain attached classes with domain-named erased alternatives. IDs remain optional in Layer 1. Constructors, copies and builders preserve reference identity, collection order, duplicates, scalar alternatives and entity cycles without identity inference.

- Publish getter-only properties and static tupled `with<Property>` / `clear<Property>` methods returning the concrete type. Scalar copies share unchanged immutable storage and references rather than traversing graphs.
- Eagerly consume external constructor/replacement iterables once. Enforce the same local invariants in constructors, copies, builder completion and hydration.
- Use read-only .NET collections, frozen JavaScript arrays and immutable Python tuples, with constant-time count/indexing for both native and transpiled F# callers. Explicit pinned-toolchain protocol adapters belong at this boundary.
- Seal every published entity, extension container, token and result, including backing storage. Ordinary assignment/deletion and collection mutation fail. Reflection and deliberate low-level runtime bypasses are outside the contract.
- Preserve canonical class definitions across packages. Internal allocation/link/seal is an explicit friend/package bridge, excluded from curated root exports and declarations.

`ModelBuilder` supports `draft(value)`, `reference(value)`, `cloneGraph(root)` and `draftFor(original)`. Cloning creates one draft per original reference and rewires every internal core/extension object reference. Typed setters/clearers validate token owner, kind and writability. `finish()` privately allocates, links, validates and seals; failure publishes nothing and remains correctable, success consumes the builder. `finish().get(token)` preserves concrete typing. SQL and YAML hydration reuse this machinery.

## Management API

| Type | Public properties |
| --- | --- |
| WriteResult<T> | Value, Changed, optional Operation |
| CommandResult | Changed, optional Operation |
| SnapshotConflictException | EntityId identifying a stale replacement baseline |
| ConcurrentWriteException | A writer committed between command preparation and its write transaction |

`read(id)` returns an optional entity; `get(id)` rejects absence. `readAll()` and `list()` return immutable collections; `Model` and `readModel()` return the Dataset. No public read/write/result carries a revision token.

Focused commands take a typed entity subject and scalar/target-ID inputs, and return the updated complete immutable value. The subject's ID identifies the current stored entity; only the requested property is changed. Its older properties are never written back, and unrelated edits do not invalidate it. Missing IDs, unknown stored subjects and wrong runtime entity kinds fail. Private provenance rejects subjects and baseline roots known to originate from another ARC, without checking their age. Detached subjects with explicit IDs select the receiving graph's stored entities. Delete takes a typed subject and applies its reference safeguards to current state. Undo/redo take no arguments and operate on current history. Dataset move commands take a typed child/process and destination Dataset. Relationship target inputs retain their validated IDs.

Keep `upsert`; do not add `replace`. Named options have equivalent semantics:

```fsharp
graph.Dataset.upsert(edited, baseline = original, recursive = true)
graph.Dataset.delete(dataset, cascade = true)
```

```javascript
graph.Dataset.upsert(edited, { baseline: original, recursive: true });
graph.Dataset.delete(dataset, { cascade: true });
```

```python
graph.Dataset.upsert(edited, baseline=original, recursive=True)
graph.Dataset.delete(dataset, cascade=True)
```

Native adapters only adapt syntax. Reject unknown names, non-Boolean flags and native positional Boolean options. Preserve one shared domain/persistence implementation.

## Adoption and write scope

- `create`/`add` strictly insert a new submitted root; an existing ID fails. New nested definitions are recursively adopted, existing nested definitions are read-only references subject to validation.
- `upsert` inserts an unknown root or fully replaces an existing same-kind root. Empty collections clear links. Unmentioned registered entities remain stored. Dataset upsert never changes the ARC's designated root.
- Full replacement requires `baseline=original` for every submitted existing writable definition. Without recursion only the baseline root definition is compared; with recursion index its reachable core/extension definitions by ID using an identity-based worklist. Baseline root kind/ID must match the submitted root. Reject distinct baseline objects sharing an ID. Compare each existing writable entity's stored local definition with its baseline, ignoring internal allocation metadata. Missing baselines fail; differences or deleted baseline entities raise `SnapshotConflictException`. Omitted baseline descendants are not replacement targets. No graph-wide revision or unrelated entity edit invalidates a matching baseline. Unknown-ID insertion omits a baseline; `add` remains strict and takes none. Recursive insertion cannot replace preexisting descendants without their baseline; use focused commands or separate child upserts before attaching them.
- Assign missing IDs only to persisted copies; supplied objects are never mutated. Changing/clearing a copied ID is insertion intent, not rename. Active IDs cannot change kind; deleted IDs may be reinserted against current-state collision checks.
- `recursive=false` by default: only the submitted root and new entities are writable. Compare every existing nested submitted definition with stored local properties, including absence, types, ordered links, duplicates and extension values; inspect descendants so changed grandchildren cannot be ignored. Reject differences with entity/property guidance. Exact references back to the root identify its replacement. Do not adopt values beneath rejected definitions.
- `recursive=true`: replace all reachable core/extension entity definitions in one operation. Traverse by reference identity; reject distinct writable objects sharing an ID. Preserve sharing/cycles and retain omitted stored objects.
- Recognize unchanged hydrated closures with private weak provenance for repository/ARC/revision. Copies lose that optimization; older/cross-ARC values undergo comparison.

A shallow copy of a cyclic value can still reference its original same-ID object; recursive upsert rejects those competing definitions. Edit such graphs with a graph clone:

```fsharp
let read = graph.readModel()
let edits = ModelBuilder()
let root = edits.cloneGraph(read)
edits.Dataset.setTitle(root, "edited")
let saved = graph.Dataset.upsert(edits.finish().get(root), baseline = read, recursive = true)
```

## Deletion

Default `cascade=false` deletes only an unreferenced target, ignoring self-links that disappear with it. Outgoing targets remain registered. ARC root deletion always fails.

With `cascade=true`, collect Dataset `hasParts`/`processes` descendants, stopping at the ARC root. Protect non-target candidates with incoming references from outside the set and propagate protection until no survivor points to another deleted descendant. The requested target remains selected. Clear optional incoming core links/remove every deleted-target collection occurrence. Surviving required core links or extension references block the entire command; callers must edit those explicitly. Shared descendants survive. One reversible operation includes all relationship changes and deletions.

## Transactions, failures and history

All public operations enter one synchronous repository gate; reentrant calls fail. Consume external iterables under that gate but before SQL transactions. Preserve provider threading constraints.

For changed writes, read a consistent current snapshot and its internal revision; compare any full-replacement baselines, compute/validate deltas and privately construct/seal the returned graph. Begin the writer transaction with a conditional revision increment for the revision just observed as its first statement and immediately check `SELECT changes()` on that connection. Persist rows, history, journal and metadata atomically. Publish results/cache only after commit.

Reject signed-int64 exhaustion. Never convert internal revisions through JS Number. A failed conditional increment raises `ConcurrentWriteException`; stale full-replacement baselines raise `SnapshotConflictException`. Both remain distinct from provider locking errors. Never retry a write automatically with newer state.

A no-op checks current state and any upsert baseline under a consistent snapshot, returns the stored value with `Changed=false`/no Operation, and changes no revision, history, journal or redo branch. Changed edits after undo discard redo. Undo/redo advance the internal revision and restore standalone registered objects as well as the root graph. Failed validation, construction, persistence or commit leaves database state unchanged.

## Storage and performance

Normalized SQL is authoritative: ARC metadata, stable ordered entity identities, property headers/scalars, ordered string/reference rows, extension trees, operation headers, reversible deltas and revision journal. Remove full `session.state`, typed payload mirrors and duplicate associations. Keep foreign keys ARC-scoped and enabled; index entity/kind/ordinal and incoming references. Ordinals survive history without renumbering unrelated entities. Normative core SQL fixtures remain separate.

History stores scalar before/after cells, ordered collection splices and changed extension subtrees. Insert/delete deltas contain enough metadata to invert exactly. Whole-list replacement uses a deterministic changed range; focused append/remove stores its exact splice. No full graph serialization occurs for ordinary edits. Validate affected properties/owners/targets, not the whole registry; full validation is reserved for bulk import/recovery.

Each public read checks SQL revision. Warm reads reuse immutable cached rows/objects. Cold reads fetch a consistent reachable closure in indexed batches, then allocate/link/seal outside the transaction. Discard hydrated caches on revision changes; never reuse parents with potentially stale descendants. Preserve identity within each result; promise none across separate reads. Values outlive graph/session closure. Use identity dictionaries and iterative worklists.

Returning an updated Dataset necessarily costs work proportional to its reachable result. That accepted output cost must not rewrite/serialize unchanged collections or unrelated entities. Dirty-state YAML comparison is lazy/cached rather than part of every setter.

## Workspaces

Retain explicit save, root-only export, fixed paths, wrapper lifetime and borrowed-session rules. Read fresh SQL state for export/dirty/bind/recovery; checkpoint and binding changes use internal revision checks. Bulk YAML recovery archives displaced entry/history while preserving siblings. Filesystem publication and SQL commit cannot be atomic together: successful publication followed by checkpoint failure requires explicit source selection.

Opening validates required tables/constraints without initialization, migration or versioning.

## Verification and delivery

Implement and verify in order: representative portable sealing/collection/generic/builder/package proof; complete immutable model and detached codecs; normalized storage and history; public management/workspaces/native options; guides and full acceptance.

Acceptance includes backing-field/collection mutation rejection; numeric/optional/Boolean boundaries; ordering/duplicates/sharing; self/mutual cycles and builder failures; nonrecursive grandchild rejection; recursive conflicts; strict insertion/root safety; all deletion protections and exact undo; older focused subjects preserving current fields; baseline requirements/staleness/descendant conflicts; internal overflow/contention; no-ops/redo; commit rollback; cross-connection freshness; snapshot lifetime; standalone history; workspace save/recovery failures; and every runtime writer/reader combination.

Run `TestBaseModel`, `TestARCManagement`, `RunTests`, native declaration/packed-artifact checks and docs checks. Record actual outcomes in the model and management plans. Skips do not pass. Add repeatable .NET/JS/Python benchmarks and structural work assertions: warm read one small revision query/no hydration; focused scalar storage/history bounded independently of unrelated graph size; title edits never rewrite unchanged collections; no-op no persistent writes; traversal proportional to inspected/returned values.

Initial immutable verification (2026-10-18): the portable representation and packed-package proof, immutable Layer 1/builder, normalized repository/history, management/workspace adapters, and documentation were complete. `RunTests` passed with exit code 0, including all 14 base-model and 127 management shared cases on each runtime, native declarations/consumers, packed artifacts and all nine management interchange combinations. The implementation plans record the full regression results and benchmark evidence. Targeted local documentation links and whitespace checks passed. `BenchmarkARCManagement` produces repeatable CSV measurements for all three runtimes; timing limits are not acceptance assertions.

`BenchmarkARCManagement` also completed successfully, and its CSV checker passed all 117 measurement rows across 100, 1,000 and 10,000 Processes on each runtime. The management plan records actual timings, work counts and the Python memory-tracing caveat.

Value-based API verification (2026-10-20): `RunTests` exited 0 in 6m 30s. All **132 management and 14 base-model cases passed on each runtime**, plus native declarations, packed consumers and all nine management interchange combinations. Focused commands preserve current unrelated fields when given older subjects. Baseline tests cover missing/stale/deleted definitions, recursive core and extension targets, newly attached existing entities, cycles, workspace saves and foreign ARC values. Native tests inject second-connection commits between preparation and writing for both a setter and undo; both reject atomically with `ConcurrentWriteException`. Existing overflow, rollback, no-op, history and deletion tests pass. See [the management plan](arc-management.md#value-based-management-api-2026-10-20) for complete regression and benchmark results.

The updated compiled benchmark runners completed at all three standard sizes, and their checker passed **117/117 work-count checks** on 2026-10-20. Scalar edits now use seven queries instead of eight because callers no longer fetch a revision. Cached-read, incremental-write/history, no-op, returned-graph hydration and no-full-registry-scan bounds all passed. Targeted local Markdown links and Git whitespace checks also passed.
