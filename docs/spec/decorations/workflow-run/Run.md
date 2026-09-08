---
title: Run
category: Workflow Run Decoration
categoryindex: 6
index: 3
---

# Run

Workflow Run specialization of [Dataset](../../process_provenance/Dataset.md). Container describing a run folder in an ARC, documenting the execution of workflows.

**`additionalType`**: `Run`

Reference: [ARC WR RO-Crate Profile — ARC Run](../../../../references/arc_wr_ro_crate.md)

## Additional Properties (beyond Dataset)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `additionalType` | string | MUST | `Run` |
| `processes` | [WorkflowInvocation](WorkflowInvocation.md) | SHOULD | Workflow invocations|
| `performers` | [Agent](../../administrative/Agent.md) | COULD | Run performers and contributors |




