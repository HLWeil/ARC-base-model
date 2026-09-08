---
title: DataContext
category: Datamap Profile
categoryindex: 5
index: 3
---

# DataContext

A DataContext carries additional information about the shape, content, and identity of a [Data](../process_provenance/Data.md) object or a selected fragment within one. In this repository it is its own ARC Core type and keeps the Datamap vocabulary instead of reusing [Annotation](../process_provenance/Annotation.md) fields.

Reference: [ARC Datamap RO-Crate Profile](../../../references/arc_datamap_ro_crate.md)

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `type` | Text | MUST | `DataContext` in the current YAML authoring examples |
| `data` | [Data](./Data.md) | MUST | Target data object or selected data fragment |
| `explication` | [DefinedTerm](../process_provenance/DefinedTerm.md) | SHOULD | Ontological annotation of the fragment contents |
| `objectType` | [DefinedTerm](../process_provenance/DefinedTerm.md) | COULD | Expected value shape or entry type of the described fragment, e.g. `String`, `Integer` |
| `unit` | [DefinedTerm](../process_provenance/DefinedTerm.md) | COULD | Unit of measurement of the values stored in the fragment, preferably taken from Unit Ontology |
| `label` | Text | COULD | Short label such as a column header |
| `description` | Text | COULD | Additional free-text details |
| `generatedBy` | Text | COULD | Tool, assay, or method that produced the described data |

## Relationships

```mermaid
flowchart TD

    d1[DefinedTerm]
    d2[DefinedTerm]
    d3[DefinedTerm]
    la@{ shape: stadium, label: "string" }
    de@{ shape: stadium, label: "string" }
    gb@{ shape: stadium, label: "string" }

    Datamap --dataContexts--> DataContext
    DataContext --data--> Data
    DataContext --explication--> d1
    DataContext --"objectType"--> d2
    DataContext --unit--> d3
    DataContext --label--> la
    DataContext --description--> de
    DataContext --generatedBy--> gb

```
