---
title: ARCManagement
category: Project
categoryindex: 2
index: 10
---

# ARCManagement

ARCManagement manages portable ARCBaseModel graphs in memory or in a retained
SQLite repository. It owns connections, graph mutation, identity, validation,
history, and YAML codecs. Workspace operations live in the nested ARCtrl part of
the same assembly; see the [workspace guide](arctrl.md). The
[management plan](../../plans/arc-management.md) records actual verification.

## Repository and graph lifecycle

```fsharp
open ARCBaseModel
open ARCManagement

let session = Session.createInMemory()
// Alternatively: Session.createFile("collection.sqlite")
// or Session.openFile("collection.sqlite").
let graph = session.createArc(Dataset(["process-provenance"], ["example"]))
let sample = graph.Sample.create("sample")
let proc = graph.Process.create("process")
let withInput = graph.Process.setInput(proc, Some(EntityReference.Sample sample))
graph.Dataset.addProcess(graph.Model, withInput) |> ignore
graph.History.undo()
graph.History.redo()
let yaml = graph.toYaml()
let arcId = graph.ArcId
graph.close()
let reopened = session.openArc(arcId)
let metadata = session.listArcs()
session.close()
```

`Session` owns one PolyglotSQLite connection. `createFile` exclusively creates a
new database. `openFile` requires an existing schema-version-5 repository and
validates its columns, keys, and constraints; it never initializes or migrates
it. In-memory storage lasts until its session closes. Access is synchronous and
serialized.

`createArc` returns a `ManagedGraph` over a persisted copy of the supplied graph.
It preserves supplied IDs, assigns missing IDs to the copy, and rejects distinct
objects with the same ID. The supplied objects are never modified. Generated
repository `ArcId` is independent of the root Dataset ID. Equal domain IDs in
different entries identify independent objects.

Opening a database and listing `ArcInfo` metadata do not hydrate graphs or read
folders. Metadata contains `ArcId`, `RootEntityId`, and optional `Folder`.
`openArc` loads the selected entry and returns the same active `ManagedGraph`
and operation groups on repeated opens. The graph caches the stored rows of its
entry (state, ordinals, cursor, revision, folder, baseline, and saved graph).
SQL is authoritative. Closing a graph releases its context. Closing the session
invalidates all its graphs and releases its connection. Values already returned
stay valid after either. No close implicitly exports files.

`ManagedGraph` exposes `Model`, `ArcId`, `DatabasePath`,
`HasSessionOnlyObjects`, `History`, the operation groups, `toYaml()`, and
`close()`. Folder binding, saving, recovery, and `IsDirty` belong to
[`ARCtrl.ARC`](arctrl.md).

## Entity operations and history

The entity groups are `Dataset`, `Process`, `Sample`, `Data`, `Recipe`,
`Annotation`, `FormalParameter`, `DefinedTerm`, `DefinedTermSet`, `Descriptor`,
`Agent`, `Organization`, and `ScholarlyArticle`, alongside generic `Entity` and
`History`. Each entity group provides `create`, `add`, `upsert`, `get`, `list`
and `delete`. It has one `set<Property>` method per property, where an option
argument clears an optional value, and `add<Item>` and `remove<Item>` methods per
entity collection, such as `addProcess` and `removePart`. The `Entity` group has
the same CRUD members plus `setAdditionalTypes`, `setProperty`, `addProperty`, and
`removeProperty`. The groups hold about 199 members in all. Dataset creation
defaults to process provenance. Use `add(dataset)` for other profiles. Descriptor
creation requires Sample/Data.

```fsharp
let sample = graph.Sample.create("sample")
let proc = graph.Process.create("process")
let linked = graph.Process.setInput(proc, Some(EntityReference.Sample sample))
let root = graph.Dataset.addProcess(graph.Model, linked)
let cleared = graph.Process.setInput(linked, None)
```

```javascript
const sample = graph.Sample.create("sample");
const proc = graph.Process.create("process");
const linked = graph.Process.setInput(proc, sample);
const root = graph.Dataset.addProcess(graph.Model, linked);
graph.Dataset.upsert(root, true); // recursive is a positional optional argument
```

```python
sample = graph.Sample.create("sample")
proc = graph.Process.create("process")
linked = graph.Process.set_input(proc, sample)
root = graph.Dataset.add_process(graph.Model, linked)
graph.Dataset.upsert(root, recursive=True)  # recursive is a keyword
```

Management keeps no live model objects. Every returned entity is an immutable
Layer 1 value that never changes afterwards. That holds across later edits,
undo and redo, and graph or session closure. `create`, `add`, and `upsert` return
the stored copy with its assigned IDs. Setters and `add<Item>`/`remove<Item>`
return the updated value. `delete`, `undo`, and `redo` return unit. `list`
returns an immutable collection in ordinal order (a read-only collection on .NET, a
frozen array on JavaScript, and a tuple on Python). Identity across revisions is
not promised, so compare entities by their IDs.

Subjects are identified by their `Id` and must exist with the group's kind. A
group rejects a subject whose stored kind differs from its own. The `Entity` group
accepts any kind. Setters apply to the current stored state, so the setter always
wins over a subject value that has fallen behind. Relationship targets must
already be stored, and a relationship or extension target whose ID is stored with
a different kind is rejected.
`add` inserts strictly: an existing ID fails, and new nested entities are inserted
with it. `upsert` inserts an unknown root or replaces the root's own fields.
Nested entities with stored IDs are links and stay unchanged unless `recursive`
is true. Then every reachable submitted entity is written as the last truth.
With `recursive`, distinct submitted objects with one ID are accepted when their
definitions are equal. Equal definitions means equal stored rows, so nested
objects without IDs must be the same objects. Two distinct ID-less children make
the definitions differ. Every other adoption rejects distinct new objects that
share one ID: `add`, non-recursive `upsert`, `createArc`, `importYaml`, and
workspace import and recovery.
Upserting a Dataset never changes the ARC root. Cross-type ID collisions are
rejected. History entries for `add` and `upsert` are named `<Kind>.add` and
`<Kind>.upsert`, for example `Process.add`.

Management never modifies supplied objects. It assigns missing IDs to the
persisted copies. ID assignment belongs to management, never Layer 1.
Identity is not inferred from names, paths, or equal values. Values read from
another ARC are accepted and resolve by ID in the destination. Native numbers
supplied for Number values become native floats at the management boundary, and
Booleans are rejected there.

Collection edits preserve order, duplicates, and shared references. `removePart`
and its siblings delete one occurrence without unregistering the target.
Replacement setters support shared or repeated members. Process endpoints remain
optional and singular. `setInput` and `setOutput` take an `EntityReference` (a
Sample or Data value) and accept absence. Text, absence, empty text, zero, and
fractional numbers remain distinguishable.

Each command is a pure transform of the stored state, run in one outermost
transaction. Its first statement is
`UPDATE arc SET revision = revision + 1 WHERE arc_id=$arc RETURNING revision`.
If the cached revision is older, the transaction reloads the state first. A
command whose result equals the current state rolls back, so revision, history,
and redo stay unchanged. A changed result is validated, written as a row-level
diff with one `history` record, committed, and then published to the cache.
Provider errors such as `SQLITE_BUSY` surface unchanged. There are no stale-handle
errors and callers pass no revision tokens.

Reads check the revision with one query. When another connection has committed,
the read reloads the stored rows inside a read transaction. Reads hydrate fresh
immutable values on demand and cache them per revision. Revisions are entry-local,
so edits to another entry never affect a graph. SQLite permits one writer.

Undo and redo are linear and persisted, and they apply the recorded before and
after rows through the same writer. Editing after undo discards redo. Undoing an
add removes the added rows, and redo restores them with their assigned IDs. Saving
preserves history and standalone objects.

Root deletion is rejected. Dataset deletion cascades only to descendants and
processes without surviving owners. Required Descriptor targets and surviving
extension references prevent deletion. Undo restores affected links.

## YAML workflows

```fsharp
let detached = Dataset(["process-provenance"], ["detached"], id = "detached-root")
let text = DatasetYaml.encode(detached)
let indexed = DatasetYaml.encodeIndexed(detached)
let decoded = DatasetYaml.decode(text)
let imported = session.importYaml(text)
let managedText = imported.toYaml()
```

One external codec reads and writes the YAML shape of the ProcessCore codecs
(`src/ProcessCore/YML`). `DatasetYaml.encode`, `encodeIndexed` and `decode` perform
no database or filesystem work and assign no domain IDs. Encoding does not modify
supplied objects. `graph.toYaml()` and the workspace `arc.yml` use the inline mode of
the same codec. Documents of the earlier prototype formats (`$ref` objects and
anchors with aliases) are no longer readable, and there is no migration.

`session.importYaml(yaml)` decodes a detached Dataset, then adopts it through the
ordinary management rules. Missing IDs are assigned, and distinct objects that share
one ID are rejected. The decoder produces one object per ID, so a document that
repeats an entity inline with equal fields imports. A document that repeats an ID
with differing fields fails in `decode`. The import creates a fresh unbound entry
with empty history.

`graph.toYaml()` exports only the graph reachable from `Model` and writes the
managed ID of every entity. Standalone registered objects remain SQL-only. Exporting
text changes no files, save checkpoints or history. Snapshot codecs remain private.
The YAML schemas in `schemas/yml/` stay unchanged.

### YAML representation

Every entity is a mapping with `type` (the base kind name) and `id` (only when the
entity has one). Collections are sequences and keep their order and duplicates.
Absent optional values and empty collections write nothing.

| Entity | Base property | Wire key | Form | Decode aliases |
| --- | --- | --- | --- | --- |
| all | `id` | `id` | text | `@id` |
| all | type name | `type` | kind name | `@type`, `schema:Dataset`, `bioschemas:LabProcess`, `bioschemas:Sample`, `schema:PropertyValue`, `bioschemas:LabProtocol`, `bioschemas:FormalParameter`, `File` and `MediaObject` (the last two mean Data) |
| all | `additionalTypes` | `additionalType` or `additionalTypes` | one or many text | |
| Dataset | `conformsTo` | `conformsTo` | sequence of text | scalar |
| Dataset | `identifiers` | `identifier` and `identifiers` | `identifier` holds the first item, `identifiers` the full sequence when there is more than one | |
| Dataset | `title`, `description`, `license`, `datePublished`, `dateCreated`, `dateModified` | same names | text | |
| Dataset | `hasParts` | `hasPart` | sequence of Datasets | Data entries go to `dataFiles` |
| Dataset | `dataFiles` | `dataFiles` | sequence of Data | |
| Dataset | `agents` | `agents` | sequence of Agents | |
| Dataset | `citations` | `citations` | sequence of ScholarlyArticles | |
| Dataset | `processes` | `processes` | sequence of Process entries, grouped | |
| Dataset | `descriptors` | `descriptors` | sequence of Descriptors | |
| Dataset | `additionalProperties` | `additionalProperty` | sequence of Annotations | |
| Process | `name` | `name` | text | |
| Process | `input`, `output` | `inputs`, `outputs` | sequences of Samples or Data, aligned by position | |
| Process | `executesRecipe` | `executesRecipe` | Recipe | `executesProtocol` |
| Process | `parameterValues` | `parameterValue` | sequence of Annotations | |
| Recipe | `name`, `description`, `version`, `url` | same names | text | |
| Recipe | `parameters` | `parameters` | sequence of FormalParameters | |
| Recipe | `intendedUse` | `intendedUse` | quoted text, or a DefinedTerm | |
| Recipe | `additionalProperties`, `components` | `additionalProperty`, `components` | sequences of Annotations | |
| Sample | `name` | `name` | text | |
| Sample | `additionalProperties` | `additionalProperty` | sequence of Annotations | |
| Data | `path`, `selector`, `selectorFormat`, `encodingFormat` | same names | text | |
| Data | `hasParts` | `hasPart` | sequence of Data | |
| Data | `additionalProperties` | `additionalProperty` | sequence of Annotations | |
| Annotation | `name`, `unit`, `nameTAN`, `valueTAN`, `unitTAN` | same names | text | |
| Annotation | `value` | `value` | text always quoted, numbers plain | |
| Annotation | `instanceOf` | `instanceOf` | FormalParameter | |
| FormalParameter | `name`, `nameTAN` | same names | text | |
| FormalParameter | `defaultValue` | `defaultValue` | Annotation | |
| DefinedTerm | `name`, `identifier` | same names | text | |
| DefinedTerm | `tan` | `TAN` | text | |
| DefinedTerm | `inDefinedTermSet` | `inDefinedTermSet` | URL text, or a DefinedTermSet | |
| DefinedTermSet | `name`, `identifier` | same names | text | |
| Descriptor | `describes` | `describes` | Sample or Data | |
| Descriptor | `annotations` | `annotations` | sequence of Annotations | |
| Organization | `name`, `url` | same names | text | |
| Agent | `name`, `givenName`, `familyName` | same names | text | `name` derived from `givenName` and `familyName` when missing |
| Agent | `emails` | `email` or `emails` | one or many text | |
| Agent | `affiliations` | `affiliation` or `affiliations` | one or many Organizations | |
| Agent | `identifiers` | `identifier` or `identifiers` | one or many text | |
| Agent | `additionalProperties` | `additionalProperty` | sequence of Annotations | |
| Agent | `jobTitles` | `jobTitles` | sequence of DefinedTerms | `jobTitle`, as one DefinedTerm or a sequence |
| ScholarlyArticle | `headline`, `creativeWorkStatus` | same names | text, DefinedTerm | |
| ScholarlyArticle | `identifiers` | `identifier` or `identifiers` | one or many text | |
| ScholarlyArticle | `authors` | `authors` | sequence of Agents | |
| ScholarlyArticle | `additionalProperties` | `additionalProperty` | sequence of Annotations | |

The wire names live in the typed property table (`Internal/Schema.fs`), which the
encoder and the decoder share. A one-or-many property writes the singular key with a
scalar or one object when the collection has exactly one item, and the plural key
with a sequence otherwise. The decoder accepts either key with either form and turns
a scalar into a one-item collection. A document that holds both keys of one property
fails. The same holds for aliases such as `jobTitle` next to `jobTitles`. `Dataset`
identifiers are the exception: `identifier` and `identifiers` appear together, and
`identifier` must repeat the first item of `identifiers`.

The `dataContexts` of ProcessCore documents have no base counterpart. They decode as
extension properties.

#### Identity and references

Equal `id` values within one document denote one entity. The decoder returns one
object for them, and two definitions with differing fields fail with "Conflicting
definitions for entity ID <id>". Both inline and indexed documents follow this rule.

The inline mode (`encode`, `toYaml`, `arc.yml`) writes every occurrence of an entity
with all its fields, as ProcessCore does. The only reference is `{"@id": id}` for an
entity that is still being written, which encodes a cycle. Entities without IDs are
also written inline at each occurrence. Their sharing is not preserved, so the
decoder returns distinct equal objects. The exception is the common fields of a
grouped process entry, whose entities stay shared (see the process entry rules below).
A cycle among entities without IDs fails on
encode, because it has no way to name the entity. Two distinct objects that write the
same ID must have equal definitions, or the encoder fails with the same conflict
message. The comparison is structural: it looks at the kind, the properties and the
extensions, compares a target that has an ID by that ID and an ID-less target by its own
structure, and never depends on how the entity is written.

The indexed mode (`encodeIndexed`) writes each Annotation, Recipe and Agent that has
an ID once, in the root sections `annotations`, `recipes` and `agentIndex`. An entry
carries `id` and `@id` with the same value. Every other occurrence, including the
Dataset `agents` list, is `{"@id": id}`. Entities without IDs stay inline.

The decoder reads both modes and documents that mix inline definitions with
references. A reference is a mapping with only `@id` or only `id`. In positions that
hold one entity or a list of entities, including Sample or Data endpoints, a plain
string is a reference as well. In positions that hold text or an entity (`intendedUse`
and `inDefinedTermSet`), a plain string is the text alternative. A reference to an ID
that no definition in the document supplies fails. Definitions that nothing references
are dropped. The decoder also accepts the sections `labProtocols` (an alias of
`recipes`) and `samples`, which supply definitions only.

#### Process entries

Consecutive processes in one `processes` list share one entry when each has at least
one endpoint, they have the same endpoint presence (input present, output present),
all of them have an ID or none has, and their remaining row data is equal (`name`,
`additionalType`, `executesRecipe`, `parameterValue` and extensions). Rows refer to
other entities by key, so two distinct ID-less recipes with equal content do not
group, while a recipe object that both processes share does. The entry writes `inputs` and `outputs` as arrays aligned by position. Its `id` is a
sequence of the member IDs, or a scalar for one process.

The decoder expands an entry to `max(1, inputs, outputs)` processes and pairs the Nth
input with the Nth output. An endpoint is absent where its array is shorter. An `id`
sequence needs exactly one item per expanded process, and a scalar `id` on an entry
that expands to several processes fails. The expanded processes share the entities of
the common fields (recipe, parameter values and extension values), with or without IDs,
so re-encoding groups them again. Each process gets its own endpoints. While the
encoder writes the endpoints of one member, the other members of the entry may appear
inline in them, for example through an extension, because they are not yet being
written.

#### Extensions

Extension properties are keys next to the wire keys of their entity. Text is quoted,
numbers, `true`, `false` and `null` are plain, a blob is a `!!binary` scalar holding
the base64 text, and collections are sequences. A typed object is written inline with
`type` and `id`, or as `{"@id": id}` under the rules above. The decoder restores the
alternative from the style and tag: quoted text is text, a plain finite number is a
number, `true` and `false` are Booleans, `null` and `~` are null, `!!binary` is a
blob, and any other plain scalar is text. A mapping with a type becomes a core entity
of that kind or a generic typed object. A mapping without a type becomes a generic
object of type `Object`. Unknown keys of an entity decode as extension properties.

An extension key that equals a wire key or alias of its entity fails on encode, and so
does a section name on the root. A message names the key and the kind.

#### Decoding leniency

The decoder accepts what ProcessCore documents contain.

- A Dataset without `conformsTo` gets the profiles that its own fields imply:
  `process-provenance` for `processes`, `semantic-designation` for `descriptors` and
  `administrative` for any administrative field, including Data entries in `hasPart`,
  which count as data files. A Dataset with none of these gets `administrative`.
- An entry without a `type` takes the kind of its position. In a position that holds
  Samples or Data, or Datasets or Data, an entry with a `path` is Data.
- Comments are ignored, whether they sit between mappings, inside an entry or between
  sequence items.
- A type alias (`File`, `MediaObject` and the `schema:` and `bioschemas:` names) means its
  core kind only where the position accepts that kind. The root accepts `schema:Dataset`,
  endpoint and `hasPart` positions accept `File` and `MediaObject` for Data, and index
  sections accept the aliases of their kind. In an extension, `type: File` stays a
  generic object of type `File`. Canonical names such as `type: Sample` mean the core
  kind everywhere.
- A plain scalar that looks like a finite YAML number is a number only in
  `Annotation.value` and in extension values. Every other text property stays text.
  The encoder quotes core text scalars and extension keys, IDs included, when a YAML 1.1
  or 1.2 reader could type them implicitly: empty text, the null and Boolean words
  (`null`, `~`, `true`, `false`, `yes`, `no`, `on`, `off`, `y`, `n`, `.inf`, `.nan` in any
  case), and any text that starts like a number (an optional sign, then a digit, or a dot
  and a digit). This over-quotes `0x10`, `1_000` and `2026-01-02` on purpose, and the check
  uses character tests, so every runtime writes the same text.
- A Dataset needs at least one identifier, as in the model.

#### ProcessCore interoperability

ProcessCore's lenient parser (`Dataset.fromYamlString false`) reads both output modes.
It keeps `identifier` (the first item), the scalar Dataset fields, processes with their
endpoints, recipes, parameter values, data files, citations, agents (inline mode),
`additionalProperty` and nested `hasPart` Datasets. It treats `id`, `conformsTo`,
`identifiers` and extension keys as overflow properties of the Dataset, and it drops
the IDs of Processes, Samples and Annotations. It does not resolve `@id` references to
Agents, so the indexed mode loses the Dataset agents and the authors of citations.
It reads `defaultValue` of a FormalParameter as a DefinedTerm, which keeps only the
name of an inline Annotation and drops a reference.

## Extensions and storage

`graph.Entity` manages extensions on core classes and generic typed
`EntityObject` values through the same transaction/history pipeline. Extension
values go through `setProperty`, `addProperty`, and `removeProperty` and use
Layer 1 value classes: text, finite binary64 numbers, Boolean, null, canonical
padded base64 blobs, object references, and nested ordered collections. Missing
properties, explicit null, and empty collections are distinct. Core property
names are reserved and comparisons are case-sensitive. Set replaces or inserts,
and add rejects duplicate properties. Removing a missing property changes nothing
and writes no history. Property enumeration has no guaranteed order.

Object references resolve by ID and support cycles, including
through collections. Self-containing collection containers are rejected; express
cycles through typed objects. Reachability follows extension references.

Schema version **5** creates the tables and indexes of the normative core SQL
profile ([001_core.sql](../../schemas/sql/001_core.sql)) with an added `arc_id`
column, plus the profile's views. Primary keys and foreign keys are composite
over `arc_id`, and the registry foreign key cascades on delete. Association
targets have extra `(arc_id, target)` indexes. `process_edges` and
`core_validation_errors` are scoped by `arc_id`. The writer checks every write
with `Model.validate` and does not run `core_validation_errors`. `Model.validate`
is less strict than that view, so a stored ARC can report rows there. A Dataset
with processes whose `conformsTo` lacks process-provenance passes the write check
and appears in the view. The view stays available for queries.

Management tables add `repository`, `arc` (root, folder, baseline, saved graph,
cursor, revision), the entity registry `entity` (id, type, ordinal),
`entity_additional_type` for non-core typed objects, `entity_extension`
(blob extensions keep canonical base64 text in `value_text`, and value lookup
indexes cover `(arc_id, property, value_text)` and `(arc_id, property, value_number)`),
`history`, `arc_archive`, and `archived_history`. Each `history` row holds one
operation as encoded change records. A record holds the entity ID and ordinal,
plus the before and after rows of one changed entity. History therefore grows with
the size of the changed entities and is independent of the rest of the graph. One
Dataset with a long process list carries that list in each of its edits. Ordinals
keep list positions through undo and redo.

A write inserts new rows, removes changed association, endpoint, and extension
rows, updates scalars, clears outgoing references of removed rows, deletes
descriptors, then other core rows, then registry rows, inserts same-ID kind
replacements, and adds rows. A title edit changes four rows: the dataset update,
the history insert, and the separate `arc` revision and cursor updates. Association list edits keep the common
prefix, so appending one element writes one row. The store has no journal, typed
payload tables, mirror tables, or full state snapshots in history. The registry
and core tables, the history change records and the archived state do not store a
supplied ID, because managed entities always carry their managed ID. Archived
rows written by earlier builds may contain a `suppliedId` key, which the row codec
ignores.

Opening compares every expected object in `sqlite_master` with its exact SQL
text. Opening rejects a version 4 database with a hint to re-import the ARC from
`arc.yml`, and there is no migration. There is no extra entity surrogate-key
system or public cross-entry search API. PolyglotSQLite owns all providers.

## Layout, packages, and verification

The [project](../../src/ARCManagement/ARCManagement.fsproj) builds one
`ARCManagement` assembly. Generic facades, `Operations/`, `Runtime/`, `Internal/`,
`Serialization/`, `Storage/`, and portable `Helper/` code compile before workspace
APIs in `ARCtrl/`. Workspace policy, fixed paths, and workspace-specific
identifier/path helpers stay in that nested folder. UUID generation and general
filesystem adapters remain generic.

| Runtime | Generic APIs | Workspace API |
|---|---|---|
| F#/.NET | `ARCManagement.Session`, `ManagedGraph`, `DatasetYaml` | `ARCtrl.ARC` |
| JavaScript/TypeScript | `arc-management` | `arc-management/arctrl` |
| Python | `arc_management` | `arc_management.arctrl` |

Import domain classes from `arc-base-model` / `arc_base_model`. Entrypoints share
one implementation and canonical model/SQLite classes; callers need no generated
implementation imports or runtime helpers. Python methods use emitted snake_case
names such as `create_in_memory`, `create_arc`, `import_yaml`, and `to_yaml`.
Staging keeps curated exports and precise declarations. Management consumers use
the existing model Python numeric compatibility pass; PolyglotSQLite retains its
own representation boundaries. Keep the pinned toolchain.

```powershell
.\build.cmd TestARCManagement
.\build.cmd TestBaseModel
.\build.cmd RunTests
```

`TestARCManagement` aggregates `TestARCManagementDotNet`, `TestARCManagementJS`,
`TestARCManagementPy`, and `TestARCManagementNative`, including handwritten
JS/TS/Python consumers, declaration checks, and packed artifacts. The
[shared tests](../../tests/ARCManagement.Tests/Main.fs) are portable; only the
[demo walkthrough](../../tests/ARCManagement.Tests/Walkthrough.fs) is .NET-only.
See the [plan](../../plans/arc-management.md) for actual results. Compilation and
skipped execution are not passing runtime evidence.
