namespace ARCBaseModel

open Fable.Core

/// A named ontology, controlled vocabulary, or other set of defined terms.
[<AttachMembers>]
type DefinedTermSet(name: string, ?identifier: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("DefinedTermSet", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _identifier = identifier

    do base.EntityProperties.Reserve(["name"; "identifier"])
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
    inherit EntityObject("DefinedTerm", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _identifier = identifier
    let mutable _tan = tan
    let mutable _inDefinedTermSet = inDefinedTermSet

    do base.EntityProperties.Reserve(["name"; "identifier"; "TAN"; "inDefinedTermSet"])
    /// Human-readable term name.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Optional text or URL identifying the term.
    member _.Identifier with get() = _identifier and set(value) = _identifier <- value
    /// Term accession number within the ontology.
    member _.TAN with get() = _tan and set(value) = _tan <- value
    /// Ontology supplied as a URL or a named term set.
    member _.InDefinedTermSet with get() = _inDefinedTermSet and set(value) = _inDefinedTermSet <- value
