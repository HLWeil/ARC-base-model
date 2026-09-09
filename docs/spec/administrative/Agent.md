---
title: Agent
category: Administrative Profile
categoryindex: 6
index: 3
---

# Agent

Individual contributor, agent, author, or contact associated with a dataset or citation.

**Schema.org type**: `schema.org/Agent`

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Text | COULD | Unique identifier |
| `type` | Text | MUST | `Agent` |
| `givenName` | Text | MUST | Given name |
| `familyName` | Text | SHOULD | Family name |
| `email` | Text | SHOULD | Email address |
| `affiliation` | [Organization](Organization.md) | SHOULD | Affiliated organization |
| `identifier` | Text | SHOULD | ORCID or other identifier |
| `additionalProperty` | [Annotation](../process_provenance/Annotation.md) | COULD | Extensible agent metadata not covered by the base properties |
| `jobTitle` | [DefinedTerm](../process_provenance/DefinedTerm.md) | COULD | Job title |

## Relationships

```mermaid
flowchart TD

    gn@{ shape: stadium, label: "string" }
    fn@{ shape: stadium, label: "string" }
    e@{ shape: stadium, label: "E-MAIL" }
    i@{ shape: stadium, label: "ORCID" }

    Dataset --agents--> Agent
    Agent --affiliation--> Organization
    Agent --jobTitle--> DefinedTerm
    Agent --givenName--> gn
    Agent --familyName--> fn
    Agent --email--> e
    Agent --identifier--> i
    Agent --additionalProperty--> Annotation
```


