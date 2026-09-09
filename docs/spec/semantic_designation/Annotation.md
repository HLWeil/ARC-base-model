---
title: Annotation
category: Semantic Designation Profile
categoryindex: 5
index: 4
---

# Annotation

Extensible key-value-unit triple that expresses a semantic assertion. An
Annotation can be bundled in a Descriptor or attached directly to a
[Sample](../process_provenance/Sample.md) or
[Data](../process_provenance/Data.md) entity.

**Schema.org type**: `schema.org/PropertyValue`

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Text | MUST | Unique identifier for the annotation |
| `type` | Text | MUST | `Annotation` |
| `additionalType` | Text | COULD | Additional semantic type or subtype discriminator |
| `name` | Text | MUST | Key or property name |
| `value` | Text, Number | SHOULD | Asserted value |
| `unit` | Text | COULD | Unit associated with the asserted value |

## Relationships

```mermaid
flowchart TD
    name@{ shape: stadium, label: "string" }
    value@{ shape: stadium, label: "string or number" }
    unit@{ shape: stadium, label: "string" }
    additionalType@{ shape: stadium, label: "string" }

    Descriptor --annotations--> Annotation
    Sample --additionalProperty--> Annotation
    Data --additionalProperty--> Annotation
    Annotation --name--> name
    Annotation --value--> value
    Annotation --unit--> unit
    Annotation --additionalType--> additionalType
```
