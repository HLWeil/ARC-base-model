---
title: Organization
category: Administrative Profile
categoryindex: 6
index: 4
---

# Organization

Entity representing an organization involved in creating, curating, hosting, or publishing a dataset.

**Schema.org type**: [`schema.org/Organization`](https://schema.org/Organization)

## Properties

Recommended property mappings are documented in the [schema mapping guide](../../project/schema-mapping.md#administrative).

| Property | Type | Cardinality | Required | Description |
|----------|------|-------------|----------|-------------|
| `id` | Text | `0..1` | MAY | Optional identifier within an application-defined scope. Implementations that require an internal identifier SHOULD use this field. The domain model does not automatically assign identifiers. |
| `type` | Text | `1` | MUST | `Organization` |
| `additionalTypes` | Text | `1..*` | MUST | MUST include `administrative` as its [base-profile discriminator](../index.md#profile-discriminators). Additional classifications and decoration types MAY also be included. |
| `name` | Text | `1` | MUST | Human-readable name of the organization |
| `url` | URL | `0..1` | MAY | Organization website or identifier URL |

## Relationships

```mermaid
flowchart TD

    additionalTypes@{ shape: stadium, label: "string" }

    n@{ shape: stadium, label: "string" }
    u@{ shape: stadium, label: "URL" }

    Agent --affiliations--> Organization
    Organization --name--> n
    Organization --additionalTypes--> additionalTypes
    Organization --url--> u
```
