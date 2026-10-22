# Immutable ARCBaseModel with SQL-backed management

Status: accepted. Phases 0-5 are implemented (Layer 1, management core, storage and API, including the revision refresh protocol and the Python keyword `recursive`, and the YAML codec). The close-out remains. The YAML phase recorded its mapping decisions in the [management guide](../docs/project/arc-management.md#yaml-representation). Revised 2026-10-21 to minimize complexity in the
model, backend and API. Supersedes [the event plan](mutation_safety_event.md) and the
first implementation attempt on branch `arcmanagement–immutable`.

## Summary

Replace mutable domain objects with immutable attached classes. ARCManagement owns
current state in SQLite, validation, IDs and history. Reads return immutable values.
Direct mutation becomes impossible, so management no longer needs the live-object
machinery that detects it.

This is a breaking change of the experimental model, management API, database layout
and managed YAML format. It adds no production dependencies or compiler upgrade. The
pinned compiler stays Fable 5.20.0.

## Lessons from the first attempt

The `arcmanagement–immutable` branch grew Layer 1 from 474 to 2,154 lines and the
backend by about 1,500 lines. These were the main causes, and this plan excludes all of
them:

- a public `ModelBuilder` (912 lines) with draft and reference tokens for detached cycles
- runtime sealing, including Python `__getattribute__`/`__setattr__` overrides
- mangled-name `IReadOnlyList` adapters on frozen arrays and tuples
- full-replacement baselines, private provenance caches and two conflict exception types
- splice-level history and native named-option adapters

The current branch has these complexity sources, which this plan removes:

- live canonical instances, tombstones and a global ownership registry
- `check()` re-capturing the whole graph before every command
- 19 mirror tables rebuilt on every edit although production never reads them, plus
  three full YAML copies of the state per edit and two full snapshots per history entry
- three YAML encodings, one implemented as a text round trip through another
- a stringly typed property table next to about 240 lines of per-entity mapping
- 278 operation-group members, 155 of them untested

## Layer 1 model

- Keep the 13 entity classes, `EntityObject`, the `Entity` erased union, the value
  classes (`EntityPropertyBag`, `EntityCollection`, `EntityNull`, `EntityBlob`), the
  domain unions and the short constructors.
- Publish getter-only properties. Each class stores its fields in one internal immutable
  state record. `EntityObject` holds a common record for `id`, `additionalTypes` and
  extensions.
- Copies clone the object and replace the record. The clone uses `MemberwiseClone` on
  .NET and one emitted helper per Fable target (JS `Object.assign` over the prototype,
  Python shallow copy). Unchanged references and collections are shared.
- Each class declares one static `with<Property>(value, replacement)` per own property.
  Optional properties take an option, so `None` or `undefined` clears. There are no
  `clear*` methods. Copies apply the constructor's validation to the replaced value.
- `EntityObject` declares generic statics that return the concrete type:
  `withId`, `withAdditionalTypes`, `withProperty(key, value)` and `withoutProperty(key)`.
  Entity classes do not redeclare them.
- Collections are typed `seq<'T>` in F#. JavaScript stores them as frozen arrays and
  Python as tuples. On .NET they are `ReadOnlyCollection` instances. Constructors and
  copies consume iterables once.
- `EntityPropertyBag` is read-only (`Keys`, `Contains`, `Get`) and `EntityCollection` is
  immutable (`Count`, `Get`). Reserved-name checks remain.
- There is no runtime sealing. Getter-only properties already reject assignment
  (strict-mode TypeError, Python AttributeError), and frozen collections reject
  `push`/`append`. Accessing internal state is an unsupported bypass. Management never
  retains caller objects, so a bypass cannot corrupt persisted state.
- There is no public builder. Constructors and copies cannot create cycles. Cycles arise
  from management links by ID and from YAML `@id` references. Hydration allocates
  objects and links them through the internal state setters before publishing them.
- IDs stay optional. Layer 1 never generates or infers them.

Native staging:

- `build/base-model.mjs` declares `Iterable` getters as `readonly T[]`, supports static
  and generic static methods, and hides the fixed set of internal member names.
- `build/base-model-python.py` emits `@staticmethod` stubs and declares tuple getters as
  `tuple[T, ...]`. Generic statics use a `TypeVar`. Erased `with_<property>` parameter
  types come from the getter's existing mapping, so `ERASED_FIELDS` needs no new
  entries. Internal members stay hidden.
- Keep the Python numeric compatibility pass and shared class identity across packages.

## Management runtime

- `GraphContext` caches an immutable `State` (rows keyed by ID, with ordinals) together
  with the ARC revision it was read at. SQL is authoritative.
- Each write runs under the existing `Repository.Access` gate in one outermost
  transaction, opened with `BeginTransaction()` and finished with an explicit commit or
  rollback. Its first statement is
  `UPDATE arc SET revision = revision + 1 WHERE arc_id = $arc RETURNING revision`, which
  takes the write lock. If the previous revision differs from the cache, the command
  reloads its state from SQL inside the same transaction. Reads check the revision with
  one query. On a mismatch they reload inside a read transaction that selects the
  revision again together with all tables. Commits from other connections are therefore
  picked up automatically. There are no stale-handle errors, conflict exception types
  or caller revision tokens.
- A command is a pure `State -> State` transform. The existing change, collection and
  delete transforms are reused. An unchanged result rolls back and leaves revision,
  history and redo untouched. Otherwise the command validates once, diffs entities and
  properties, writes only the changes plus one history row, commits, and then replaces
  the cached state. Failures leave SQL and the cache unchanged, and provider errors such
  as `SQLITE_BUSY` surface unchanged.
- Reads hydrate immutable values from the cached rows with a per-revision object cache.
  Identity across separate reads is not promised. Returned values stay valid after
  later edits, undo/redo and graph or session closure.
- Inputs to `add` and `upsert` are captured by reference identity. Missing IDs are
  assigned only to the persisted copies, and supplied objects are never modified. Values
  read from another ARC are accepted, and their IDs resolve in the destination.
- A typed property table replaces the string-based `Model.specifications`. Each entry
  has a shape (`Text`, `Texts`, `Link` and `Links` with target kinds, `TextOrNumber`,
  `TextOrLink`, `SampleOrData`), a required flag, its SQL column or association and its
  YAML wire name. The table drives validation, SQL reads and writes, the YAML codec and
  delete cascades. Typed per-entity code remains only for capture (getters to row) and
  hydration (row to state record).
- Deletion rules stay. The root cannot be deleted, Dataset deletion cascades to
  exclusively owned descendants and processes, and surviving required core links or
  extension references block deletion.
- `openArc` still returns the active graph for an entry.

The redesign removes the entity dictionary and tombstones, the `Ownership` module with
`Claim`/`Release`/`AssertAvailable`, `check()`, live `restore` and object rollback,
`Model.setId` and ID rollback on undo, `SuppliedId` tracking, `ResizeArray.replace`,
repeated validation per command, and `AppliedOperation`.

## Storage

Schema version 5 replaces version 4. Version 4 databases are rejected with a hint to
re-import from `arc.yml`. There is no migration.

Core tables follow [the core SQL profile](../schemas/sql/001_core.sql). Every table,
index and view is created with an added `arc_id`:

- entity tables use primary key `(arc_id, id)`
- association tables use primary key `(arc_id, owner_id, position)`
- foreign keys include `arc_id`, and columns, CHECKs and deferred foreign-key actions
  are unchanged
- `process_edges` and `core_validation_errors` expose `arc_id`
- each core entity row also references the entity registry

Management tables:

| Table | Content |
|---|---|
| `repository` | Schema version |
| `arc` | Root ID, folder, baseline, saved graph, history cursor, revision. Replaces `session` and drops its `state` text |
| `entity` | Registry of every entity, including generic typed objects: ID, type, ordinal |
| `entity_additional_type` | `additionalTypes` of non-core typed objects |
| `entity_extension` | Extension values and references. Blob values are canonical base64 text in `value_text`, because the model represents blobs as base64 text, so the version 4 `value_blob` column is gone. Value lookup indexes cover `(arc_id, property, value_text)` and `(arc_id, property, value_number)` |
| `history` | Per operation: encoded before/after rows of the changed entities |
| `arc_archive`, `archived_history` | State and history displaced by explicit `"yml"` recovery |

The `journal` table, the typed `payload` tables, `entity_value`, `entity_reference` and
the `dataset_part`/`dataset_process` mirrors are removed.

- Writes update changed scalar columns, replace only changed association rows, insert
  new entities and delete removed entities last, because the profile's `ON DELETE
  RESTRICT` is checked immediately. A title edit is a single `UPDATE`. List edits keep
  the common prefix, so appending one element writes one row. History grows with the
  size of the changed entities and is independent of the rest of the graph.
- Opening and refreshing read each core and association table once per ARC, plus the
  registry and extensions.
- Opening validates the required tables and constraints without initializing or
  migrating.
- A conformance test runs on all runtimes. It loads `001_core.sql` and the management
  schema into in-memory databases and compares every core table's columns, primary key,
  foreign keys and the views, ignoring `arc_id`. The seed example written through
  management must pass the scoped `core_validation_errors` and `PRAGMA
  foreign_key_check`.

## Management API

Operation groups stay typed and become uniform:

| Members | Per group |
|---|---|
| `create(required arguments)`, `add(value)`, `upsert(value, ?recursive)`, `get(id)`, `list()`, `delete(value)` | Every entity type |
| `set<Property>(value, replacement)` | Every property. An option clears, and collections are replaced from iterables |
| `add<Item>(value, target)`, `remove<Item>(value, target)` | Every entity collection |

The `Entity` group keeps CRUD, `setAdditionalTypes`, and `setProperty`/`addProperty`/
`removeProperty` for generic extension values.

The groups lose 34 `clear*` methods, 12 typed duplicates such as `setInputSample` and
`setValueText`, 13 per-type `setAdditionalTypes`, 14 typed extension helpers,
`movePart`/`moveProcess`, `getProperty`/`hasProperty` and `AppliedOperation`. Native
callers pass erased values directly, F# callers use the domain union cases, and
extension reads use `EntityProperties` on the value. The groups shrink from 278 to
about 200 members. `Session`, `ManagedGraph`, `DatasetYaml` and the ARCtrl `ARC` facade
keep their shape.

Semantics:

- A subject is identified by its `Id` and must match the group's kind. Setters apply to
  the current stored state. The setter always wins, and there are no staleness checks.
- Relationship targets must already be stored in this ARC.
- Setters return the updated immutable value. `create`, `add` and `upsert` return the
  stored copy, including assigned IDs. `delete`, `undo` and `redo` return unit.
- `remove<Item>` deletes the first matching occurrence. Order, duplicates and shared
  references are preserved.
- `add` inserts strictly, and an existing ID fails. New nested entities are inserted
  too.
- `upsert(value)` inserts an unknown root or replaces the root's own fields. Nested
  entities with existing IDs are links and are never overwritten. New nested entities
  are inserted.
- `upsert(value, recursive = true)` writes every reachable submitted entity as the last
  truth. Distinct submitted objects sharing an ID must have equal definitions, and the
  command fails otherwise.
- Upserting a Dataset never changes which Dataset is the ARC root.
- `recursive` is an ordinary optional parameter: positional in JavaScript, a keyword in
  Python. There are no native option-object adapters.

Management staging derives union parameter types for setters from the base-model
property types, which replaces the hand-kept table in `build/arc-management-python.py`.

## YAML

One external codec follows the shape produced by
[the ProcessCore YAML codecs](../src/ProcessCore/YML/) and
[the YAML schemas](../schemas/yml/):

- `type` and `id` fields, ProcessCore wire names such as `identifier`, `additionalType`,
  `hasPart`, `parameterValue` and `additionalProperty`, and `{"@id": ...}` references
- processes with equal non-endpoint state grouped into `inputs`/`outputs` arrays, which
  decoding expands pairwise (the Nth input with the Nth output)
- lenient decoding of unknown keys into extensions, and extensions encoded as overflow
  keys

Encoding has two modes, `DatasetYaml.encode` (inline) and `DatasetYaml.encodeIndexed`.
Indexed mode writes Agents, Recipes and Annotations once in top-level sections and
references them by `@id` everywhere else. The mapping step decides which mode managed
exports use.

Identity:

- Equal `id` values within one document denote one entity, and differing definitions
  fail.
- A reference to an entity that is still being written uses `@id`, which encodes cycles.
- Entities without IDs are written inline at each occurrence. Their sharing is not
  preserved, and cycles among them are rejected. This replaces the anchor format's
  guarantees.

The codec serves `DatasetYaml.encode/decode`, `ManagedGraph.toYaml`, workspace import
and save (`arc.yml`), and later the project-file `dataset.yml` codec. It replaces the
`$ref` and anchor formats. The internal row codec remains only for history and archive
payloads.

Start with a base-model-to-wire mapping table in `docs/project/` that settles these
points:

- multiple `identifiers` versus the single `identifier`
- `conformsTo`, which the YAML schemas do not define
- `additionalTypes` versus `additionalType`
- `descriptors` versus `dataContexts`
- Agent field names (`email`, `affiliation`, `jobTitles`) and DefinedTerm `TAN` versus
  `termCode`
- Data inside `hasPart`
- the name of the Agent index section, since Dataset already uses `agents`

Interop checks decode `examples/process_core/minimal.yml`, `examples/administrative/`
and `examples/datamap/`. A .NET test with a test-only ProcessCore reference checks that
ProcessCore's lenient parser reads this codec's output and that this codec reads
ProcessCore's output.

## Workspace

`ARCtrl.ARC` keeps its public shape and forwards `Model`, the operation groups and
`History` to its exact `ManagedGraph`. Save, `IsDirty`, binding and recovery read
current state. Direct-mutation checks disappear. `IsDirty` compares a lazily computed
export with the saved graph. Filesystem conflict checks, failed-save guarantees and the
archive for explicit `"yml"` recovery remain.

## Phases and verification

Each phase ends with its FAKE targets passing. A skipped target is not a pass.

| Phase | Scope | Checks |
|---|---|---|
| 0. Spike | `EntityObject`, `Sample`, `Process` and `Dataset` with state records, the clone helper, frozen collections, generic statics and union `with*` methods. Staged declarations. A Python keyword `recursive` on a sample method | `TestBaseModel` on all runtimes, plus inspection of emitted classes and declarations |
| 1. Layer 1 | All entity and value classes, both stagers, shared and native Layer 1 tests, [the base-model guide](../docs/project/base-model.md) | `TestBaseModel` |
| 2. Management core | Typed property table, cached state, capture and hydration. Removal of the live-object machinery, while old storage and YAML still work on rows | `TestARCManagement` |
| 3. Storage | Core-shaped schema, diff writer, history deltas, conformance test. Removal of `journal` and the mirrors | `TestARCManagement`, `TestCoreSQL` |
| 4. API | Trimmed groups, revision refresh, upsert semantics, ARC facade, management stagers, shared and native tests | `TestARCManagement` |
| 5. YAML | Mapping table, ProcessCore-shaped codec, workspace import and save, interop | `TestARCManagement` |
| 6. Close-out | AGENTS.md, guides, `schemas/sql/design.md`, implementation plans | `RunTests`, packed artifacts, declarations, docs link check |

Acceptance coverage:

- Assignment and collection mutation fail natively. Copies are pure and share unchanged
  references. Options clear. Numeric, text and Boolean alternatives, ordering,
  duplicates and sharing survive copies, storage and YAML.
- Returned values stay unchanged after edits, undo/redo and closure. Fresh reads reflect
  edits, history and other connections' commits.
- Supplied inputs keep absent IDs while results carry assigned IDs.
- Setters on older values preserve current unrelated fields. Nonrecursive upsert never
  overwrites nested stored entities. Recursive upsert replaces them and rejects
  conflicting definitions.
- No-op edits change no revision, history or redo. Failed validation or commit changes
  nothing.
- Deletion cascades and protections, root safety, cycles through management and YAML,
  extensions, and the core-profile conformance test.
- Workspace save, recovery and archive. Handwritten JS/TS/Python consumers against
  staged and packed artifacts. Every runtime writer/reader combination.

Track line counts of `src/ARCBaseModel` (474 lines) and of `Runtime/`, `Internal/`,
`Storage/` and `Serialization/` in `src/ARCManagement` (1,477 lines) per phase.
Investigate any phase that grows them substantially.
Storage phase result: the management directories (`Runtime/`, `Internal/`, `Storage/`
and `Serialization/`) grew from 1,479 to 1,989 lines. Most of the growth is the 93 DDL
statements in `CoreSchema.fs` and the diff writer.
Runtime and API result: the same four management folders shrank from 1,999 lines
(measured at 1ab7328^) to 1,938 lines (measured at 5f527e0) after the runtime rewrite.
Phase 1 result: `src/ARCBaseModel` grew from 474 to 815 lines and the management
directories from 1,477 to 1,479 lines. Most of the Layer 1 growth is the state
records and about 73 short `with*` copy members that replace mutable properties. Record actual results in
[the Layer 1 plan](arc-base-model-layer-1.md) and [the management plan](arc-management.md).
