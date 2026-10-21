---
title: ARCBaseModel Layer 1
category: Project
categoryindex: 2
index: 8
---

# ARCBaseModel Layer 1

`ARCBaseModel` implements the [base specification](../spec/index.md) as 13 immutable
F# classes targeting .NET Standard 2.0. The same source produces JavaScript with
TypeScript declarations and an importable Python package. Native callers use
ordinary constructors, getter-only properties, static `with*` copies, frozen
arrays or tuples, and primitive values.

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

let dataset2 = Dataset.withProcesses(dataset, [ measurement ])
let dataset3 = Dataset.withDescriptors(dataset2, [ descriptor ])
let dataset4 = Dataset.withDataFiles(dataset3, [ data ])
let cleared = Annotation.withValue(temperature, None)
let renamed = Sample.withName(sample, "leaf 2")
let identified = EntityObject.withId(sample, Some "sample-1")
```

Each `with*` function is a static member that takes the original value and a
replacement, and returns a new object of the same class. The original is
unchanged. A copy shares unchanged fields and collections with the original.

Constructors require the specification's mandatory data: for example, a Sample
needs a name, Data needs a path, and Descriptor needs a target. Dataset requires
nonempty `ConformsTo` and `Identifiers` collections, including at least one of
`administrative`, `process-provenance`, and `semantic-designation`. Additional
profile strings are allowed. Required strings cannot be null; supplied text is
not trimmed or normalized.

Every entity has a fixed, read-only `Type` and an optional `Id`. It also has
`AdditionalTypes`. All properties are getter-only. Optional fields default to
absence. A `with*` copy of an optional property takes an option, so `None` (F# and
Python) or `undefined` (JavaScript) clears it. Zero and empty text
remain present values. Public property names are PascalCase in all three
languages. The static copy functions are `with<Property>` in F# and JavaScript
and `with_<snake_case>` in Python, for example `withNameTAN` and `with_name_tan`.

`EntityObject` provides the generic `withId`, `withAdditionalTypes`,
`withProperty`, and `withoutProperty` (Python `with_id`, `with_additional_types`,
`with_property`, `without_property`). They return the concrete class of their
argument. Copies run the same validation as the constructors, so a copy cannot
remove a required value.

Collections are typed `seq<'T>` in F# (`readonly T[]` in TypeScript, `tuple[T, ...]`
in Python). On .NET they are `ReadOnlyCollection` instances. JavaScript stores
frozen arrays and Python stores tuples. Construction and `with*` copies consume an input
iterable once and keep its elements. Getters return the stored collection
without allocating, and `push` or `append` on it fails. Collections keep their
order and duplicates, and entities keep shared references. Matching names or IDs
do not merge objects. Constructor and copy checks run when a value is built, and nothing
else enforces conformance afterwards.

The model has no public builder and no runtime sealing. Assigning to a
getter-only property throws a TypeError in strict-mode JavaScript (which includes
ES modules) and an AttributeError in Python. The internal state behind the
properties is an unsupported bypass. Constructors and copies cannot build cyclic graphs.
ARCManagement and YAML decoding create cycles through internal state assignment.

Transitional note: until the management runtime phase of the immutable redesign,
managed graphs update adopted instances in place. They write IDs into supplied
instances and reassign internal state on edits and undo.

## Alternative values across languages

F# uses named erased unions and ordinary pattern matching. Native consumers
supply the underlying values directly:

| F# alternative | JavaScript / TypeScript | Python |
|---|---|---|
| `AnnotationValue.Text` / `.Number` | string / number | str / int or float |
| `EntityReference.Sample` / `.Data` | Sample / Data instance | Sample / Data instance |
| `RecipeIntendedUse.Text` / `.Term` | string / DefinedTerm instance | str / DefinedTerm instance |
| `DefinedTermSetReference.Url` / `.TermSet` | string / DefinedTermSet instance | str / DefinedTermSet instance |

For example, JavaScript constructs and copies with plain values:

```javascript
import { Annotation, Sample, Process, EntityObject } from "arc-base-model";

const temperature = new Annotation("temperature", 23.5);
const cleared = Annotation.withValue(temperature, undefined);
const sample = new Sample("leaf", [temperature]);
const measurement = Process.withInput(new Process("measure leaf"), sample);
const identified = EntityObject.withId(sample, "sample-1");
sample.AdditionalProperties.push(cleared); // throws, the array is frozen
```

Python uses the same classes and snake_case copy functions:

```python
from arc_base_model import Annotation, Sample, Process, EntityObject

temperature = Annotation("temperature", 23.5)
cleared = Annotation.with_value(temperature, None)
sample = Sample("leaf", [temperature])
measurement = Process.with_input(Process("measure leaf"), sample)
identified = EntityObject.with_id(sample, "sample-1")
# sample.AdditionalProperties is a tuple, so it has no append
```

Neither language constructs Fable union tags or calls Fable runtime helpers.
Consumers should import the public package entrypoints so all libraries use the
same entity class definitions.

The executable examples are also acceptance tests:

- [JavaScript consumer](https://github.com/HLWeil/ProcessCore/blob/main/tests/ARCBaseModel.Native/javascript.mjs)
- [Python consumer](https://github.com/HLWeil/ProcessCore/blob/main/tests/ARCBaseModel.Native/python.py)
- [TypeScript consumer](https://github.com/HLWeil/ProcessCore/blob/main/tests/ARCBaseModel.Native/consumer.ts)

`TestBaseModelNative` exercises these consumers against the staged packages.
They cover construction, pure copies, absence, collections, every alternative,
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

The user-updated Fable 5.20 runtime represents doubles as plain Python floats;
its erased numeric tests recognize the exact float type, still excluding native
integers. Python staging applies a small AST compatibility pass supporting both
compiler shapes. It accepts native `int`/`float` and older Fable wrappers while
excluding Python Booleans. Wrapped Annotation, property-bag, collection and probe
numeric reads return native floats; supplied native values retain their identity.
The pass handles the wrapper constructor and
the newer float conversion function. It adapts representation without changing
domain text, identifiers, or object identity. No dependency or compiler upgrade
is performed by staging.

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

## Typed extension properties

All thirteen core classes inherit `EntityObject`. Its read-only `Type` and optional `Id` are getter-only, and copies change `Id` and `AdditionalTypes` through `withId` and `withAdditionalTypes`. Generic extension objects require a type string, and never receive an inferred ID. See the [normative extension contract](../spec/extensions.md).

```fsharp
let quality =
    EntityObject("QualityAssessment")
    |> fun q -> EntityObject.withProperty(q, "accepted", Entity.Bool true)
    |> fun q -> EntityObject.withProperty(q, "score", Entity.Number 0.95)
let sample =
    Sample("leaf")
    |> fun s -> EntityObject.withProperty(s, "lab:quality", Entity.Object quality)
    |> fun s -> EntityObject.withProperty(s, "lab:missingMeasurement", Entity.Null(EntityNull()))
let withoutQuality = EntityObject.withoutProperty(sample, "lab:quality")
```

JavaScript/TypeScript uses direct scalar and object values:

```javascript
const quality = EntityObject.withProperty(new EntityObject("QualityAssessment"), "score", 0.95);
let sample = new Sample("leaf");
sample = EntityObject.withProperty(sample, "lab:quality", quality);
sample = EntityObject.withProperty(sample, "lab:values", new EntityCollection([0, false, ""]));
sample = EntityObject.withProperty(sample, "lab:binary", new EntityBlob("AA=="));
sample = EntityObject.withProperty(sample, "lab:null", new EntityNull());
sample = EntityObject.withoutProperty(sample, "lab:binary");
```

Python uses the same attached classes and members:

```python
quality = EntityObject("QualityAssessment")
quality = EntityObject.with_property(quality, "score", 0.95)
sample = Sample("leaf")
sample = EntityObject.with_property(sample, "lab:quality", quality)
sample = EntityObject.with_property(sample, "lab:values", EntityCollection([0, False, ""]))
sample = EntityObject.with_property(sample, "lab:binary", EntityBlob("AA=="))
sample = EntityObject.with_property(sample, "lab:null", EntityNull())
sample = EntityObject.without_property(sample, "lab:binary")
```

Import these classes through the root `arc-base-model` or `arc_base_model` entry. `arc-base-model/internal` is an undeclared runtime entry used only by ARCManagement's bundle so both packages share class identity. `EntityCollection` copies the input container once and keeps every element, including duplicates and object references. It is immutable, and `Count` and `Get` read its stored values. `EntityBlob.Base64` returns validated, unmodified canonical standard padded base64.

`withProperty` inserts a key or replaces its value and returns a new object. `withoutProperty` returns a new object without the key and accepts a key that is already absent. The read-only `EntityProperties` bag offers `Keys`, `Contains`, and `Get`. `Contains` distinguishes absence from explicit null. `Get` throws for a missing key. `Keys` is an immutable collection with no guaranteed enumeration order. Core wire names are reserved, and `withProperty` rejects them with exact case-sensitive comparisons.

Numbers use binary64, matching `AnnotationValue`; integer and float inputs share one alternative. Large integers may lose precision. Python staging adapts Fable numeric checks and unwraps wrapped numeric reads at bag and collection boundaries; future transpiled consumers must apply the existing `compat` pass and share the staged model classes.

This change supplies no YAML codec or SQL persistence. Future YAML maps extension keys alongside core fields, null to YAML null, and blobs to tagged binary. SQL guidance uses typed rows, ordered elements, BLOBs, and entity relationships, retaining the current supplied-ID requirement. Future codecs must report unsupported non-finite numbers and define shared/cyclic reference encoding explicitly.
