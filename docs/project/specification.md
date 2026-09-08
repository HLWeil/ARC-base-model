---
title: Specification Guide
category: Project
categoryindex: 2
index: 2
---

# Specification Guide

The normative specification lives in [docs/spec](../spec/index.md). The repository now organizes the ARC RDM model as three sibling profiles: ARC Core, datamap, and administrative. The implementation remains one shared model; profile boundaries are documentation and mapping boundaries, not runtime type boundaries.

## Unified Profiles

```mermaid
flowchart LR
    Dataset --processes--> Process
    Dataset --dataFiles--> Data
    Dataset --agents--> Agent
    Dataset --citations--> ScholarlyArticle
    Dataset --dataContexts--> DataContext
    Dataset --hasPart--> Dataset
    Process --inputs--> Sample
    Process --"outputs"--> Data
    Process --executesRecipe--> Recipe
    Process --parameterValue--> Annotation
    Recipe --parameters--> FormalParameter
    Recipe --components--> Annotation
    Annotation --instanceOf--> FormalParameter
    Agent --affiliation--> Organization
    ScholarlyArticle --authors--> Agent
```

Shared model entities:

| Entity | Source |
|--------|--------|
| Dataset | [Dataset](../spec/process_provenance/Dataset.md) |
| Process | [Process](../spec/process_provenance/Process.md) |
| Recipe | [Recipe](../spec/process_provenance/Recipe.md) |
| Sample | [Sample](../spec/process_provenance/Sample.md) |
| Data | [Data](../spec/process_provenance/Data.md) |
| DataContext | [DataContext](../spec/datamap/DataContext.md) |
| Annotation | [Annotation](../spec/process_provenance/Annotation.md) |
| FormalParameter | [FormalParameter](../spec/process_provenance/FormalParameter.md) |
| DefinedTerm | [DefinedTerm](../spec/process_provenance/DefinedTerm.md) |
| Agent | [Agent](../spec/administrative/Agent.md) |
| Organization | [Organization](../spec/administrative/Organization.md) |
| ScholarlyArticle | [ScholarlyArticle](../spec/administrative/ScholarlyArticle.md) |

Profile entry points:

| Profile | Purpose | Source |
|---------|---------|--------|
| ARC Core | Provenance through datasets, processes, protocols, samples, data, and annotations | [ARC Core](../spec/process_provenance/overview.md) |
| Datamap | Data files, data fragments, fragment descriptors, and dataset data contexts | [Datamap](../spec/datamap/overview.md) |
| Administrative | Dataset metadata, agents, affiliations, citations, licenses, and dates | [Administrative](../spec/administrative/overview.md) |

## Decorations

Decorations add domain-specific meaning through `additionalType`, specialized properties, and decoration-specific entities.

| Decoration | Purpose | Source |
|------------|---------|--------|
| ISA | Investigation, Study, Assay, Source, Sample, and ISA property value roles | [ISA](../spec/decorations/isa/overview.md) |
| Workflow Run | Workflow and Run datasets, workflow protocols, and workflow invocations | [Workflow Run](../spec/decorations/workflow-run/overview.md) |
| Datamap | Promoted to a sibling profile | [Datamap](../spec/datamap/overview.md) |

## Naming Notes

The current core vocabulary uses `Process` and `Recipe`, not the older placeholder names `Process` and `Protocol`.

For process I/O, the current core and YAML schema names are:

- `inputs`
- `outputs`
- `executesRecipe`
- `parameterValue`
- `additionalProperty`

Some legacy/profile-shaped examples and upstream references use RO-Crate or Bioschemas names such as `object` and `result`. See [Examples and schemas](examples-and-schemas.md) for how those files are treated.

## Querying

The query use cases are described in [Querying](querying.md). The implementation exposes `Path` as a returned value object, while traversal and query operations are attached to the model types in `src/ProcessCore/Graph.fs`.


