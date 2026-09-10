---
title: Recipe
category: Process Provenance Profile
categoryindex: 4
index: 4
---

# Recipe

Description of a planned procedure. Recipes define what a Process executes, including intended use, equipment, reagents, and software.

**Bioschemas type**: [`LabProtocol`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT)

Decorations specialize Recipe via `additionalTypes`:
- ISA: Recipe
- Workflow Run: Workflow Protocol (SoftwareSourceCode + ComputationalWorkflow + Recipe)

## Properties

Recommended property mappings are documented in the [schema mapping guide](../../project/schema-mapping.md#process-provenance).

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Recipe` |
| `additionalTypes` | Text | `0..*` | MAY | Additional classifications or specializations of the recipe. Discriminator used for decoration types. |
| `name` | Text | `0..1` | SHOULD | Human-readable title of the recipe |
| `parameters` | [FormalParameter](FormalParameter.md) | `0..*` | MAY | Prospective parameter slots for values supplied when executing the recipe |
| `description` | Text | `0..1` | SHOULD | Short description or abstract of the planned procedure |
| `intendedUse` | [DefinedTerm](DefinedTerm.md), Text | `0..1` | SHOULD | Recipe classification, expressed as a controlled vocabulary term or plain text |
| `additionalProperties` | [Annotation](Annotation.md) | `0..*` | MAY | Extensible recipe metadata not covered by the base properties |
| `components` | [Annotation](Annotation.md) | `0..*` | MAY | Annotations describing equipment, software, reagents, materials, or other components used in the recipe |
| `version` | Text | `0..1` | MAY | Version identifier of the recipe |
| `url` | URL | `0..1` | MAY | URL of an external resource describing the recipe |

## Relationships

```mermaid
flowchart TD

    na@{ shape: stadium, label: "string" }
    de@{ shape: stadium, label: "string" }
    ve@{ shape: stadium, label: "string" }
    ur@{ shape: stadium, label: "URL" }
    av[Annotation]
    co[Annotation]
    iu["DefinedTerm or Text"]

    Process --executesRecipe--> Recipe
    Recipe --intendedUse--> iu
    Recipe --additionalProperties--> av
    Recipe --components--> co
    Recipe --parameters--> FormalParameter
    Recipe --name--> na
    Recipe --description--> de
    Recipe --version--> ve
    Recipe --url--> ur

```

