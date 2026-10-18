namespace ARCBaseModel

open Fable.Core

/// A named ontology, controlled vocabulary, or other set of defined terms.
[<AttachMembers>]
type DefinedTermSet(name: string, ?identifier: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("DefinedTermSet", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _identifier = identifier

    do base.ReserveProperties(["name"; "identifier"])
    do base.SealInternal()
    /// Human-readable name of the term set.
    member _.Name with get() = _name
    /// Optional text or URL identifying the term set.
    member _.Identifier with get() = _identifier

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "identifier" -> _identifier <- unbox<string option> value
        | _ -> base.AssignInternal(key,value)
    override _.ValidateInternal() =
        Construction.text "Name" _name |> ignore
        _identifier |> Option.iter (Construction.text "Identifier" >> ignore)
        ()
    static member withName(value:DefinedTermSet,replacement:string):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withIdentifier(value:DefinedTermSet,replacement:string):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifier",box(Some replacement)))
    static member clearIdentifier(value:DefinedTermSet):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifier",box(None:string option)))
    static member withId(value:DefinedTermSet,replacement:string):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:DefinedTermSet):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:DefinedTermSet,replacement:seq<string>):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:DefinedTermSet,key:string,replacement:Entity):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:DefinedTermSet,key:string):DefinedTermSet = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))

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

    do base.ReserveProperties(["name"; "identifier"; "TAN"; "inDefinedTermSet"])
    do base.SealInternal()
    /// Human-readable term name.
    member _.Name with get() = _name
    /// Optional text or URL identifying the term.
    member _.Identifier with get() = _identifier
    /// Term accession number within the ontology.
    member _.TAN with get() = _tan
    /// Ontology supplied as a URL or a named term set.
    member _.InDefinedTermSet with get() = _inDefinedTermSet

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "identifier" -> _identifier <- unbox<string option> value
        | "tan" -> _tan <- unbox<string option> value
        | "inDefinedTermSet" -> _inDefinedTermSet <- unbox<DefinedTermSetReference option> value
        | _ -> base.AssignInternal(key,value)
    override _.ValidateInternal() =
        Construction.text "Name" _name |> ignore
        _identifier |> Option.iter (Construction.text "Identifier" >> ignore)
        _tan |> Option.iter (Construction.text "TAN" >> ignore)
#if FABLE_COMPILER
        _inDefinedTermSet |> Option.iter (fun item -> if not (Construction.isText (box item) || box item :? DefinedTermSet) then invalidArg "InDefinedTermSet" "Invalid value alternative.")
#else
        _inDefinedTermSet |> Option.iter (Construction.required "InDefinedTermSet" >> ignore)
#endif
        ()
    static member withName(value:DefinedTerm,replacement:string):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withIdentifier(value:DefinedTerm,replacement:string):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifier",box(Some replacement)))
    static member clearIdentifier(value:DefinedTerm):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifier",box(None:string option)))
    static member withTAN(value:DefinedTerm,replacement:string):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("tan",box(Some replacement)))
    static member clearTAN(value:DefinedTerm):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("tan",box(None:string option)))
    static member withInDefinedTermSet(value:DefinedTerm,replacement:DefinedTermSetReference):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("inDefinedTermSet",box(Some replacement)))
    static member clearInDefinedTermSet(value:DefinedTerm):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("inDefinedTermSet",box(None:DefinedTermSetReference option)))
    static member withId(value:DefinedTerm,replacement:string):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:DefinedTerm):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:DefinedTerm,replacement:seq<string>):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:DefinedTerm,key:string,replacement:Entity):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:DefinedTerm,key:string):DefinedTerm = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
