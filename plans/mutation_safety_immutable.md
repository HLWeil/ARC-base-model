# Immutable ARCBaseModel with SQL-backed management

## Summary

Replace mutable domain objects with immutable attached classes. ARCManagement remains responsible for current SQLite state, validation, revisions and persisted history. Reads return immutable values representing the database state observed by that read.

This is a breaking API change. Preserve the existing package names, specification vocabulary, SQL schema version 4, YAML formats and history data. Add no production dependencies or compiler upgrade. Public management batches remain outside this change.

## Immutable model and graph construction

- Keep the existing entity classes, common `EntityObject` base and erased alternatives. Preserve optional IDs, reference identity, ordering, duplicates and all specified value alternatives.
- Make every public property getter-only. Add tupled static `with<Property>(value, replacement)` methods returning the same concrete entity type, plus `clear<Property>(value)` for optional properties. Include common properties and extension operations.
- Copy methods preserve unchanged entity references. They do not assign IDs, rewrite incoming references or persist anything. Copying a self-referencing object retains references to the original object unless explicitly rebuilt.
- Expose collection properties as `seq<T>` backed by owned immutable storage: read-only containers on .NET, frozen arrays in JavaScript, and tuples in Python. Accept ordinary native iterables as construction/copy inputs and consume them eagerly.
- Replace mutable extension bags and `EntityCollection` operations with immutable storage and static copy operations. Preserve reserved-name checks and rejection of collection-container cycles.
- Enforce runtime immutability on every published model value. Freeze JavaScript instances and owned containers after derived initialization; guard Python attribute assignment/deletion after construction. Keep deliberate Python low-level bypasses outside the supported contract.
- Preserve short constructors. Use private construction machinery and thin native constructor adapters where necessary, retaining the same underlying entity classes for erased-union matching.

Add a typed `ModelBuilder` for detached cyclic construction:

```fsharp
let builder = ModelBuilder()
let root = builder.draft(Dataset(["process-provenance"], ["example"]))
builder.Dataset.setHasParts(root, [root])

let built = builder.finish()
let dataset = built.get(root)
```

- `draft(value)` creates a distinct future entity, retaining prototype values and relationships until explicitly replaced. `reference(value)` supplies a read-only token for an existing immutable entity.
- Builder relationship setters accept typed tokens and reject foreign-builder tokens, wrong entity kinds and edits to reference tokens.
- Provide immutable builder-only `ModelValue` factories for primitives, object tokens and nested extension collections.
- `finish()` validates definitions, then allocates, links and seals unpublished objects without invoking application callbacks. Successful completion consumes the builder; failed completion publishes nothing and permits correction.
- Reuse the private allocation/link/seal mechanism for SQL and YAML hydration. Preserve sharing, arbitrary entity cycles, absent IDs and distinct detached objects with equal IDs.

## SQL reads, writes and public API

Introduce attached result classes:

- `RevisionToken`: internally constructed, ARC-scoped, with an exact opaque string value.
- `ReadResult<T>`: `Value` and `Revision`.
- `WriteResult<T>`: `Value`, `Revision` and `Operation`.
- `CommandResult`: `Revision` and `Operation`, for deletion and history transitions.

The primary workflow becomes:

```javascript
const read = graph.Sample.read("s1");
const edited = Sample.withName(read.Value, "B");
const saved = graph.Sample.upsert(read.Revision, edited);
```

**Reads**

- Keep `Model`, `get(id)` and `list()` as convenient current-state reads. Add `readModel()`, `read(id)` and `readAll()` returning versioned results.
- Load persisted state and revision together on every operation. Materialize each read consistently, using a temporary identity map to preserve sharing and cycles within its result.
- Remove retained canonical entities, tombstones, object-ownership registration and direct-mutation scans. Retain no additional long-lived graph snapshot for undo/redo.
- Promise no reference identity across separate reads. Returned values remain readable and unchanged after later edits, undo/redo, graph closure or session closure.

**Writes**

- Require the expected revision as the first argument to graph-edit and history methods.
- Keep focused operations, targeting IDs: for example, `setName(revision, sampleId, name)` and `setInputSample(revision, processId, sampleId)`.
- Make `create`, `add`, `upsert` and focused edits return committed values through `WriteResult<T>`. Deletion and undo/redo return `CommandResult`.
- `create`/`add` are strict insertion operations. `upsert` fully replaces the submitted root entity; empty collections clear existing values.
- Existing nested IDs resolve as references in the destination ARC and do not overwrite their targets. Recursively adopt new referenced entities, assigning omitted IDs only to newly constructed persisted copies. Reject conflicting new definitions and cross-type collisions.
- Permit immutable input values to be reused across ARCs. The destination graph determines the ID namespace.
- Advance revision and record one history operation for every successful edit command, including identical-value replacements.

**Transactions and history**

- Serialize connection access. Begin the write transaction with an atomic revision comparison/update; reject mismatches with a structured revision-conflict error containing expected/current tokens.
- Validate and persist state, mirrors, history, journal and revision in that transaction. Construct and seal the returned value before commit; expose success only after commit.
- Failures leave SQL, history, revision and supplied objects unchanged. Keep provider locking errors distinguishable from revision conflicts.
- Reuse existing persisted history for undo/redo. Restore database state without changing previously returned objects. Distinguish apply, undo and redo in returned operation metadata.
- New-entry creation/import requires no expected revision. Workspace save/bind/recovery operate on freshly read SQL state with internal revision checks, retaining their existing public call shapes.

## Integration and documentation

- Keep ARCtrl forwarding operations to its exact `ManagedGraph`. Make export, dirty status, checkpoints and recovery use fresh SQL state while preserving existing filesystem conflict and failed-save guarantees.
- Preserve detached YAML anchor/alias behavior and managed root-only exports. Preserve existing schema-v4 databases and historical before/after payloads.
- Update native staging and declarations together: read-only TypeScript fields/arrays, precise Python tuple annotations, result wrappers, builder exports and static copy methods.
- Preserve shared model class identity across packages and the existing Python numeric compatibility pass. Do not patch the shared Fable runtime.
- Update repository instructions, the Layer 1 and management plans, canonical guides and native examples. Document copy versus persistence, full replacement, stale-write recovery, read-result lifetime and shallow reference preservation.

## Verification and acceptance

First verify the construction/sealing and collection representations with the pinned compiler before applying them across every entity.

Acceptance coverage must include:

- Getter-only APIs and rejection of ordinary native field, backing-field and collection mutation.
- Pure static copies, optional-value clearing, native numeric alternatives, duplicates, ordering and unchanged references.
- Builder-created self/mutual cycles, extension collections, token validation, failed construction and immutable YAML/SQL hydration.
- Inputs retaining absent IDs while successful results contain assigned IDs.
- Old values remaining unchanged; fresh reads reflecting edits, undo/redo and other connections' committed changes.
- Stale writes failing without side effects, including sequential stale-copy edits in one session.
- Existing nested-ID references not reverting other entities, plus full replacement and collision rules.
- Transaction rollback, history recovery, schema-v4 reopening, exports, checkpoints and workspace recovery.

Run `TestBaseModel`, `TestARCManagement` and repository-wide `RunTests`. Inspect emitted classes and declarations, type-check handwritten TypeScript consumers, and execute handwritten JavaScript/Python consumers against both staged and packed artifacts. Record actual results in both implementation plans; skipped targets do not count as passing.
