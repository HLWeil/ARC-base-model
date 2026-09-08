---
title: Examples And Schemas
category: Project
categoryindex: 2
index: 4
---

# Examples And Schemas

Schemas are derived representations of the markdown specification. The markdown files in [docs/spec](../spec/index.md) remain the normative source.

## Schema Artifacts

| Location | Status |
|----------|--------|
| `schemas/sql/` | SQLite-oriented SQL profile with executable DDL and seed data |
| `schemas/yml/` | JSON Schema draft 2020-12 expressed in YAML for the unified ProcessCore model |
| `schemas/document-db/` | Placeholder for a future document database representation |

The YAML schemas describe the current unified vocabulary. In particular, `Process.yml` expects `inputs`, `outputs`, and `executesRecipe`; dataset metadata uses repo-native fields such as `agents`, `citations`, `dataFiles`, and `dataContexts`.

## Examples

| Location | Status |
|----------|--------|
| `examples/process_core/minimal.yml` | Schema-shaped Process Provenance example intended to match the current YAML schema vocabulary |
| `examples/datamap/proteomics_data.yml` | Data file, data fragment, and data context example using repo-native YAML names |
| `examples/datamap/metadata_powered_analysis.yml` | Combined process and Datamap example for metadata-powered fragment selection |
| `examples/administrative/dataset_administration.yml` | Agent, organization, citation, license, and date example |
| `examples/isa/` | Legacy/profile-shaped ISA and Datamap examples kept as domain examples |
| `examples/workflow-run/` | Placeholder for future Workflow Run examples |

The current ISA examples are useful profile examples, but they are not currently guaranteed to validate against the strict core YAML schemas. They use some RO-Crate/Bioschemas profile terms such as `object`, `result`, `annotations`, and `additionalProperties`.

## Reconciliation Rule

When adding new examples, prefer one of these explicit categories:

- Profile schema examples that use the current repo-native vocabulary and should be validatable.
- Profile-shaped examples that preserve upstream/profile terminology and are labeled as such.

This avoids mixing schema-valid core examples with legacy/profile examples that intentionally use another vocabulary.

## Core Example Shape

```yaml
type: Dataset
identifier: demo
processes:
  - type: Process
    name: Extraction
    inputs:
      - type: Sample
        additionalType: Source
        name: Leaf tissue
    outputs:
      - type: Data
        path: raw/extract.tsv
    executesRecipe:
      type: Recipe
      name: Extraction
```


