---
title: ARCBaseModel Layer 1
category: Project
categoryindex: 2
index: 8
---

# ARCBaseModel Layer 1

`ARCBaseModel` implements the [base specification](../spec/index.md) as 13 mutable
F# classes targeting .NET Standard 2.0. The same source produces JavaScript with
TypeScript declarations and an importable Python package. Native callers use
ordinary constructors, properties, arrays or lists, and primitive values.

One `Dataset` class combines the three base profiles. `ConformsTo` declares one
or more applicable profiles; it does not select a different runtime class.
Nested datasets declare their own profiles independently of their parent.

## Build and verify

Use the repository's .NET SDK, Node/npm, and uv installations. Restore the pinned
tools and dependencies from the repository root before the first build:

```powershell
dotnet tool restore
npm ci
uv sync --frozen
```

Then run these named FAKE targets:

```powershell
.\build.cmd BuildBaseModel
.\build.cmd TestBaseModel
```

`BuildBaseModel` builds the .NET library and stages the native artifacts beneath
`build/out/base-model`. JavaScript consumers import `arc-base-model`; Python
consumers import `arc_base_model`. These are local artifacts, not registry
publications. `TestBaseModel` runs shared F# behavior tests on .NET, JavaScript,
and Python, plus handwritten native consumers and TypeScript checking.

Individual FAKE targets are `TestBaseModelDotNet`, `TestBaseModelJS`,
`TestBaseModelPy`, and `TestBaseModelNative`. The repository's `RunTests` aggregate
includes the new suite. This does not enable the separate legacy ProcessCore
Python suite.

`build/base-model.mjs` creates the JavaScript entrypoint and TypeScript
declarations under `build/out/base-model/js/node_modules/arc-base-model`.
`build/base-model-python.py` creates the Python entrypoint, precise `.pyi` stubs,
and `py.typed` marker under `build/out/base-model/python/arc_base_model`. These
entrypoints export the generated model classes; they do not define replacement
entity classes. Staging restores the named alternatives that Fable emits as
`Any` in Python and removes implementation-only details from public declarations.

## Construct and edit objects

Reference `src/ARCBaseModel/ARCBaseModel.fsproj` from an F# project:

```fsharp
open ARCBaseModel

let temperature = Annotation("temperature", value = AnnotationValue.Number 23.5)
let sample = Sample("leaf", additionalProperties = [ temperature ])
let data = Data("measurements.csv")
let measurement = Process("measure leaf", input = EntityReference.Sample sample,
                          output = EntityReference.Data data)
let descriptor = Descriptor(EntityReference.Sample sample, annotations = [ temperature ])
let dataset = Dataset([ "process-provenance"; "semantic-designation" ], [ "example-study" ])

dataset.Processes.Add(measurement)
dataset.Descriptors.Add(descriptor)
dataset.DataFiles.Add(data)
temperature.Value <- None
```

Constructors require the specification's mandatory data: for example, a Sample
needs a name, Data needs a path, and Descriptor needs a target. Dataset requires
nonempty `ConformsTo` and `Identifiers` collections, including at least one of
`administrative`, `process-provenance`, and `semantic-designation`. Additional
profile strings are allowed. Required strings cannot be null; supplied text is
not trimmed or normalized.

Every entity has a fixed, read-only `Type`, optional mutable `Id`, and mutable
`AdditionalTypes`. Optional fields default to absence. Native JavaScript uses
`undefined` to clear them; Python uses `None`. Zero and empty text remain present
values. Public property names are PascalCase in all three languages.

Collections are `ResizeArray` in F#, arrays in JavaScript, and lists in Python.
Construction and collection replacement copy the container while retaining its
elements. Getters expose the live collection. Order, duplicates, and shared
entity references are preserved; matching names or IDs do not merge objects.
Constructor checks do not continuously enforce conformance after mutation.

## Alternative values across languages

F# uses named erased unions and ordinary pattern matching. Native consumers
assign the underlying values directly:

| F# alternative | JavaScript / TypeScript | Python |
|---|---|---|
| `AnnotationValue.Text` / `.Number` | string / number | str / int or float |
| `EntityReference.Sample` / `.Data` | Sample / Data instance | Sample / Data instance |
| `RecipeIntendedUse.Text` / `.Term` | string / DefinedTerm instance | str / DefinedTerm instance |
| `DefinedTermSetReference.Url` / `.TermSet` | string / DefinedTermSet instance | str / DefinedTermSet instance |

For example, JavaScript can assign `annotation.Value = 23.5` and
`process.Input = sample`. Python uses the same property assignments. Neither
language constructs Fable union tags or calls Fable runtime helpers. Consumers
should import the public package entrypoints so all libraries use the same
entity class definitions.

The executable examples are also acceptance tests:

- [JavaScript consumer](https://github.com/HLWeil/ProcessCore/blob/main/tests/ARCBaseModel.Native/javascript.mjs)
- [Python consumer](https://github.com/HLWeil/ProcessCore/blob/main/tests/ARCBaseModel.Native/python.py)
- [TypeScript consumer](https://github.com/HLWeil/ProcessCore/blob/main/tests/ARCBaseModel.Native/consumer.ts)

`TestBaseModelNative` exercises these consumers against the staged packages.
They cover construction, mutation, absence, collections, every alternative,
reference identity, and a transpiled F# mapping probe. The probe maps alternatives
to separate SQL-shaped columns and back, proving that native-created values can
be consumed by F# pattern matching after transpilation. It is a test fixture,
not a persistence API.

## Pinned Python compiler compatibility

Fable 5.6.0 represents F# floating-point numbers with a runtime wrapper. Its
[numeric type-test implementation](https://github.com/fable-compiler/Fable/blob/e504757ef7f32c6d72aedda604967343e24d24a0/src/Fable.Transforms/Python/Fable2Python.Reflection.fs#L362)
tests `isinstance(value, float64)`, which does not recognize an ordinary Python
`42` or `23.5`. This matches the compiler's documented
[numeric representation](https://fable.io/docs/python/compatibility.html#numeric-types).
Consequently, raw Fable output alone does not satisfy this library's native
numeric contract.

Python staging applies a small AST compatibility pass. It extends generated
`float64` checks to accept native `int` and `float` values alongside the Fable
wrapper, excluding Python booleans from the numeric case. It also converts
wrapped numeric return values at the public `Annotation.Value` and test-probe
numeric boundaries to native Python floats. This adapts numeric representation;
it does not normalize domain text, identifiers, or object identity. The compiler
version and production dependencies remain unchanged.

Apply the same compatibility pass to future transpiled F# Python consumers,
including a separately built SQLite mapper:

```text
python build/base-model-python.py compat <generated-dir>
```

The package build applies it automatically to its generated model and test
consumers. External consumers still need to import the staged model's classes
rather than include another model copy. This compatibility step is required
while using the pinned compiler; changing union case order is not a general fix
for native numeric pattern matching.

## Layer boundaries and IDs

Layer 1 never requires, generates, or infers an entity's optional `Id`.
Dataset's required `Identifiers` collection is a separate property. Graph
indexes, registration, deduplication, normalization, codecs, storage, and
filesystem or network operations remain outside this library. Existing
ProcessCore consumers keep their current implementation.

SQLite is one possible Layer 2 implementation. It may require or assign domain
IDs and use them for keys, relationships, and identity resolution. That
implementation owns uniqueness and collision policies; the base model does not
require separate SQL surrogate keys. Erased alternatives need explicit mappings
rather than reflection over union tags, and separately built consumers must
reuse the same model classes. Layer 1 adds no SQLite schema or driver.
