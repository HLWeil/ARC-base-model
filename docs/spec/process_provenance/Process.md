---
title: Process
category: Process Provenance Profile
categoryindex: 4
index: 3
---

# Process

Core transformation in the process graph. A Process connects an optional input to an optional output and can reference the Recipe that was executed.

**Bioschemas type**: [`LabProcess`](https://bioschemas.org/types/LabProcess/0.1-DRAFT)

Decorations specialize Process via `additionalTypes`:
- ISA: Process
- Workflow Run: Workflow Invocation (CreateAction + Process)

## Properties

Recommended property mappings are documented in the [schema mapping guide](../../project/schema-mapping.md#process-provenance).

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Process` |
| `additionalTypes` | Text | `1..*` | MUST | MUST include `process-provenance` as its [base-profile discriminator](../index.md#profile-discriminators). Additional classifications and decoration types MAY also be included. |
| `name` | Text | `1` | MUST | Human-readable name of the process |
| `input` | [Sample](../shared/Sample.md), [Data](../shared/Data.md) | `0..1` | SHOULD | Sample or data object used as the input of this process |
| `output` | [Sample](../shared/Sample.md), [Data](../shared/Data.md) | `0..1` | SHOULD | Sample or data object produced as the output of this process |
| `executesRecipe` | [Recipe](Recipe.md) | `0..1` | SHOULD | Recipe executed by this process |
| `parameterValues` | [Annotation](../shared/Annotation.md) | `0..*` | SHOULD | Parameter annotations describing values used in this process |

## Relationships

```mermaid
flowchart TD

    additionalTypes@{ shape: stadium, label: "string" }

    na@{ shape: stadium, label: "string" }
    i["Sample or Data (input)"]
    o["Sample or Data (output)"]

    Dataset --processes--> Process
    Process --input--> i
    Process --additionalTypes--> additionalTypes
    Process --"output"--> o
    Process --executesRecipe--> Recipe
    Process --parameterValues--> Annotation
    Process --name--> na
```

## Inputs and Outputs

Each Process represents one directed graph edge with at most one input and at most one output. Either endpoint MAY be absent. Fan-in, fan-out, and parallel lanes are represented by multiple Process instances.
