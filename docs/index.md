---
title: ARC Base Model
category: Documentation
categoryindex: 1
index: 1
---

# ARC Base Model

Specification and implementation workspace for the ARC Base Model. The model is
organized into three base profiles—Process Provenance, Semantic Designation,
and Administrative—and domain-specific decoration profiles that extend them.
The repository also contains derived schema representations, examples, and F#
libraries for working across .NET, JavaScript, and Python runtimes.

## Start Here

- [Project overview](project/overview.md)
- [Base profiles](spec/index.md)
- [Decoration profiles](spec/decorations/overview.md)
- [Specification guide](project/specification.md)
- [Implementation guide](project/implementation.md)
- [ProcessCore implementation guide](core-implementation/overview.md)
- [Examples and schemas](project/examples-and-schemas.md)
- [Reference material](project/references.md)
- [Prior art notes](project/prior-art.md)

## Profile Overview

```mermaid
flowchart TB
    arc[ARC Base Model]
    base[Base Profiles]
    decorations[Decoration Profiles]

    arc --> base
    arc --> decorations

    base --> provenance[Process Provenance]
    base --> semantics[Semantic Designation]
    base --> administrative[Administrative]

    decorations --> isa[ISA]
    decorations --> workflow[Workflow Run]
    decorations --> datamap[Datamap]

    decorations -. extend .-> base
```

