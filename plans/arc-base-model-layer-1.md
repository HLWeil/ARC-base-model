# ARCBaseModel: Layer 1 Implementation

## Implementation state

Update this section in every implementation commit, alongside the changes it describes. Record checks actually executed, failures, and justified deviations; do not mark unverified work complete.

| Milestone | State | Evidence / remaining work |
|---|---|---|
| Plan and contributor guidance | Ready for initial commit | Agreed scope and API decisions recorded below. |
| Independent library and incremental interop checks | Not started | Create the .NET Standard 2.0 project and verify alternatives as their supporting types are introduced. |
| Complete base model | Not started | Implement all 13 entities and shared behavioral tests. |
| Native artifacts and mapping consumer | Not started | Build public JS/TS/Python entrypoints and exercise transpiled F# mapping from native callers. |
| Build integration, documentation, regression checks | Not started | Register FAKE targets, integrate RunTests, document runnable examples, and verify. |

### Commit and verification log

- Initial plan: implementation has not started. Existing `AGENTS.md` guidance from the planning discussion is included and refined with this plan. The earlier `conformsTo` specification rename was committed separately as `9c8a438`.

## Objective and layer boundaries

Implement a separate **ARCBaseModel** library and namespace representing all 13 base-spec entities faithfully in F#. The same source supports .NET, JavaScript/TypeScript, and Python through APIs usable directly from each language.

The library targets **netstandard2.0**. The pinned Fable.Core and resolved FSharp.Core provide compatible assets; no proposed domain operation requires netstandard2.1. Test executables can retain the repository's net10.0 target.

Layer 1 provides domain classes, native entrypoints, documentation, and tests. It preserves supplied values, collection order, duplicates, and entity references. IDs are optional, mutable properties; the model never generates them automatically.

Layer 2 implementations add operational policies. SQLite is one such implementation, not the definition of Layer 2. It may require or assign domain IDs and use them for keys, relationships, and identity resolution. Those policies do not make IDs mandatory in Layer 1.

Graph operations, deduplication, normalization, codecs, persistence, and migration of existing ProcessCore consumers are outside this implementation. A test-only mapping consumer demonstrates compatibility with a future transpiled persistence library.

## Public model and API

### Entity classes

Implement the normative entity tables in [the base specification](../docs/spec/index.md). One Dataset class combines the properties of all three base profiles: a Dataset may declare several profiles through ConformsTo. Profiles determine applicable conformance rules, not separate runtime entity types.

Every entity exposes fixed read-only Type, optional mutable Id, and mutable AdditionalTypes. Use attached classes, explicit backing fields, and PascalCase properties; preserve TAN acronyms. Avoid public records, overloaded members, DynamicObj inheritance, and custom equality/hash policies.

| Class | Required constructor arguments |
|---|---|
| Annotation, DefinedTerm, DefinedTermSet, Sample | name: string |
| Agent, Organization, Process | name: string |
| Data | path: string |
| ScholarlyArticle | headline: string |
| Descriptor | describes: EntityReference |
| Dataset | conformsTo: seq<string>, identifiers: seq<string> |
| FormalParameter, Recipe | None |

Optional properties are available through optional constructor arguments and setters. Native examples use minimal constructors followed by assignments, avoiding long positional argument lists.

### Named erased alternatives

Declare the following with `[<Erase; RequireQualifiedAccess>]`:

| F# type | Cases | Properties |
|---|---|---|
| AnnotationValue | Text of string; Number of float | Annotation.Value |
| EntityReference | Sample of Sample; Data of Data | Process.Input/Output; Descriptor.Describes |
| RecipeIntendedUse | Text of string; Term of DefinedTerm | Recipe.IntendedUse |
| DefinedTermSetReference | Url of string; TermSet of DefinedTermSet | DefinedTerm.InDefinedTermSet |

F# callers use meaningful cases and ordinary pattern matching. Native JS/Python callers supply underlying primitives or class instances. TypeScript declarations and Python annotations describe the alternatives without requiring runtime union factories. Generic public U2 signatures are excluded.

Classifications, identifiers, URLs, paths, dates, and TANs remain strings. ConformsTo is an open string collection. Numeric annotation values use F# float.

### Construction and mutation

- Reject null required strings/references; do not invent trimming or nonempty-string restrictions absent from the spec.
- Dataset construction requires nonempty Identifiers and ConformsTo, including at least one recognized, case-sensitive base discriminator. Do not insert identifiers or profiles automatically.
- Constructor checks establish required initial data. Continuous conformance validation is outside Layer 1; mutable objects can subsequently become incomplete.
- Optional values default to None and support native omission, assignment, reading, and clearing.
- Collections use ResizeArray with independent empty defaults. Construction and replacement shallow-copy containers, preserving order, duplicates, and entity references. Getters expose live collections.
- Do not register entities, maintain backlinks, infer identity, or equate distinct instances by ID/name/path.
- Support recursive Dataset/Data nesting and mutually linked Annotation/FormalParameter instances. Each nested Dataset declares its own profiles; every Data object supplies its own path.

## Implementation milestones

### 1. Establish the library and verify interop incrementally

Create src/ARCBaseModel with one netstandard2.0 F# project, one ordered compile list, and the existing Fable.Core dependency. Do not depend on ProcessCore infrastructure. Add an independent net10.0 ARCBaseModel.Tests Pyxpecto executable, register both projects in the solution, and set IsPackable=false for this stage.

Verify each erased alternative across all targets as its supporting domain types are implemented. These are real production classes retained in the final library, not a separate prototype model. Begin with term types, Annotation/FormalParameter, Sample/Data references, and Recipe; check native constructors, properties, options, collections, generated types, and pattern matching before building further dependent behavior.

A test-only F# mapping consumer must:

- Pattern-match all four named erased alternatives after compilation to .NET, JS, and Python.
- Project values into explicit SQL-shaped columns and reconstruct them.
- Accept native-created and native-assigned values from handwritten JS/Python callers.
- Preserve text/number distinctions, absence, entity types, and shared references.
- Read and assign optional IDs through the public API.
- Use explicit mapping, not reflection over erased union metadata.

The mapper and native entrypoint must share the same generated model classes. Check returned instances against public exports; do not bundle duplicate model constructors. Do not silently replace the agreed API with wrapper objects or obj properties to bypass failures.

### 2. Complete the base model

Implement the remaining profile entities and unified Dataset with every normative property. Keep source ordering explicit, including the mutually recursive Annotation/FormalParameter declarations. Entity tables override stale diagrams/guides.

Keep collection editing on exposed collections. Do not reproduce graph registration or deduplication methods. Add behavioral tests as entities become available.

### 3. Produce native artifacts

Add **BuildBaseModel as a named FAKE build target**, invoked with `./build.cmd BuildBaseModel`. It builds the .NET library and stages JS/TS and Python artifacts under build/out/base-model. It does not publish artifacts or replace the separate test targets.

- Stage an ESM package named arc-base-model with explicit exports and resolvable TypeScript declarations. Use existing TypeScript/Vite tooling and Fable's copied local runtime.
- Stage an importable arc_base_model Python package with curated exports and the pinned Python Fable runtime.
- Export all 13 classes and the alternative typing surfaces; do not expose erased cases as runtime factories.
- Native callers import public entrypoints, not generated implementation files. Keep mapping probes test-only.
- Clean only dedicated generated outputs before rebuilding so stale artifacts cannot satisfy checks.

Release packaging and publication are deferred. No new production dependencies or compiler upgrades are planned.

### 4. Integrate verification and documentation

Add FAKE targets TestBaseModelDotNet, TestBaseModelJS, TestBaseModelPy, TestBaseModelNative, and the TestBaseModel aggregate. Test targets depend on the required build outputs. Make RunTests include TestBaseModel so CI exercises the new library on all runtimes independently of the disabled legacy Python suite.

Refine AGENTS.md with named-erased-union rules, runtime class identity requirements, and Layer 1 versus Layer 2 ID policies. Add a guide under docs/project with executable examples for all three languages.

## Future SQLite compatibility

Explicit paired-column mappings can represent text/number, Sample/Data, text/DefinedTerm, and URL/DefinedTermSet alternatives. SQL checks choose one case and allow absence for optional properties; transpiled F# pattern matching selects the columns.

The SQLite implementation owns ID scope, uniqueness, collision handling, assignment, and identity resolution. It may use domain IDs directly as primary/foreign keys and maintain ID-keyed identity maps. A separate surrogate-key system is optional. Ordered associations can preserve repeated entries referencing the same identified entity.

Loading reconstructs classes using the shared model runtime. Numeric storage edge cases and exact persistence semantics will be specified with that implementation. Layer 1 includes no production SQL schema, driver, repository, or persistence identity policy.

## Verification and completion

Shared .NET/JS/Python tests cover required construction, optional/assignable IDs, fixed discriminators, independent defaults, all profile combinations, recursive nesting, all alternative cases, numeric zero versus text, optional clearing, collection copying/order/duplicates, shared references, singular endpoints, mutual links, and distinct same-ID instances.

Native tests import the complete public surface, construct and mutate native values, call the transpiled mapping consumer in both directions, verify class identity, type-check valid/invalid TypeScript calls, and execute documentation examples.

Completion requires a passing TestBaseModel, solution build, existing-suite regression checks, and fsdocs or targeted documentation/link verification. Record actual outcomes in the implementation state above. Every logical implementation commit must update this plan; larger commits are acceptable where required to keep a coherent, verified change together.
