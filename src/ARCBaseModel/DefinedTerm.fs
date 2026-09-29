namespace ARCBaseModel

open Fable.Core

/// A named ontology, controlled vocabulary, or other set of defined terms.
[<AttachMembers>]
type DefinedTermSet(name: string, ?identifier: string, ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = Construction.required "name" name
    let mutable _identifier = identifier
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "DefinedTermSet"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, in supplied order and without deduplication.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable name of the term set.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Optional text or URL identifying the term set.
    member _.Identifier with get() = _identifier and set(value) = _identifier <- value

/// A term set supplied as a URL or an actual term-set object.
[<Erase; RequireQualifiedAccess>]
type DefinedTermSetReference =
    | Url of string
    | TermSet of DefinedTermSet

/// An ontology term with optional accession and containing term set.
[<AttachMembers>]
type DefinedTerm(name: string, ?identifier: string, ?tan: string, ?inDefinedTermSet: DefinedTermSetReference,
                 ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = Construction.required "name" name
    let mutable _identifier = identifier
    let mutable _tan = tan
    let mutable _inDefinedTermSet = inDefinedTermSet
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "DefinedTerm"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable term name.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Optional text or URL identifying the term.
    member _.Identifier with get() = _identifier and set(value) = _identifier <- value
    /// Term accession number within the ontology.
    member _.TAN with get() = _tan and set(value) = _tan <- value
    /// Ontology supplied as a URL or a named term set.
    member _.InDefinedTermSet with get() = _inDefinedTermSet and set(value) = _inDefinedTermSet <- value
