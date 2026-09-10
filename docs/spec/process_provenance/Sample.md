---
title: Sample
category: Process Provenance Profile
categoryindex: 4
index: 5
---

# Sample

Input or output biological, chemical, or digital sample in the process graph. Samples can derive from other samples through processes, forming provenance chains.

**Recommended Bioschemas type mapping**: [`Sample`](https://bioschemas.org/types/Sample/0.3-DRAFT) for material samples; digital-sample mappings depend on the target profile.

Decorations specialize Sample via `additionalTypes`:
- ISA: Sample, Source

## Properties

Recommended property mappings are documented in the [schema mapping guide](../../project/schema-mapping.md#process-provenance).

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Sample` |
| `additionalTypes` | Text | `0..*` | MAY | Additional classifications or specializations of the sample. Discriminator used for decoration types. |
| `name` | Text | `1` | MUST | Human-readable name of the sample |
| `additionalProperties` | [Annotation](Annotation.md) | `0..*` | SHOULD | Characteristics, factors, or other metadata describing the sample |

## Relationships

```mermaid
flowchart TD

    na@{ shape: stadium, label: "string" }

    Process --input--> Sample
    Process --"output"--> Sample
    Sample --additionalProperties--> Annotation
    Sample --name--> na
```

