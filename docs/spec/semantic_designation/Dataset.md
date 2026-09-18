---
title: Dataset
category: Semantic Designation Profile
categoryindex: 5
index: 2
---

# Dataset

Container for semantic descriptions and nested datasets. A Dataset groups the
descriptors that designate entities represented in an ARC.

This is the same Dataset type described by the [Administrative](../administrative/Dataset.md) and [Process Provenance](../process_provenance/Dataset.md) profiles.

## Properties

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Dataset` |
| `additionalTypes` | Text | `1..*` | MUST | MUST include `semantic-designation` as its [base-profile discriminator](../index.md#profile-discriminators). Additional classifications or specializations MAY also be included. |
| `identifiers` | Text | `1..*` | MUST | Identifiers by which the dataset is known or referenced, such as a DOI, accession number, repository name, or other identifying string. Identifiers may be globally scoped or scoped to a particular system or context. |
| `title` | Text | `0..1` | SHOULD | Human-readable dataset title |
| `description` | Text | `0..1` | SHOULD | Short description or abstract |
| `descriptors` | [Descriptor](Descriptor.md) | `0..*` | SHOULD | Semantic descriptions associated with the dataset |
| `hasParts` | [Dataset](../index.md#dataset-nesting) | `0..*` | SHOULD | Contained datasets from any base profile. Each child declares its own profile, independently of its parent and siblings; the same nesting options apply at every depth. |
| `additionalProperties` | [Annotation](../shared/Annotation.md) | `0..*` | MAY | Extensible metadata |

## Relationships

```mermaid
flowchart TD
    additionalTypes@{ shape: stadium, label: "string" }
    identifiers@{ shape: stadium, label: "string" }
    title@{ shape: stadium, label: "string" }
    description@{ shape: stadium, label: "string" }

    Dataset --descriptors--> Descriptor
    Dataset --hasParts--> NestedDataset["Dataset (part)"]
    Dataset --additionalTypes--> additionalTypes
    Dataset --identifiers--> identifiers
    Dataset --title--> title
    Dataset --description--> description
    Dataset --additionalProperties--> Annotation
```
