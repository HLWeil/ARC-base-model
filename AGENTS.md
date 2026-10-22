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
│   ├── ARCBaseModel/             portable Layer 1 domain classes
│   ├── PolyglotSQLite/           portable SQLite values, drivers, transactions, and CRUD
│   ├── ARCManagement/            graph management, YAML codecs, and SQLite repositories
│   │   └── ARCtrl/               ARC workspace binding, save, and recovery
│   └── ProcessCore/              existing core, YAML, SQL, and Fable projects
├── tests/                        Pyxpecto tests
│   ├── ARCBaseModel.Tests/       shared Layer 1 behavior tests and mapping probe
│   ├── ARCBaseModel.Native/      handwritten JavaScript, TypeScript, and Python consumers
│   ├── PolyglotSQLite.Tests/     shared SQLite behavior and database interoperability tests
│   ├── PolyglotSQLite.Native/    handwritten SQLite package consumers
│   ├── ARCManagement.Tests/      shared management/workspace tests and native consumers
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

- F# / .NET projects in `src/`: independent `ARCBaseModel` and `PolyglotSQLite` target .NET Standard 2.0; existing consolidated `ProcessCore` retains its own Fable-specific project files.
- FAKE build project under `build/`.
- fsdocs for generated documentation.
- Pyxpecto tests, with Fable transpilation paths for JavaScript and Python.
- JavaScript runtime tests use Node and `better-sqlite3`.
- Python runtime tests use `uv` and Python stdlib `sqlite3`.

## Accepted redesign

[The immutable redesign](plans/mutation_safety_immutable.md) supersedes the live-projection management rules and the previous managed YAML format. The Layer 1, storage (schema version 5 with a diff writer and delta history), runtime and API phases have landed, so the rules in this file describe the immutable model and the cached-state runtime. The YAML phase (ProcessCore-shaped codec) and the close-out remain, and until the YAML phase lands the DatasetYaml and root-only `id`/`$ref` rules below describe the code. Each phase updates this file. Do not reintroduce what the redesign excludes: a public model builder, runtime sealing, caller revision tokens or baselines, mirror tables, or the `journal` table.

## Layer 1: Portable Domain Model

Layer 1 represents the base specification as one shared F# domain model for .NET, JavaScript/TypeScript, and Python. The independent `ARCBaseModel` library targets `netstandard2.0`. Direct use from native JavaScript and Python is part of the public API contract; successful Fable compilation alone is insufficient. Track implementation and verification in [the Layer 1 plan](plans/arc-base-model-layer-1.md), updating its state alongside every implementation commit.

- Implement the shared entities and all three base profiles faithfully, with one Dataset type covering their combined properties. Preserve the specified value alternatives, cardinalities, and recursive mixed-profile nesting.
- Every entity's `id` is optional. Do not require, generate, or infer IDs, including from names, paths, or other identifiers. This does not make separately required properties such as Dataset `identifiers` optional.
- SQLite is one possible Layer 2 implementation. Such implementations may require or assign domain IDs and use them extensively for keys, relationships, and identity resolution; their policies do not make IDs mandatory in Layer 1. A separate SQL surrogate-key system is not required by the base model.
- Preserve supplied values, object references, collection order, and duplicates. Constructors and pure copies must not deduplicate, merge, register, canonicalize, or infer identity. Do not reuse existing equality/hash policies to equate entities by name or ID.
- Keep graph indexes, back-edge maintenance, traversal, fragment resolution, normalization, codecs, persistence, and filesystem/network operations outside Layer 1. Existing ProcessCore behavior is reference material, not a requirement to carry those policies into the base model.

### Public API Shape

- Use classes, not F# records, for public domain entities and value wrappers. Apply `[<AttachMembers>]` to public portable classes so native callers can use their instance and static members.
- Use domain-named `[<Erase; RequireQualifiedAccess>]` unions for spec alternatives: `AnnotationValue`, `EntityReference`, `RecipeIntendedUse`, and `DefinedTermSetReference`. Do not expose generic `U2` signatures, ordinary emitted F# union wrappers, `Choice`, or `Result`. F# callers use meaningful cases; JS/Python callers use direct values. Verify each alternative across all targets as its supporting types are implemented, including transpiled F# pattern matching on native-created values. Cases must remain distinguishable at runtime; mapper and caller must share the same entity class definitions. Native consumers must not construct Fable union tags/fields or invoke runtime helpers.
- Publish getter-only properties. Each class keeps its own fields in an internal immutable state record, and `EntityObject` holds a common record (`Id`, `AdditionalTypes`, extension properties). Avoid public auto-properties (`member val`) and constructor-parameter shadowing. Inspect output for leaked backing names such as `Name@`. Assigning to a getter-only property throws a TypeError in strict-mode JavaScript (which includes ES modules) and an AttributeError in Python, so the model has no runtime sealing. Reaching the internal state is an unsupported bypass.
- Edit through static `with<Property>(value, replacement)` copies, one per own property (Python `with_<snake_case>`). Optional properties take an option, so `None`/`undefined` clears them. `EntityObject` provides the generic `withId`, `withAdditionalTypes`, `withProperty`, and `withoutProperty`, which return the concrete class. Copies run the constructor validation. The public API has no builder, so constructors and copies cannot create cycles. ARCManagement and YAML decoding create them through internal state assignment, enabled by `InternalsVisibleTo` for ARCManagement and ARCManagement.Tests. The stagers hide internal members and record types (`State`, `Common`, `Reserved`, `ReserveProperties`, `CloneObject`, `Copy`, `CopyAny`, `With`, `Without`, `Values`, `Empty`).
- Keep required constructor arguments small and spec-driven. Use optional arguments for optional fields. Give class-owned factories and operations distinct names and tupled argument lists. Avoid public overloads, curried calling conventions, and requiring detached compiler-generated functions. Do not assume F# named optional arguments become a JavaScript options object or usable Python keyword arguments without checking the output. Common calls should not require long runs of `undefined`/`None` placeholders. Use a small constructor plus `with*` copies, or a tested facade with named factories.
- Type model collections as `seq<'T>` (TypeScript `readonly T[]`, Python `tuple[T, ...]`). On .NET they are `ReadOnlyCollection` instances. JavaScript stores frozen arrays and Python stores tuples, so `push` and `append` fail. Keep F# lists, maps, and sets internal. Accept native iterables at the API boundary and consume each input once into the stored collection without filtering or replacing its entity references. `EntityPropertyBag` is read-only (`Keys`, `Contains`, `Get`) and `EntityCollection` is immutable (`Count`, `Get`). Verify F# array representations separately, especially on Python.
- Simple F# options are acceptable when native callers can supply, read, and clear them through ordinary target-language absence values. Avoid nested options or option wrappers in the public contract. Test omission and clearing explicitly, including that `0`, `false`, and empty text are not treated as absent where applicable.
- Preserve spec strings (including URLs, paths, dates, and open-ended profile classifications) without introducing .NET-only wrappers or closed enums. Verify native numeric inputs and outputs for Number fields; do not convert numbers into text to simplify interop.
- Keep stable, curated exports in the native entrypoints. For Layer 1, `build/base-model.mjs` stages `build/out/base-model/js/node_modules/arc-base-model`, and `build/base-model-python.py` stages `build/out/base-model/python/arc_base_model`, including `__init__.py`, precise `.pyi` stubs, and `py.typed`. The existing ProcessCore entrypoints remain separate. Export every intended public type with documented names. Staging restores Python erased alternatives emitted as `Any` and hides implementation-only declaration details. `arc-base-model/internal` is an undeclared runtime entry used only by ARCManagement's bundle so both packages share class identity. Consumers import the root entry. Thin native facades may adapt call syntax, but must not duplicate domain logic or require callers to import generated implementation files or Fable runtime helpers.
- Keep shared source and compile order aligned across target projects. Use the repository's pinned Fable toolchain; keep target-specific interop at explicit boundaries and verify behavior before adopting newer compiler features.

### Pinned Python Numeric Compatibility

Fable 5.6.0's [numeric type test](https://github.com/fable-compiler/Fable/blob/e504757ef7f32c6d72aedda604967343e24d24a0/src/Fable.Transforms/Python/Fable2Python.Reflection.fs#L362) checks the `float64` wrapper, so ordinary Python ints/floats can fail erased-union pattern matches. The [Python numeric compatibility documentation](https://fable.io/docs/python/compatibility.html#numeric-types) describes that wrapper representation. Successful raw transpilation is insufficient for the native numeric contract.

- `build/base-model-python.py` applies a focused AST compatibility pass to generated Python modules. Numeric type checks accept native `int`/`float` and Fable `float64`, excluding `bool`. Public `Annotation.Value` and test-probe numeric return boundaries convert wrapper values to native floats; this is representation adaptation, not normalization of domain values or IDs.
- Future transpiled F# Python consumers must apply `python build/base-model-python.py compat <generated-dir>` and import the same staged model classes. Package staging applies the pass automatically to model and test consumers. Do not rely on case reordering, replace entity classes, or monkeypatch the shared Fable runtime.
- Keep checks for native integer/fractional/zero values, text versus numbers, wrapped F# numeric outputs, optional values, wildcard matches, and Boolean exclusion. Reassess this compatibility pass when intentionally changing the compiler; no compiler upgrade or new production dependency is part of Layer 1.

### Evidence and Examples

Use these as API-design references, not as substitutes for the base specification:

- Local class/property patterns: `src/ProcessCore/DefinedTerm.fs` and `Administrative.fs`. Existing `Graph.fs` unions and deduplication behavior are not Layer 1 templates.
- [arc-validate portable Fable conventions](https://github.com/nfdi4plants/arc-validate/blob/dev/AGENTS.md#portable-fable-code-style): attached class members, explicit backing fields, and generated API review.
- Concrete AVPR examples: an attached [Author class](https://github.com/nfdi4plants/arc-validate-package-registry/blob/ee5d98536873a8d12140aea778c196eebafe76dd/src/ValidationPackage.Model/Author.fs), plus handwritten [JavaScript](https://github.com/nfdi4plants/arc-validate-package-registry/blob/ee5d98536873a8d12140aea778c196eebafe76dd/tests/ValidationPackage.NativePackageSmoke/javascript.mjs) and [Python consumers](https://github.com/nfdi4plants/arc-validate-package-registry/blob/ee5d98536873a8d12140aea778c196eebafe76dd/tests/ValidationPackage.NativePackageSmoke/python.py). Reuse their API patterns, not their domain-specific defaults or validation policies.
- [DataHubClient native packages and consumer checks](https://github.com/nfdi4plants/DataHubClient#package-smoke-tests): curated exports and tests using installed packages from native code.
- Fable's [JavaScript features](https://fable.io/docs/javascript/features.html) and [Python features](https://fable.io/docs/python/features.html) describe attached members, overload limitations, and erased unions. Its [JavaScript](https://fable.io/docs/javascript/compatibility.html) and [Python compatibility](https://fable.io/docs/python/compatibility.html) pages describe collection and option representations. Check these against the pinned compiler rather than assuming identical output across versions or targets.

## PolyglotSQLite: Portable SQLite Infrastructure

`src/PolyglotSQLite` is an independent .NET Standard 2.0 library with native Node/TypeScript and Python entrypoints. Track milestones, repairs R01-R11, and actual verification in [the PolyglotSQLite plan](plans/polyglot-sqlite.md), updating it in every implementation commit. The [guide](docs/project/polyglot-sqlite.md) describes the supported API and ownership contract.

- Reuse the pinned Microsoft.Data.Sqlite, better-sqlite3, and stdlib sqlite3 providers. Keep ProcessCore self-contained; do not add references from ProcessCore to this library. ARC schemas, entity mappings, and ID policies belong to a future Layer 2 implementation.
- Public values, rows, parameters, transaction scopes, and table metadata are attached classes. Keep ordinary F# unions and maps internal. Preserve all five SQLite storage classes, exact signed int64 values, ordinal columns, duplicate column names, and absence versus SQL NULL.
- Use explicit Python representation boundaries for native int/float/bytes and transpiled F# numeric/array behavior. Do not apply the ARCBaseModel AST compatibility pass to this library or patch the Fable runtime. Verify compiled output and native consumers; CompiledName behavior differs for methods and properties.
- Ordinary statements must not commit a surrounding transaction. Validate one statement before provider preparation, generate nested savepoints internally, and keep scripts separate. Borrowed handles require exclusive use and must be idle before wrapping; borrowed Python detect_types=0 is an explicit caller precondition because sqlite3 cannot expose that flag.
- Run `TestPolyglotSQLite` for the complete contract: shared tests on all three runtimes, handwritten native callers, declaration checks, local packed artifacts, and every cross-runtime database writer/reader combination. The independent targets are `TestPolyglotSQLiteDotNet`, `TestPolyglotSQLiteJS`, `TestPolyglotSQLitePy`, `TestPolyglotSQLiteNative`, and `TestPolyglotSQLiteInterop`.

## ARCManagement: Graph and Workspace Management

`src/ARCManagement/ARCManagement.fsproj` builds one portable assembly over
ARCBaseModel and PolyglotSQLite. Track implementation and actual verification in
[the management plan](plans/arc-management.md). The
[management guide](docs/project/arc-management.md) and
[workspace guide](docs/project/arctrl.md) document the public boundaries.

- `ARCManagement.Session` owns one connection and creates, opens, imports, and
  enumerates independently managed entries. `createArc` and `openArc` return
  `ManagedGraph`. Repeated opening returns the same active `ManagedGraph`. Keep `ArcInfo`,
  `ArcId`, and `listArcs` names. Session closure invalidates all its graphs.
- `ARCManagement.ManagedGraph` owns model access, operation groups, identity,
  validation, transactions, history, graph lifetime, and root-only `toYaml()`.
  Management keeps no live model objects. A graph caches the stored rows of its
  entry and SQL is authoritative. Reads check the revision with one query,
  reload inside a read transaction when another connection committed, and
  hydrate fresh immutable values on demand with a per-revision cache. Identity
  across revisions is not promised. Returned values never change, including
  after later edits, undo/redo, and graph or session closure. Preserve supplied
  references, ordering, and duplicates. ID assignment belongs here, never Layer
  1. It applies only to persisted copies and never modifies supplied objects.
- Operation groups cover 13 entity types with `create`, `add`, `upsert(value,
  ?recursive)`, `get`, `list`, and `delete`, one `set<Property>` per property
  (an option clears), and `add<Item>`/`remove<Item>` per entity collection.
  Setters and add/remove return the updated immutable value, `create`/`add`/
  `upsert` return the stored copy, `delete`/`undo`/`redo` return unit, and `list`
  returns an immutable collection. Subjects are identified by `Id`, must exist
  with the group's kind, and setters apply to current stored state. `add` is
  strict, `upsert` inserts an unknown root or replaces the root's own fields,
  and nested stored entities are links unless `recursive = true` (a positional
  optional argument in JavaScript and a keyword in Python). Writes run in one
  outermost transaction that bumps the arc revision first. An unchanged result
  rolls back, so revision, history, and redo stay unchanged. There are no
  caller revision tokens, stale-handle errors, ownership registry, or
  direct-mutation detection. Native numbers for Number values become floats at
  the management boundary, and Booleans are rejected.
- `ARCManagement.DatasetYaml.encode/decode` are detached codecs. Preserve absent
  IDs and distinct objects with equal supplied IDs, sharing, cycles, extensions,
  and scalar types through serialization-local anchors/aliases. Do not modify
  supplied objects or perform SQL/filesystem/ownership operations.
  `Session.importYaml` subsequently applies normal adoption rules, assigning
  missing IDs to persisted copies and rejecting distinct-object ID collisions in a fresh unbound
  entry. Managed exports retain root-only `id`/`$ref` output and do not advance
  checkpoints or history. Snapshot codecs remain private.
- Top-level `ARCtrl.ARC` lives in the nested `ARCtrl/` folder. Keep workspace
  policy, fixed layout paths, and workspace-specific identifier/path helpers
  there. Generic operations, runtime, storage, serialization, UUID generation,
  and portable filesystem adapters stay outside it. Compile generic APIs first;
  `ARC.importFolder` takes a typed `ARCManagement.Session`.
- `ARC.bind(graph, folder)` applies binding policy; `ARC.fromGraph(graph)` requires
  a retained binding without reading files. Forward `Model`, operation groups,
  and `History` to the exact exposed `ManagedGraph`. Folder APIs and `IsDirty`
  belong to ARC. Closing any wrapper closes its graph and invalidates every
  shared wrapper; only `create`/`openFolder` also close their privately owned
  session. Borrowed sessions and sibling graphs remain usable.
- Retain `arc.yml`, `.arc/arc.sqlite`, per-entry archives,
  and save/recovery checkpoint guarantees. Workspace imports initialize the
  graph, binding, and baseline transactionally. Project-file integration is
  separate future work. Experimental SQL storage at schema version 5 scopes the
  core profile tables by `arc_id` and has no migration (opening rejects version 4
  databases with a re-import hint). Opening validates every expected object.
- Generic native entrypoints are `arc-management` / `arc_management`; workspace
  entrypoints are `arc-management/arctrl` / `arc_management.arctrl`. Use shared
  JavaScript chunks and one Python implementation tree so model, SQLite, and
  management class identity is common. Keep curated declarations and the
  existing model Python numeric compatibility handling. Do not retain legacy
  project/package aliases or introduce production dependencies.
- Run `TestARCManagement` for shared .NET/JS/Python behavior, handwritten native
  consumers, declarations, and packed artifacts. Independent targets are
  `TestARCManagementDotNet`, `TestARCManagementJS`, `TestARCManagementPy`, and
  `TestARCManagementNative`. `TestCoreSQL` reuses the .NET target. Also run
  `TestBaseModel` and `RunTests` for changes to these boundaries; a skipped or
  failed target is not a passing check.

## Commands

```powershell
.\build.cmd BuildSolution
.\build.cmd BuildBaseModel
.\build.cmd TestBaseModel
.\build.cmd BuildPolyglotSQLite
.\build.cmd TestPolyglotSQLite
.\build.cmd TestARCManagement
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

For portable domain API changes, run the shared behavioral tests on .NET, JavaScript, and Python, then inspect the emitted classes and type declarations. `TestBaseModel` covers all three runtimes and native consumers and is included in `RunTests`. The legacy ProcessCore Python suite remains separate; its skipped `runTestsPy` target is not evidence of cross-target support. A skipped or failing target is not a passing check.

The [Layer 1 guide](docs/project/base-model.md) documents construction, native values, build artifacts, and Layer 2 identity policies. The FAKE targets `TestBaseModelDotNet`, `TestBaseModelJS`, `TestBaseModelPy`, and `TestBaseModelNative` can also run independently with their required build dependencies.

Add handwritten JavaScript/TypeScript and Python consumer tests alongside the shared F# tests. Import through the public entrypoints and exercise construction, property reads/writes, optional values, native arrays/lists, spec alternatives, duplicate preservation, and shared entity references. Type-check TypeScript callers as well as running JavaScript and Python callers. When package artifacts are produced, run these consumers against the packed artifacts too; this catches export and dependency defects that transpiled F# tests cannot detect.
