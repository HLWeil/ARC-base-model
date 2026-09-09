---
title: Base Profiles
category: Specification
categoryindex: 3
index: 1
---

# Base Profiles

The ARC Data Model specification defines three base profiles that together form the general ARC RDM model: Process Provenance, Semantic Designation, and Administrative. The implementation uses one unified object model; the base profiles describe coherent subsets of the same model surface. Decoration profiles add domain-specific refinements on top of them.

## Reading Order

1. [Process Provenance](process_provenance/overview.md)
2. [Semantic Designation](semantic_designation/overview.md)
3. [Administrative](administrative/overview.md)
4. [Decorations](decorations/overview.md)
5. [ARC Workspace Project File](project_file.md)
6. [Querying](../project/querying.md)

## Principles

- Process-centric: experiments and workflows are modeled as processes connecting inputs to outputs.
- Unified: Process Provenance, Semantic Designation, and Administrative properties are available on shared model types rather than split into separate runtime objects.
- Extensible: `Annotation` and `additionalType` carry domain-specific information while typed properties cover the common profile surface.
- Representation-aware but model-first: SQL and YAML schemas derive from the markdown spec.

## Main Areas

| Area | Description |
|------|-------------|
| [Process Provenance](process_provenance/overview.md) | Provenance model: Dataset, Process, Recipe, Sample, Data, Annotation, FormalParameter, and DefinedTerm |
| [Semantic Designation](semantic_designation/overview.md) | Semantic descriptions that connect datasets, samples, and data with bundled annotations |
| [Administrative](administrative/overview.md) | Dataset agents, affiliations, citations, licenses, dates, and administrative metadata |
| [Decorations](decorations/overview.md) | ISA, Workflow Run, and Datamap decoration profiles layered onto the base profiles |
| [ARC Workspace Project File](project_file.md) | Bidirectional rules for partitioning ARC metadata across local resources |
| [Querying](../project/querying.md) | Query use cases and graph traversal notes |
