---
title: Extension Properties
category: Specification
categoryindex: 3
index: 2
---

# Extension Properties

Every base entity MAY contain arbitrarily named extension properties. These are separate from annotation-based `additionalProperties`, whose names, units, and ontology annotations retain their existing meaning.

Every object, including objects nested in extensions, MUST have a single textual `type`. Its `id` is optional; implementations MUST NOT infer or generate IDs in the portable domain model. `additionalTypes` is a separate ordered collection of text classifications. Core entities retain their fixed type discriminators.

Extension keys are unique, case-sensitive text. Property enumeration order is unspecified; ordered values use collections. Core wire-property names for the containing entity, including `type`, `id`, and `additionalTypes`, are reserved and MUST NOT be overwritten by extensions. Other names are preserved verbatim, including empty names. Namespaced keys are recommended for independently developed extensions.

An extension value is text, a binary64 number, a Boolean, explicit null, binary data, a typed object, or an ordered collection of extension values. Collections can mix alternatives and preserve duplicates. Objects can be shared or cyclic; the portable model preserves supplied references without traversal or normalization. Missing properties and explicit null are distinct.

Binary data uses a distinct blob value containing canonical standard padded base64 text. Empty base64 text is valid. Blob text is validated and preserved without re-encoding. Numbers do not distinguish integer literals from fractional literals and do not guarantee exact arbitrary-size integers.