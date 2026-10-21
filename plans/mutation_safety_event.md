# Automatic mutation handling for ARCBaseModel and ARCManagement

Status: not pursued. See the accepted [immutable redesign](mutation_safety_immutable.md).

## Summary

Make ordinary property assignments and collection mutations enter ARCManagement’s transactional operation pipeline automatically. Detached ARCBaseModel objects remain usable without configuration, persistence or ID assignment.

Decisions confirmed:

- Preserve ordinary JavaScript array and Python list syntax.
- Automatically adopt newly referenced detached entities.
- Commit each mutation individually; do not add an edit-scope API.
- Support normal instance methods and operators. Explicit runtime bypasses, such as Python `list.append(collection, value)`, remain outside the supported editing contract.

## ARCBaseModel and native collections

- Add an optional pre-mutation hook to every entity and mutable value container. With no handler attached, mutations apply immediately. ARCManagement attaches handlers automatically; application code never configures a gate.
- Route scalar setters, optional-value clearing, whole-collection replacement, property-bag operations and nested `EntityCollection` edits through the hook **before changing stored values**.
- Keep hooks generic: ARCBaseModel must retain no ownership, ID-generation, SQL, validation-pipeline or filesystem policy.
- Introduce a controlled `ModelCollection<'T>` surface for shared F# code, replacing public `ResizeArray` exposure. Provide `Count`, indexing, enumeration, `Add`, `AddRange`, `Insert`, `Remove`, `RemoveAt`, `Clear` and `ReplaceWith(seq<_>)`. Preserve constructor collection inputs and document the F# collection-type change.
- Provide one stable native view per collection from construction:
  - JavaScript: a proxy over an array, retaining indexing, iteration, `.length` and `Array.isArray`.
  - Python: a list subclass, retaining indexing, slicing, iteration and `isinstance(value, list)`.
- Forward transpiled F# collection calls through those same views. Keep one authoritative collection storage; never expose its raw bypass.
- Support JavaScript `push`, `pop`, `shift`, `unshift`, `splice`, `sort`, `reverse`, `fill`, `copyWithin`, indexed assignment and length truncation. Support Python list mutators, integer/slice assignment and deletion, `+=` and `*=`.
- Execute each collection method on a temporary copy, then submit one complete replacement. Preserve native return values and receiver identity. Detect Python augmented assignment’s subsequent property setter so it creates no duplicate operation.
- Reject hole-producing JavaScript edits on managed collections. Nonmutating copies return detached native collections. Explicit base-class calls, borrowed prototype mutators and private implementation-field access are unsupported.
- Add a trusted integration bridge using internal members and `.NET InternalsVisibleTo("ARCManagement")`; exclude attachment and replay methods from curated native APIs.

## ARCManagement operation pipeline

- Replace direct-mutation scanning with interception. Retain existing snapshots for persisted state, history and recovery.
- Route intercepted changes and existing operation-group methods into one command engine. Preserve operation-group names and avoid nested repository access or duplicate history entries.
- For every mutation:
  1. Acquire serialized repository access before evaluating collection callbacks or input iterables.
  2. Verify lifetime, ownership and revision.
  3. Stage replacement values and recursive adoption without exposing changes.
  4. Validate the complete proposed state.
  5. Write state, history, journal and revision in one SQLite transaction.
  6. Project into canonical objects through trusted replay, then complete the commit.
- Replay bypasses interception only for management’s own hydration, projection, ID assignment, undo/redo and rollback. Scope it with `try/finally`; execute no application callbacks during replay.
- On failure, restore values, collection contents and aliases, original IDs, ownership and handlers. Failed newly adopted objects return to their previous detached state. If projection repair fails, invalidate the graph and require reopening.
- Suppress no-op history, journal and revision changes after checking lifetime, ownership and revision. Compare entity references by identity and preserve collection order and duplicates.
- Reject reentrant managed writes from comparators, iterators or other mutation callbacks. Each public collection method remains one operation despite changing several elements.
- Retain existing SQL mirroring and schema version 4. Incremental SQL writes are outside this rework.

## Identity, adoption and lifecycle

- Automatically adopt detached entities introduced through direct assignments, collection edits and equivalent operation-group setters. Assign missing IDs only in management; reject distinct-object ID collisions and ownership by another graph.
- Reject direct changes to managed entity IDs, including clearing them.
- Preserve canonical entity instances and managed core collection views across assignments, upserts, undo and redo. Copy supplied external arrays/lists rather than adopting their containers. Detached whole-property assignments retain their existing copy semantics.
- Track each shared extension collection by reference and all its active owning properties/paths. Mutating it updates every affected occurrence in one operation. Reject cross-graph sharing and collection-containment cycles.
- Reconcile extension containers instead of recreating them during unrelated edits. Retain runtime topology information alongside live history so undo/redo preserves held wrappers and sharing.
- Preserve the current persisted value representation: reopening materializes fresh extension containers without introducing persisted container identities.
- Deleted entities and extension containers with no active occurrences remain inactive and reject mutation; undo or valid reassignment can reactivate them. Closed or displaced projections remain write-invalid rather than silently becoming detached.
- Keep `ARCtrl.ARC` forwarding to its exact `ManagedGraph`. Automatic mutations update SQLite and pending-export status; filesystem publication remains explicit through `save()`.

## Verification and documentation

- Add shared tests for detached usability, every setter and collection operation, optional clearing, native numeric alternatives, duplicates, reference preservation and automatic adoption.
- Test ownership conflicts, managed ID rejection, no-ops, stale revisions, validation/provider/commit failures, rollback, undo/redo and repository reopening.
- Retain collection references before adoption and across replacement, unrelated edits, extension removal and history. Verify that references stay connected or reject writes according to their lifecycle.
- Add handwritten JS/TS and Python tests for native syntax, collection identity, mutator return values, one history entry per mutation, augmented assignment and callback reentrancy.
- Inspect emitted classes and declarations. Preserve shared model class identity, precise native types and the existing Python numeric compatibility pass with the pinned toolchain.
- Run `TestBaseModel`, `TestARCManagement` and `RunTests`, including native consumers and packed artifacts. Run a documentation build or targeted link check.
- Update contributor guidance, both implementation plans and the model/management/workspace guides. Document automatic assignment failures, native bypass boundaries, F# collection migration and individual-operation semantics.
- Introduce no production dependencies, compiler upgrades or database migration.
