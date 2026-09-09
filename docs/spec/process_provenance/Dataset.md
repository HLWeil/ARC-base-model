---
title: Dataset
category: Process Provenance Profile
categoryindex: 4
index: 2
---

# Dataset

Container and context for data and processes. 

**Schema.org type**: `schema.org/Dataset`

Decorations specialize Dataset via `additionalType`:
- ISA: Investigation, Study, Assay
- Workflow Run: ARC Workflow, ARC Run
- Datamap

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Text | MUST | Unique identifier for the dataset |
| `type` | Text | MUST | `Dataset` |
| `additionalType` | Text | COULD | Discriminator for decoration type |
| `identifier` | Text | MUST | Identifying descriptor |
| `title` | Text | SHOULD | Human-readable dataset title |
| `description` | Text | SHOULD | Short description or abstract |
| `processes` | [Process](Process.md) | SHOULD | Processes contained in this dataset |
| `hasPart` | [Dataset](Dataset.md) | SHOULD | Sub-datasets |
| `additionalProperty` | [Annotation](Annotation.md) | COULD | Extensible metadata |

## Relationships

```mermaid
flowchart TD

    id@{ shape: stadium, label: "string" }
    na@{ shape: stadium, label: "string" }
    de@{ shape: stadium, label: "string" }

    d[Dataset]
    Dataset --processes--> Process
    d --hasPart--> Dataset
    Dataset --additionalProperty--> Annotation
    Dataset --identifier--> id
    Dataset --title--> na
    Dataset --description--> de


```


