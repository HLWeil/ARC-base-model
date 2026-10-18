namespace ARCBaseModel

open Fable.Core

/// Recipe classification represented by text or a controlled vocabulary term.
[<Erase; RequireQualifiedAccess>]
type RecipeIntendedUse =
    | Text of string
    | Term of DefinedTerm

/// Description of a planned procedure and its prospective parameters.
[<AttachMembers>]
type Recipe(?name: string, ?parameters: seq<FormalParameter>, ?description: string,
            ?intendedUse: RecipeIntendedUse, ?additionalProperties: seq<Annotation>, ?components: seq<Annotation>,
            ?version: string, ?url: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Recipe", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = name
    let mutable _parameters = Construction.optionalCollection parameters
    let mutable _description = description
    let mutable _intendedUse = intendedUse
    let mutable _additionalProperties = Construction.optionalCollection additionalProperties
    let mutable _components = Construction.optionalCollection components
    let mutable _version = version
    let mutable _url = url

    do base.ReserveProperties(["name"; "parameters"; "description"; "intendedUse"; "additionalProperties"; "components"; "version"; "url"])
    do base.SealInternal()
    /// Human-readable recipe title.
    member _.Name with get() = _name
    /// Prospective parameter slots, preserving references and duplicates.
    member _.Parameters with get() = _parameters
    /// Description of the planned procedure.
    member _.Description with get() = _description
    /// Classification as plain text or a defined term.
    member _.IntendedUse with get() = _intendedUse
    /// Extensible recipe metadata.
    member _.AdditionalProperties with get() = _additionalProperties
    /// Equipment, software, reagents, or other components.
    member _.Components with get() = _components
    /// Recipe version identifier.
    member _.Version with get() = _version
    /// URL of an external recipe resource.
    member _.Url with get() = _url

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string option> value
        | "parameters" -> _parameters <- Construction.collection (unbox<seq<FormalParameter>> value)
        | "description" -> _description <- unbox<string option> value
        | "intendedUse" -> _intendedUse <- unbox<RecipeIntendedUse option> value
        | "additionalProperties" -> _additionalProperties <- Construction.collection (unbox<seq<Annotation>> value)
        | "components" -> _components <- Construction.collection (unbox<seq<Annotation>> value)
        | "version" -> _version <- unbox<string option> value
        | "url" -> _url <- unbox<string option> value
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        _name |> Option.iter (Construction.text "Name" >> ignore)
        this.ValidateOwned("parameters",box _parameters,fun () -> for item in _parameters do if not (box item :? FormalParameter) then invalidArg "Parameters" "Unexpected entity kind.")
        _description |> Option.iter (Construction.text "Description" >> ignore)
#if FABLE_COMPILER
        _intendedUse |> Option.iter (fun item -> if not (Construction.isText (box item) || box item :? DefinedTerm) then invalidArg "IntendedUse" "Invalid value alternative.")
#else
        _intendedUse |> Option.iter (Construction.required "IntendedUse" >> ignore)
#endif
        this.ValidateOwned("additionalProperties",box _additionalProperties,fun () -> for item in _additionalProperties do if not (box item :? Annotation) then invalidArg "AdditionalProperties" "Unexpected entity kind.")
        this.ValidateOwned("components",box _components,fun () -> for item in _components do if not (box item :? Annotation) then invalidArg "Components" "Unexpected entity kind.")
        _version |> Option.iter (Construction.text "Version" >> ignore)
        _url |> Option.iter (Construction.text "Url" >> ignore)
        ()
    static member withName(value:Recipe,replacement:string):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box(Some replacement)))
    static member clearName(value:Recipe):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box(None:string option)))
    static member withParameters(value:Recipe,replacement:seq<FormalParameter>):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("parameters",box replacement))
    static member withDescription(value:Recipe,replacement:string):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("description",box(Some replacement)))
    static member clearDescription(value:Recipe):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("description",box(None:string option)))
    static member withIntendedUse(value:Recipe,replacement:RecipeIntendedUse):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("intendedUse",box(Some replacement)))
    static member clearIntendedUse(value:Recipe):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("intendedUse",box(None:RecipeIntendedUse option)))
    static member withAdditionalProperties(value:Recipe,replacement:seq<Annotation>):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalProperties",box replacement))
    static member withComponents(value:Recipe,replacement:seq<Annotation>):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("components",box replacement))
    static member withVersion(value:Recipe,replacement:string):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("version",box(Some replacement)))
    static member clearVersion(value:Recipe):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("version",box(None:string option)))
    static member withUrl(value:Recipe,replacement:string):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("url",box(Some replacement)))
    static member clearUrl(value:Recipe):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("url",box(None:string option)))
    static member withId(value:Recipe,replacement:string):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Recipe):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Recipe,replacement:seq<string>):Recipe = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Recipe,key:string,replacement:Entity):Recipe = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Recipe,key:string):Recipe = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
