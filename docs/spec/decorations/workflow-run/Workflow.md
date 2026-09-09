---
title: Workflow
category: Workflow Run Decoration
categoryindex: 6
index: 2
---

# Workflow

Workflow Run specialization of [Dataset](../../process_provenance/Dataset.md). Container describing a workflow folder in an ARC with ISA-compliant metadata.

**`additionalType`**: `Workflow`

Reference: [ARC WR RO-Crate Profile — ARC Workflow](../../../../references/arc_wr_ro_crate.md)

## Additional Properties (beyond Dataset)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `additionalType` | string | MUST | `Workflow` |
| `mainEntity` | [WorkflowProtocol](WorkflowProtocol.md) | MUST | The main workflow |
| `contacts` | [Agent](../../administrative/Agent.md) | COULD | Workflow contacts and contributors |


