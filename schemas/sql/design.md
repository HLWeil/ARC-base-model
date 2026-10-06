# Core SQL profile

This SQLite schema represents the [three base profiles](../../docs/spec/index.md)
and all thirteen shared/profile entity types. Normative markdown property tables
govern the representation. Management sessions, history, registration, revision
and filesystem lifecycle are separate concerns and have no tables here.

## Identity and values

**SQL persistence requires explicitly supplied domain IDs for every entity.**
Entity tables use `id TEXT NOT NULL PRIMARY KEY`. IDs remain optional in the
portable domain model; this profile's restriction does not change that model.
IDs are preserved verbatim and unique within each entity table. SQL does not
assign IDs, infer identity from paths or names, or maintain a global registry.
Different entity types may use the same ID. Distinct Data entities may share
the same path and selector.

Required scalars are NOT NULL; optional scalars use NULL. Strings, URLs and dates
are preserved without parsing. Empty text is a value. Entity `type` is constrained
to the normative name. Collections use `(owner_id, position)` keys with
nonnegative positions; order, duplicate values, repeated targets and shared
references are preserved. No exclusive owner, orphan or tree policy is imposed.

## Property mapping checklist

Every entity maps `id` and `type` to scalar columns and `additionalTypes` to an
ordered `<entity>_additional_type` table. Domain `id` is `0..1`/MAY; the SQL
profile requires it. `type` is `1`/MUST. `additionalTypes` is `0..*`.

In the table below, bold scalar fields are `1`/MUST. Other scalar fields are
`0..1`; collections are `0..*` except Dataset `identifiers` and `conformsTo`,
which are `1..*`/MUST. SHOULD/MAY recommendations remain as specified by the
linked normative tables and do not become additional required columns.
Scalar columns use snake_case; entity references add `_id`. Collection tables
use the owner name and singular property name, with `value` for text items or
the referenced entity's `<type>_id` for entity items.

| Entity | Scalar fields in addition to id/type | Ordered collections in addition to additionalTypes |
|---|---|---|
| [Dataset](../../docs/spec/administrative/Dataset.md) | title, description, license, datePublished, dateCreated, dateModified | conformsTo, identifiers, hasParts, dataFiles, agents, citations, additionalProperties, [processes](../../docs/spec/process_provenance/Dataset.md), [descriptors](../../docs/spec/semantic_designation/Dataset.md) |
| [Process](../../docs/spec/process_provenance/Process.md) | **name**, input, output, executesRecipe | parameterValues |
| [Recipe](../../docs/spec/process_provenance/Recipe.md) | name, description, intendedUse, version, url | parameters, components, additionalProperties |
| [Sample](../../docs/spec/shared/Sample.md) | **name** | additionalProperties |
| [Data](../../docs/spec/shared/Data.md) | **path**, selector, selectorFormat, encodingFormat | hasParts, additionalProperties |
| [Annotation](../../docs/spec/shared/Annotation.md) | **name**, value, unit, nameTAN, valueTAN, unitTAN, instanceOf | — |
| [FormalParameter](../../docs/spec/shared/FormalParameter.md) | name, nameTAN, defaultValue | — |
| [DefinedTerm](../../docs/spec/shared/DefinedTerm.md) | **name**, identifier, TAN, inDefinedTermSet | — |
| [DefinedTermSet](../../docs/spec/shared/DefinedTermSet.md) | **name**, identifier | — |
| [Descriptor](../../docs/spec/semantic_designation/Descriptor.md) | **describes** | annotations |
| [Agent](../../docs/spec/administrative/Agent.md) | **name**, givenName, familyName | emails, affiliations, identifiers, additionalProperties, jobTitles |
| [Organization](../../docs/spec/administrative/Organization.md) | **name**, url | — |
| [ScholarlyArticle](../../docs/spec/administrative/ScholarlyArticle.md) | **headline**, creativeWorkStatus | identifiers, authors, additionalProperties |

This produces 13 entity tables and 39 association/endpoint tables. Dataset combines
all three profiles. Each nested Dataset declares its own profiles independently.

## Relationships and alternatives

- Dataset `hasParts` uses `dataset_has_part` and targets Dataset only. `dataFiles`
  uses `dataset_data_file`. Data `hasParts` uses `data_has_part` and targets Data.
- Process `input` and `output` use `process_io(process_id, direction)`. Missing
  rows represent absent endpoints; each existing row selects exactly one Sample
  or Data foreign key. `executesRecipe` uses `executes_recipe_id`.
- Annotation `value` uses mutually exclusive `value_text` and `value_number REAL`.
  Both NULL mean absence. Numeric `42` and textual `"42"` remain distinct; REAL
  preserves binary64 numbers, including zero and fractional values.
- Recipe `intendedUse` uses `intended_use_id` (DefinedTerm) or `intended_use_text`.
  Text matching an entity ID remains text; no lookup changes its alternative.
- DefinedTerm `inDefinedTermSet` uses `in_defined_term_set_url` or
  `in_defined_term_set_id` (DefinedTermSet). The set's fields and shared identity
  remain represented by its own entity table.
- Descriptor `describes` requires exactly one `sample_id` or `data_id`.
- FormalParameter `defaultValue` references Annotation. Annotation `instanceOf`
  references FormalParameter. Initially deferred foreign keys allow this cycle
  to be assembled within an explicit transaction.

Owner foreign keys cascade association-row deletion; other references restrict
entity deletion. Traversal/reference indexes support queries without adding
identity policy. `process_edges` exposes complete singular input/output pairs;
Processes with either endpoint absent have no complete edge in that view.

## Completed-representation validation

DDL enforces required scalars, type discriminators, foreign keys, positions and
alternative cardinalities. Collection rows can be assembled in stages.

After assembly, `core_validation_errors` must return no rows and
`PRAGMA foreign_key_check` must report no violations. The view checks nonempty
Dataset identifiers, at least one case-sensitive base discriminator in `conformsTo`,
and the appropriate declarations for process, descriptor and administrative
properties. Additional classification strings are permitted. Each child is
validated independently; no root containment or ownership policy is imposed.

The existing `src/ARCtrl/Storage/SQLiteStore.fs` provides a small validation helper
over `PolyglotSQLite`. It remains separate from the prototype's session schema.
There is no parallel row API or duplicated SQLite infrastructure. Tests live in
`tests/ManagementPrototype.Tests`, reuse the existing store/provider, and execute
the same SQL cases on .NET, Node and Python.

## Compatibility and artifacts

This is a breaking revision of the old SQL profile: positional multi-endpoint I/O,
protocol-era names, singular collection columns, mixed Dataset/Data containment,
flattened term sets, string-only Annotation values, fragment-derived uniqueness
and DefinedTerm defaults are removed. No implicit migration is supplied.

The existing `ProcessCore.SQL` API is retired and does not implement this new
profile. Core SQL tests live in `tests/ManagementPrototype.Tests`, with portable
driver and generic repository tests in `tests/PolyglotSQLite.Tests`. Normative profiles and YAML schemas are unchanged. Decoration-specific
tables remain outside this revision.

`001_core.sql` is an explicitly destructive recreation script, not a production
migration. `seed_example.sql` demonstrates all three profiles, mixed nesting,
shared references, duplicates, defaults, components and scalar alternatives.
Rebuild the committed example database and run checks with:

```powershell
uv run --no-sync python tests/ManagementPrototype.Tests/CoreSql.py --rebuild
.\build.cmd TestCoreSQL
```

The rebuild checks database integrity, foreign keys, profile validation and local
documentation links. `TestCoreSQL` runs the existing prototype tests plus focused
core schema checks through the pinned `PolyglotSQLite` providers on all runtimes.
