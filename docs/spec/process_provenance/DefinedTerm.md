---
title: DefinedTerm
category: Process Provenance Profile
categoryindex: 4
index: 9
---

# DefinedTerm

Ontology annotation referencing a term in a controlled vocabulary or ontology.

**Schema.org type**: `schema.org/DefinedTerm`

## Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | Text | MUST | Term URI |
| `type` | Text | MUST | `DefinedTerm` |
| `name` | Text | MUST | Term name |
| `TAN` | Text | SHOULD | Identifier within the ontology |
| `inDefinedTermSet` | URL, DefinedTermSet | SHOULD | Link to the ontology |

## Relationships

```mermaid
flowchart TD
    na@{ shape: stadium, label: "string" }
    ta@{ shape: stadium, label: "string" }
    se@{ shape: stadium, label: "URL/DefinedTermSet" }

    DefinedTerm --name--> na
    DefinedTerm --TAN--> ta
    DefinedTerm --inDefinedTermSet--> se
```

