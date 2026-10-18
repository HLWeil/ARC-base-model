namespace ARCBaseModel

open Fable.Core

/// A biological, chemical, or digital sample without inferred identity.
[<AttachMembers>]
type Sample(name: string, ?additionalProperties: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Sample", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _additionalProperties = Construction.optionalCollection additionalProperties

    do base.ReserveProperties(["name"; "additionalProperties"])
    do base.SealInternal()
    /// Human-readable sample name.
    member _.Name with get() = _name
    /// Extensible metadata. Replacement copies the container, retaining annotation references.
    member _.AdditionalProperties with get() = _additionalProperties

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "additionalProperties" -> _additionalProperties <- Construction.collection (unbox<seq<Annotation>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        Construction.text "Name" _name |> ignore
        this.ValidateOwned("additionalProperties",box _additionalProperties,fun () -> for item in _additionalProperties do if not (box item :? Annotation) then invalidArg "AdditionalProperties" "Unexpected entity kind.")
        ()
    static member withName(value:Sample,replacement:string):Sample = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withAdditionalProperties(value:Sample,replacement:seq<Annotation>):Sample = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalProperties",box replacement))
    static member withId(value:Sample,replacement:string):Sample = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Sample):Sample = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Sample,replacement:seq<string>):Sample = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Sample,key:string,replacement:Entity):Sample = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Sample,key:string):Sample = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))

/// A file or file fragment; every nested Data object supplies its own path.
[<AttachMembers>]
type Data(path: string, ?selector: string, ?selectorFormat: string, ?encodingFormat: string,
          ?hasParts: seq<Data>, ?additionalProperties: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Data", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _path = Construction.required "path" path
    let mutable _selector = selector
    let mutable _selectorFormat = selectorFormat
    let mutable _encodingFormat = encodingFormat
    let mutable _hasParts = Construction.optionalCollection hasParts
    let mutable _additionalProperties = Construction.optionalCollection additionalProperties

    do base.ReserveProperties(["path"; "selector"; "selectorFormat"; "encodingFormat"; "hasParts"; "additionalProperties"])
    do base.SealInternal()
    /// Path to the target file, unchanged by construction.
    member _.Path with get() = _path
    /// Optional fragment selector.
    member _.Selector with get() = _selector
    /// URL describing the selector syntax.
    member _.SelectorFormat with get() = _selectorFormat
    /// MIME type of the data object or fragment.
    member _.EncodingFormat with get() = _encodingFormat
    /// Nested fragments, preserving supplied references and order.
    member _.HasParts with get() = _hasParts
    /// Extensible file, fragment, or content metadata.
    member _.AdditionalProperties with get() = _additionalProperties

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "path" -> _path <- unbox<string> value
        | "selector" -> _selector <- unbox<string option> value
        | "selectorFormat" -> _selectorFormat <- unbox<string option> value
        | "encodingFormat" -> _encodingFormat <- unbox<string option> value
        | "hasParts" -> _hasParts <- Construction.collection (unbox<seq<Data>> value)
        | "additionalProperties" -> _additionalProperties <- Construction.collection (unbox<seq<Annotation>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        Construction.text "Path" _path |> ignore
        _selector |> Option.iter (Construction.text "Selector" >> ignore)
        _selectorFormat |> Option.iter (Construction.text "SelectorFormat" >> ignore)
        _encodingFormat |> Option.iter (Construction.text "EncodingFormat" >> ignore)
        this.ValidateOwned("hasParts",box _hasParts,fun () -> for item in _hasParts do if not (box item :? Data) then invalidArg "HasParts" "Unexpected entity kind.")
        this.ValidateOwned("additionalProperties",box _additionalProperties,fun () -> for item in _additionalProperties do if not (box item :? Annotation) then invalidArg "AdditionalProperties" "Unexpected entity kind.")
        ()
    static member withPath(value:Data,replacement:string):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("path",box replacement))
    static member withSelector(value:Data,replacement:string):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("selector",box(Some replacement)))
    static member clearSelector(value:Data):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("selector",box(None:string option)))
    static member withSelectorFormat(value:Data,replacement:string):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("selectorFormat",box(Some replacement)))
    static member clearSelectorFormat(value:Data):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("selectorFormat",box(None:string option)))
    static member withEncodingFormat(value:Data,replacement:string):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("encodingFormat",box(Some replacement)))
    static member clearEncodingFormat(value:Data):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("encodingFormat",box(None:string option)))
    static member withHasParts(value:Data,replacement:seq<Data>):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("hasParts",box replacement))
    static member withAdditionalProperties(value:Data,replacement:seq<Annotation>):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalProperties",box replacement))
    static member withId(value:Data,replacement:string):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Data):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Data,replacement:seq<string>):Data = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Data,key:string,replacement:Entity):Data = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Data,key:string):Data = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))

/// A Sample or Data instance used by a process endpoint or descriptor.
[<Erase; RequireQualifiedAccess>]
type EntityReference =
    | Sample of Sample
    | Data of Data
