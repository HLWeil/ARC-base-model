---
title: Annotation
category: Process Provenance Profile
categoryindex: 4
index: 7
---

# Annotation

An annotation is a key, value, unit triple, represented by `name`, `value`, and `unit`. Each of the three can optionally be associated with an ontology term through `nameTAN`, `valueTAN`, and `unitTAN`, respectively. Annotations are the primary extension mechanism of Process Provenance. They can be attached through `additionalProperties` for cross-cutting metadata, or through dedicated relationships such as `parameterValues` and `components` when the host type already defines a more specific role.

**Schema.org type**: [`PropertyValue`](https://schema.org/PropertyValue)

Decoration subtypes:

- ISA: ParameterValue, CharacteristicValue, FactorValue, Component
- Workflow Run: Workflow Input, Prefix, Position

## Properties

Recommended property mappings are documented in the [schema mapping guide](../../project/schema-mapping.md#process-provenance).

TAN stands for **term accession number**. The `nameTAN`, `valueTAN`, and `unitTAN` fields contain the URLs of the ontology terms used for the key, value, and unit. The `name` and `unit` fields hold human-readable names.

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Annotation` |
| `additionalTypes` | Text | `0..*` | SHOULD | Additional classifications or specializations of the annotation. Discriminator used for decoration types. |
| `name` | Text | `1` | MUST | Human-readable name of the annotation's key |
| `value` | Text, Number | `0..1` | SHOULD | Textual or numeric value of the annotation |
| `unit` | Text | `0..1` | MAY | Human-readable name of the unit associated with the annotation's value |
| `nameTAN` | URL | `0..1` | SHOULD | URL of the ontology term used for the annotation's key |
| `valueTAN` | URL | `0..1` | MAY | URL of the ontology term used for the annotation's value |
| `unitTAN` | URL | `0..1` | MAY | URL of the ontology term used for the annotation's unit |
| `instanceOf` | [FormalParameter](FormalParameter.md) | `0..1` | MAY | Links a parameter value to its formal parameter definition |

## Relationships

```mermaid
flowchart TD

    na@{ shape: stadium, label: "string" }
    va@{ shape: stadium, label: "string or number" }
    un@{ shape: stadium, label: "string" }
    nt@{ shape: stadium, label: "URL" }
    vt@{ shape: stadium, label: "URL" }
    ut@{ shape: stadium, label: "URL" }

    Dataset --additionalProperties--> Annotation
    Process --parameterValues--> Annotation
    Sample --additionalProperties--> Annotation
    Data --additionalProperties--> Annotation
    Recipe --additionalProperties--> Annotation
    Recipe --components--> Annotation
    FormalParameter --defaultValue--> Annotation

    Annotation --name--> na
    Annotation --value--> va
    Annotation --unit--> un
    Annotation --nameTAN--> nt
    Annotation --valueTAN--> vt
    Annotation --unitTAN--> ut

    Annotation --instanceOf--> FormalParameter
```

