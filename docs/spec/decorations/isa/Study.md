---
title: Study
category: ISA Decoration
categoryindex: 5
index: 3
---

# Study

ISA specialization of [Dataset](../../process_provenance/Dataset.md). Represents a unit of research with associated experimental processes at the study level.

**`additionalType`**: `Study`

Reference: [ISA RO-Crate Profile — Study](../../../../references/isa_ro_crate.md)

## Additional Properties (beyond Dataset)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `additionalType` | string | MUST | `Study` |
| `assays` | [Assay](Assay.md) | COULD | Contained assays or data files |
| `contacts` | [Agent](../../administrative/Agent.md) | COULD | Study contacts and contributors |
| `citation` | ScholarlyArticle | COULD | Associated publications |

## Relationships

```mermaid
flowchart TD

    at@{ shape: stadium, label: "\"Study\"" }

    Study --contacts--> Agent
    Study --citation--> ScholarlyArticle
    Investigation --hasPart--> Study
    Study -.assays.-> Assay
    Study --additionalType--> at
    Study --processes--> Process
```

