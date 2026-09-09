---
title: Workflow Invocation
category: Workflow Run Decoration
categoryindex: 6
index: 5
---

# Workflow Invocation

Workflow Run specialization of [Process](../../process_provenance/Process.md). Represents the execution of a Workflow Protocol, combining computational and laboratory workflow execution.

**`additionalType`**: `Workflow Invocation`

**Multi-type**: CreateAction + Process

Reference: [ARC WR RO-Crate Profile — Workflow Invocation](../../../../references/arc_wr_ro_crate.md)

## Properties

Inherits all properties from [Process](../../process_provenance/Process.md). WR-specific refinements:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `executesRecipe` | [WorkflowProtocol](WorkflowProtocol.md) | MUST | Executed workflow (MUST equal `instrument`) |
| `inputs` | MediaObject, Dataset, Annotation | MUST | Input files consumed |
| `outputs` | MediaObject, Dataset, Annotation | MUST | Output files created/modified |
| `parameterValue` | [Annotation](Annotations.md) | COULD | Workflow parameter values |
| `description` | Text | COULD | Execution details (CLI args, settings) |

