---
title: Workflow Protocol
category: Workflow Run Decoration
categoryindex: 6
index: 4
---

# Workflow Protocol

Workflow Run specialization of [Recipe](../../process_provenance/Recipe.md). Describes the prospective metadata of a computational workflow, combining computational and laboratory workflow descriptions.

**`additionalType`**: `Workflow Protocol`

**Multi-type**: SoftwareSourceCode + ComputationalWorkflow + Recipe

Reference: [ARC WR RO-Crate Profile — Workflow Protocol](../../../../references/arc_wr_ro_crate.md)

## Additional Properties (beyond Recipe)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `inputParameters` | [FormalParameter](FormalParameter.md) | SHOULD | Workflow inputs |
| `outputParameters` | [FormalParameter](FormalParameter.md) | SHOULD | Workflow outputs |
| `programmingLanguage` | ComputerLanguage, Text | SHOULD | Runtime environment |
| `creator` | Agent, Organization | SHOULD | Creator/author |
| `license` | CreativeWork, URL | SHOULD | License |
| `sdPublisher` | Agent, Organization | SHOULD | Host site |
| `hasPart` | CreativeWork | COULD | Tools/scripts used in workflow |
| `components` | SoftwareApplication, DefinedTerm | COULD | Software used |


