# Project Context

ARC Data Model is the specification and implementation workspace for the ARC process data model. It contains the normative markdown spec, derived SQL/YAML schema artifacts, examples, reference material, fsdocs documentation, and F# libraries for ProcessCore and the SQL profile.

## Architecture

```text
ARC-Data-Model/
├── docs/                         fsdocs documentation pages
│   ├── index.md
│   ├── _head.html                 fsdocs head injection, including Mermaid support
│   └── project/                   canonical project guides
│   └── spec/                      normative model specification
│       ├── shared/                shared entity specifications
│       ├── process_provenance/    process provenance base profile
│       ├── semantic_designation/  semantic designation base profile
│       └── administrative/        administrative base profile
├── spec/                         compatibility pointer to docs/spec
├── schemas/                      derived schema representations
│   ├── sql/                       executable SQLite profile and design notes
│   ├── yml/                       JSON Schema draft 2020-12 expressed in YAML
│   └── document-db/               placeholder for future document DB schemas
├── examples/                     concrete example documents
│   ├── core/                      schema-shaped core examples
│   ├── isa/                       legacy/profile-shaped ISA and Datamap examples
│   └── workflow-run/              placeholder for future Workflow Run examples
├── references/                   upstream profiles and preserved prior implementation notes
├── src/                          F# implementation projects
│   └── ProcessCore/              consolidated core, YAML, SQL, and Fable projects
├── tests/                        Pyxpecto tests
│   ├── ProcessCore.Tests/        consolidated core, YAML, and SQL tests
│   └── SpeedTest/
└── build/                        FAKE build project and task modules
```

## Current Vocabulary

- Core process/protocol entities are `Process` and `Recipe`.
- Core process I/O properties are `input`, `output`, and `executesRecipe`. Each endpoint is optional and singular.
- Dataset profile declarations use `conformsTo`, a collection despite the singular field name. Collection properties include `additionalTypes`, `additionalProperties`, `parameterValues`, and `hasParts`.
- Some wire profiles or legacy examples use `inputs`, `outputs`, `object`, or `result`; follow their own specifications at the codec boundary. The normative entity tables in `docs/spec/` govern the domain API when older guides or implementations disagree.
- Long-form project documentation belongs under `docs/project/`.
- Normative specification prose belongs under `docs/spec/`.
- Existing README files should stay short and link into `docs/`.

## Tech Stack

- F# / .NET projects in `src/`, currently centered on consolidated `ProcessCore` with Fable-specific project files beside it.
- FAKE build project under `build/`.
- fsdocs for generated documentation.
- Pyxpecto tests, with Fable transpilation paths for JavaScript and Python.
- JavaScript runtime tests use Node and `better-sqlite3`.
- Python runtime tests use `uv` and Python stdlib `sqlite3`.

## Layer 1: Portable Domain Model

Layer 1 represents the base specification as one shared F# domain model for .NET, JavaScript/TypeScript, and Python. The independent `ARCBaseModel` library targets `netstandard2.0`. Direct use from native JavaScript and Python is part of the public API contract; successful Fable compilation alone is insufficient. Track implementation and verification in [the Layer 1 plan](plans/arc-base-model-layer-1.md), updating its state alongside every implementation commit.

- Implement the shared entities and all three base profiles faithfully, with one Dataset type covering their combined properties. Preserve the specified value alternatives, cardinalities, and recursive mixed-profile nesting.
- Every entity's `id` is optional. Do not require, generate, or infer IDs, including from names, paths, or other identifiers. This does not make separately required properties such as Dataset `identifiers` optional.
- SQLite is one possible Layer 2 implementation. Such implementations may require or assign domain IDs and use them extensively for keys, relationships, and identity resolution; their policies do not make IDs mandatory in Layer 1. A separate SQL surrogate-key system is not required by the base model.
- Preserve supplied values, object references, collection order, and duplicates. Constructors, setters, and collection helpers must not deduplicate, merge, register, canonicalize, or infer identity. Do not reuse existing equality/hash policies to equate entities by name or ID.
- Keep graph indexes, back-edge maintenance, traversal, fragment resolution, normalization, codecs, persistence, and filesystem/network operations outside Layer 1. Existing ProcessCore behavior is reference material, not a requirement to carry those policies into the base model.

### Public API Shape

- Use classes, not F# records, for public domain entities and value wrappers. Apply `[<AttachMembers>]` to public portable classes so native callers can use their instance and static members.
- Use domain-named `[<Erase; RequireQualifiedAccess>]` unions for spec alternatives: `AnnotationValue`, `EntityReference`, `RecipeIntendedUse`, and `DefinedTermSetReference`. Do not expose generic `U2` signatures, ordinary emitted F# union wrappers, `Choice`, or `Result`. F# callers use meaningful cases; JS/Python callers use direct values. Verify each alternative across all targets as its supporting types are implemented, including transpiled F# pattern matching on native-created values. Cases must remain distinguishable at runtime; mapper and caller must share the same entity class definitions. Native consumers must not construct Fable union tags/fields or invoke runtime helpers.
- Use explicit mutable backing fields such as `_name` with public getters/setters; avoid public auto-properties (`member val`) and constructor-parameter shadowing. Inspect output for leaked backing names such as `Name@`.
- Keep required constructor arguments small and spec-driven; use optional arguments for optional fields. Give class-owned factories and operations distinct names and tupled argument lists. Avoid public overloads, curried calling conventions, and requiring detached compiler-generated functions. Do not assume F# named optional arguments become a JavaScript options object or usable Python keyword arguments without checking the output. Common calls should not require long runs of `undefined`/`None` placeholders; use a small constructor plus properties, named factories, or a tested facade.
- Prefer `ResizeArray<T>` for mutable model collections: Fable documents JavaScript arrays and Python lists for this shape. Keep F# lists, maps, and sets internal. Accept native collections at the API boundary; consume any `seq<T>` input into stored collections without filtering or replacing its entity references. Verify F# array representations separately, especially on Python.
- Simple F# options are acceptable when native callers can supply, read, and clear them through ordinary target-language absence values. Avoid nested options or option wrappers in the public contract. Test omission and clearing explicitly, including that `0`, `false`, and empty text are not treated as absent where applicable.
- Preserve spec strings (including URLs, paths, dates, and open-ended profile classifications) without introducing .NET-only wrappers or closed enums. Verify native numeric inputs and outputs for Number fields; do not convert numbers into text to simplify interop.
- Keep stable, curated exports in the TypeScript and Python entrypoints (currently `src/ProcessCore/index.ts` and `src/ProcessCore/__init__.py`). Export every intended public type with documented names. Thin native facades may adapt call syntax, but must not duplicate domain logic or require callers to import generated implementation files or Fable runtime helpers.
- Keep shared source and compile order aligned across target projects. Use the repository's pinned Fable toolchain; keep target-specific interop at explicit boundaries and verify behavior before adopting newer compiler features.

### Evidence and Examples

Use these as API-design references, not as substitutes for the base specification:

- Local class/property patterns: `src/ProcessCore/DefinedTerm.fs` and `Administrative.fs`. Existing `Graph.fs` unions and deduplication behavior are not Layer 1 templates.
- [arc-validate portable Fable conventions](https://github.com/nfdi4plants/arc-validate/blob/dev/AGENTS.md#portable-fable-code-style): attached class members, explicit backing fields, and generated API review.
- Concrete AVPR examples: an attached [Author class](https://github.com/nfdi4plants/arc-validate-package-registry/blob/ee5d98536873a8d12140aea778c196eebafe76dd/src/ValidationPackage.Model/Author.fs), plus handwritten [JavaScript](https://github.com/nfdi4plants/arc-validate-package-registry/blob/ee5d98536873a8d12140aea778c196eebafe76dd/tests/ValidationPackage.NativePackageSmoke/javascript.mjs) and [Python consumers](https://github.com/nfdi4plants/arc-validate-package-registry/blob/ee5d98536873a8d12140aea778c196eebafe76dd/tests/ValidationPackage.NativePackageSmoke/python.py). Reuse their API patterns, not their domain-specific defaults or validation policies.
- [DataHubClient native packages and consumer checks](https://github.com/nfdi4plants/DataHubClient#package-smoke-tests): curated exports and tests using installed packages from native code.
- Fable's [JavaScript features](https://fable.io/docs/javascript/features.html) and [Python features](https://fable.io/docs/python/features.html) describe attached members, overload limitations, and erased unions. Its [JavaScript](https://fable.io/docs/javascript/compatibility.html) and [Python compatibility](https://fable.io/docs/python/compatibility.html) pages describe collection and option representations. Check these against the pinned compiler rather than assuming identical output across versions or targets.

## Commands

```powershell
.\build.cmd BuildSolution
.\build.cmd RunTests
.\build.cmd runTestsJS
.\build.cmd runTestsPy
.\build.cmd TranspileTS
.\build.cmd TranspilePy
.\build.cmd BuildDocs
.\build.cmd WatchDocs
dotnet fsdocs watch
npm run test:js
```

## Git & Commits

- Conventional commits: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`.
- One logical change per commit. Keep diffs reviewable.
- Never force-push to `main`.

## Prohibitions

- Do NOT add new production dependencies without asking first.
- Do NOT rewrite preserved upstream reference files unless explicitly asked; prefer documenting current behavior in `docs/project/`.

## Verification

Before marking docs plumbing work as done, run an fsdocs build or a targeted markdown/link check when practical. Before marking implementation work as done, run the relevant FAKE test target.

For portable domain API changes, run the shared behavioral tests on .NET, JavaScript, and Python, then inspect the emitted classes and type declarations. The current `RunTests` aggregate omits Python; invoke `runTestsPy` explicitly until that is fixed. A skipped or failing target is not evidence of cross-target support.

Add handwritten JavaScript/TypeScript and Python consumer tests alongside the shared F# tests. Import through the public entrypoints and exercise construction, property reads/writes, optional values, native arrays/lists, spec alternatives, duplicate preservation, and shared entity references. Type-check TypeScript callers as well as running JavaScript and Python callers. When package artifacts are produced, run these consumers against the packed artifacts too; this catches export and dependency defects that transpiled F# tests cannot detect.
