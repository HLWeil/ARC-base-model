---
title: Base Profiles
category: Specification
categoryindex: 3
index: 1
---

# Base Profiles

The ARC Data Model specification defines three base profiles that together form the general ARC RDM model: Process Provenance, Semantic Designation, and Administrative. The implementation uses one unified object model; the base profiles describe coherent subsets of the same model surface.

Entity specifications shared unchanged between base profiles live in `shared/`: [Data](shared/Data.md), [Sample](shared/Sample.md), [Annotation](shared/Annotation.md), [DefinedTerm](shared/DefinedTerm.md), and [DefinedTermSet](shared/DefinedTermSet.md). Entities whose fields differ between profiles retain their full tables in each profile folder.

## Reading Order

1. [Process Provenance](process_provenance/overview.md)
2. [Semantic Designation](semantic_designation/overview.md)
3. [Administrative](administrative/overview.md)
4. [ARC Workspace Project File](project_file.md)
5. [Querying](../project/querying.md)

## Profile Discriminators

Every profile-specific entity MUST declare its base profile in `additionalTypes` using the corresponding case-sensitive discriminator:

| Base profile | Discriminator |
|--------------|---------------|
| [Administrative](administrative/overview.md) | `administrative` |
| [Semantic Designation](semantic_designation/overview.md) | `semantic-designation` |
| [Process Provenance](process_provenance/overview.md) | `process-provenance` |

A profile-specific entity MUST include its profile's discriminator and MUST NOT include either of the other two base-profile discriminators. Other classifications or specializations MAY also be present in `additionalTypes`. The `type` field identifies the entity type; the base-profile discriminator identifies the applicable profile-specific specification.

Shared entity definitions are profile-neutral and do not require a base-profile discriminator. Referencing a shared entity from a profile does not assign that profile's discriminator to it. Profile discriminators are declared by each profile-specific entity and are not inherited through references.

### Dataset Nesting

A Dataset's `hasParts` collection MAY contain Datasets from any base profile, including different profiles within the same collection. Each child MUST declare its own discriminator and follow the corresponding [Administrative Dataset](administrative/Dataset.md), [Process Provenance Dataset](process_provenance/Dataset.md), or [Semantic Designation Dataset](semantic_designation/Dataset.md) specification. A child's profile is independent of its parent's and siblings' profiles. These nesting options apply recursively to every child's `hasParts`.

## Principles

- Process-centric: experiments and workflows are modeled as processes connecting inputs to outputs.
- Unified: Process Provenance, Semantic Designation, and Administrative properties are available on shared model types rather than split into separate runtime objects.
- Extensible: `Annotation` and `additionalTypes` carry domain-specific information while typed properties cover the common profile surface.
- Representation-aware but model-first: SQL and YAML schemas derive from the markdown spec.

## Main Areas

| Area | Description |
|------|-------------|
| [Process Provenance](process_provenance/overview.md) | Provenance model: Dataset, Process, Recipe, Sample, Data, Annotation, FormalParameter, DefinedTerm, and DefinedTermSet |
| [Semantic Designation](semantic_designation/overview.md) | Semantic descriptions that connect datasets, samples, and data with bundled annotations |
| [Administrative](administrative/overview.md) | Dataset agents, affiliations, citations, licenses, dates, and administrative metadata |
| [ARC Workspace Project File](project_file.md) | Bidirectional rules for partitioning ARC metadata across local resources |
| [Querying](../project/querying.md) | Query use cases and graph traversal notes |
