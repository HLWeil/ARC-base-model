---
title: Descriptor
category: Semantic Designation Profile
categoryindex: 5
index: 3
---

# Descriptor

A semantic description of one [Sample](../process_provenance/Sample.md) or
[Data](../process_provenance/Data.md) entity. A Descriptor collects annotations
that express semantic assertions about that entity.

## Properties

Recommended property mappings are documented in the [schema mapping guide](../../project/schema-mapping.md#semantic-designation).

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Descriptor` |
| `additionalTypes` | Text | `0..*` | SHOULD | Additional semantic types or subtype discriminators |
| `describes` | [Sample](../process_provenance/Sample.md), [Data](../process_provenance/Data.md) | `1` | MUST | Sample or data object described by this descriptor |
| `annotations` | [Annotation](Annotation.md) | `0..*` | SHOULD | Semantic assertions bundled by this descriptor |

## Relationships

```mermaid
flowchart TD
    additionalTypes@{ shape: stadium, label: "string" }
    target["Sample or Data"]

    Dataset --descriptors--> Descriptor
    Descriptor --describes--> target
    Descriptor --annotations--> Annotation
    Descriptor --additionalTypes--> additionalTypes
```
