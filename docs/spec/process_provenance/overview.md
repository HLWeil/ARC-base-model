---
title: Process Provenance Profile
category: Process Provenance Profile
categoryindex: 4
index: 1
---

# Process Provenance Profile

The Process Provenance Profile describes the fundamental entities and relationships in the ARC process model. It abstracts experimental and computational workflows as process graphs that connect sample and data inputs to sample and data outputs.

## Core Types

| Type | Description |
|------|-------------|
| [Dataset](Dataset.md) | Container and context for processes, nested datasets, data files, and metadata |
| [Process](Process.md) | Transformation node connecting inputs to outputs |
| [Recipe](Recipe.md) | Planned procedure that a process executes |
| [Sample](Sample.md) | Biological, chemical, or digital sample used as input or output |
| [Data](Data.md) | Data file or selected file fragment |
| [Annotation](Annotation.md) | Extensible key-value-unit triple |
| [FormalParameter](FormalParameter.md) | Prospective parameter slot for recipes |
| [DefinedTerm](DefinedTerm.md) | Ontology annotation or controlled vocabulary term |

## Process Graph

```mermaid
flowchart LR
    Dataset --processes--> Process
    Dataset --dataFiles--> Data
    Dataset --hasPart--> Dataset
    Process --inputs--> Sample
    Process --"outputs"--> Data
    Process --executesRecipe--> Recipe
    Process --parameterValue--> Annotation
    Recipe --parameters--> FormalParameter
    Annotation --instanceOf--> FormalParameter
```

For a relational view of these types, see [schemas/sql/design.md](../../../schemas/sql/design.md).

