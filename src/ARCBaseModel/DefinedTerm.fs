namespace ARCBaseModel

open Fable.Core

type internal DefinedTermSetState = { Name: string; Identifier: string option }

/// A named ontology, controlled vocabulary, or other set of defined terms.
[<AttachMembers>]
type DefinedTermSet(name: string, ?identifier: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("DefinedTermSet", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: DefinedTermSetState =
        { Name = Construction.required "name" name
          Identifier = identifier }

    do base.ReserveProperties(["name"; "identifier"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable name of the term set.
    member _.Name = state.Name
    /// Optional text or URL identifying the term set.
    member _.Identifier = state.Identifier
    static member withName(value: DefinedTermSet, replacement: string) : DefinedTermSet =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withIdentifier(value: DefinedTermSet, replacement: string option) : DefinedTermSet =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Identifier = replacement })

/// A term set supplied as a URL or an actual term-set object.
[<Erase; RequireQualifiedAccess>]
type DefinedTermSetReference =
    | Url of string
    | TermSet of DefinedTermSet

type internal DefinedTermState =
    { Name: string
      Identifier: string option
      TAN: string option
      InDefinedTermSet: DefinedTermSetReference option }

/// An ontology term with optional accession and containing term set.
[<AttachMembers>]
type DefinedTerm(name: string, ?identifier: string, ?tan: string, ?inDefinedTermSet: DefinedTermSetReference,
                 ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("DefinedTerm", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: DefinedTermState =
        { Name = Construction.required "name" name
          Identifier = identifier
          TAN = tan
          InDefinedTermSet = inDefinedTermSet }

    do base.ReserveProperties(["name"; "identifier"; "TAN"; "inDefinedTermSet"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable term name.
    member _.Name = state.Name
    /// Optional text or URL identifying the term.
    member _.Identifier = state.Identifier
    /// Term accession number within the ontology.
    member _.TAN = state.TAN
    /// Ontology supplied as a URL or a named term set.
    member _.InDefinedTermSet = state.InDefinedTermSet
    static member withName(value: DefinedTerm, replacement: string) : DefinedTerm =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withIdentifier(value: DefinedTerm, replacement: string option) : DefinedTerm =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Identifier = replacement })
    static member withTAN(value: DefinedTerm, replacement: string option) : DefinedTerm =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with TAN = replacement })
    static member withInDefinedTermSet(value: DefinedTerm, replacement: DefinedTermSetReference option) : DefinedTerm =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with InDefinedTermSet = replacement })
