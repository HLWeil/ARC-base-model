---
title: Portable Base Model
category: Project
categoryindex: 2
index: 8
---

# Portable immutable base model

ARCBaseModel represents the shared entities and three base profiles as attached immutable classes for .NET, JavaScript/TypeScript and Python. It has no database, filesystem, ownership or identity-generation policy. The [immutable implementation plan](../../plans/mutation_safety_immutable.md) defines the API contract.

## Construction and copies

Supply only the specification's required constructor arguments. Optional IDs remain absent until explicitly supplied or assigned to persisted copies by ARCManagement. Values, strings, ordering, duplicates and entity references are preserved.

```fsharp
let sample = Sample("before")
let edited = Sample.withName(sample, "after")
let dataset = Dataset(["process-provenance"], ["example"], title = "title")
let cleared = Dataset.clearTitle(dataset)
```

```javascript
const sample = new Sample("before");
const edited = Sample.withName(sample, "after");
```

```python
sample = Sample("before")
edited = Sample.with_name(sample, "after")
```

Properties are getter-only. Copies keep the same concrete entity type and share unchanged immutable containers/referenced entities; changing a scalar does not copy the graph. Use static clear methods for optional absence. Zero, false and empty text remain present values where their alternative is supported. Numbers remain numbers; Python Boolean values do not match numeric alternatives.

Collection inputs may be native iterables and are eagerly copied once. Outputs are read-only .NET lists, frozen JS arrays and immutable Python tuples. Indexing and count/length are constant-time, including from transpiled F#. Collection getters return their stored container. Copies preserve unchanged containers. Native adapters retain the canonical model classes used by erased alternatives.

Ordinary property, backing-field and collection mutation fails. Deliberate runtime reflection/low-level mutation bypasses are outside the API contract.

## Graphs and cycles

A shallow copy preserves existing outgoing references, including references to its original object. Use a builder to construct or edit cycles:

```fsharp
let builder = ModelBuilder()
let token = builder.draft(Dataset(["process-provenance"], ["root"]))
builder.Dataset.setHasParts(token, [token])
let cyclic = builder.finish().get(token)

let edit = ModelBuilder()
let cloned = edit.cloneGraph(cyclic)
edit.Dataset.setTitle(cloned, "edited cycle")
let edited = edit.finish().get(cloned)
```

`cloneGraph` clones by object identity and rewires all reachable core and extension references. `draftFor(original)` retrieves that original's cloned draft. `draft(value)` creates a distinct future entity retaining prototype relationships; `reference(value)` supplies an uneditable reference token. Typed token checks reject foreign builders and wrong entity kinds. Failed completion publishes nothing and permits correction; successful completion consumes the builder.

Detached YAML preserves sharing, cycles, absent IDs and distinct objects with equal IDs. Management subsequently applies its separate ID/collision policy; see the [management guide](arc-management.md).

## Packages and verification

Import only public entrypoints: `arc-base-model` for JS/TS and `arc_base_model` for Python. Generated implementation modules and Fable runtime helpers are not part of the consumer API. Packages include curated declarations, Python stubs and `py.typed`; Python numeric compatibility is applied during staging with the pinned Fable 5.20.0 toolchain.

Run `TestBaseModel` for shared .NET/JS/Python behavior, handwritten native consumers, TypeScript declaration checks and packed artifacts. A successful transpilation or skipped target does not establish runtime support.
