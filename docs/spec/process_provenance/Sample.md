---
title: Sample
category: Process Provenance Profile
categoryindex: 4
index: 5
---

# Sample

Input or output biological, chemical, or digital sample in the process graph. Samples can derive from other samples, forming provenance chains.

**Schema.org type**: `bioschemas.org/Sample`

Decorations specialize Sample:
- ISA: Sample, Source

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Text | MUST | Unique sample name |
| `type` | Text | MUST | `Sample` |
| `additionalType` | Text | COULD | Decoration discriminator, e.g. `Sample` or `Source` |
| `name` | Text | MUST | Name identifying the sample |
| `additionalProperty` | [Annotation](Annotation.md) | SHOULD | Characteristics, factors, or other extensible metadata |

## Relationships

```mermaid
flowchart TD

    na@{ shape: stadium, label: "string" }

    Process --inputs"--> Sample
    Process --"outputs"--> Sample
    Sample --additionalProperty--> Annotation
    Sample --name--> na
```

