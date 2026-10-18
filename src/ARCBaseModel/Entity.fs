namespace ARCBaseModel
open System.Collections.Generic
open Fable.Core

[<Erase; RequireQualifiedAccess>]
type Entity =
    | Object of EntityObject
    | Collection of EntityCollection
    | Number of float
    | Text of string
    | Bool of bool
    | Null of EntityNull
    | Blob of EntityBlob
and [<AttachMembers>] EntityObject(entityType:string, ?id:string, ?additionalTypes:seq<string>) as this =
    inherit ImmutableObject()
    let _type = Construction.text "entityType" entityType
    let mutable _id = id
    let mutable _additionalTypes = Construction.optionalCollection additionalTypes
    let mutable _properties = EntityPropertyBag(reserved=["type";"id";"additionalTypes"])
    let mutable _validatedContainers = Construction.map (Seq.empty<string*obj>)
    do
#if FABLE_COMPILER_PYTHON
        let exact:bool = Fable.Core.PyInterop.emitPyExpr this "type($0) is EntityObject"
#else
#if FABLE_COMPILER
        let exact:bool = Fable.Core.JsInterop.emitJsExpr this "Object.getPrototypeOf($0) === EntityObject.prototype"
#else
        let exact = this.GetType() = typeof<EntityObject>
#endif
#endif
        if exact then this.SealInternal()
    member _.Type = _type
    member _.Id = _id
    member _.AdditionalTypes = _additionalTypes
    member _.EntityProperties = _properties
    member internal this.ReserveProperties(keys:seq<string>) =
        this.EnsureUnsealed()
        _properties <- _properties.WithReserved(keys)
    member internal this.ValidateOwned(key:string,values:obj,validate:unit->unit) =
        this.EnsureUnsealed()
        let cached=Construction.contains<obj> _validatedContainers key
        if not cached || not (obj.ReferenceEquals(Construction.get<obj> _validatedContainers key,values)) then
            Construction.ValidatedContainers <- Construction.ValidatedContainers+1
            validate()
            _validatedContainers <- Construction.map (Seq.append (Construction.pairs<obj> _validatedContainers |> Seq.filter (fun (name,_) -> name<>key)) [key,values])
    abstract member ValidateInternal:unit->unit
    default _.ValidateInternal() = ()
    member internal this.SealInternal() =
        this.ValidateInternal()
        _id |> Option.iter (Construction.text "id" >> ignore)
        this.ValidateOwned("additionalTypes",box _additionalTypes,fun () -> _additionalTypes |> Seq.iter (Construction.text "additionalTypes" >> ignore))
        this.SealObject()
    abstract member AssignInternal:string*obj->unit
    default this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "id" -> _id <- unbox value
        | "additionalTypes" -> _additionalTypes <- Construction.collection (unbox<seq<string>> value)
        | _ -> invalidArg "key" ("Unknown core property: " + key)
    member internal this.SetExtensionInternal(key:string,value:Entity) =
        this.EnsureUnsealed()
        _properties <- _properties.With(key,value)
    member internal this.RemoveExtensionInternal(key:string) =
        this.EnsureUnsealed()
        _properties <- _properties.Without(key)
    static member internal Copy<'T when 'T :> EntityObject>(value:'T, change:'T->unit):'T =
        Construction.required "value" value |> ignore
        let copy = value.CloneObject() |> unbox<'T>
        change copy
        copy.SealInternal()
        copy
    static member withId(value:EntityObject,replacement:string) = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:EntityObject) = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:EntityObject,replacement:seq<string>) = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:EntityObject,key:string,replacement:Entity) = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:EntityObject,key:string) = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
and [<AttachMembers>] EntityPropertyBag(?values:seq<string*Entity>,?reserved:seq<string>) as this =
    inherit ImmutableObject()
    let _values = Construction.map (defaultArg values Seq.empty)
    let _keys = Construction.keys<Entity> _values |> Construction.collection
    let _reserved = Construction.optionalCollection reserved
    do
        for key in _keys do
            Construction.text "key" key |> ignore
            if Seq.contains key _reserved then invalidArg "key" "Reserved core property name."
        this.SealObject()
    member _.Keys = _keys
    member _.Contains(key:string) = Construction.contains<Entity> _values key
    member _.Get(key:string):Entity =
        if not (Construction.contains<Entity> _values key) then invalidArg "key" "Missing extension property."
        Construction.get<Entity> _values key
    member internal _.WithReserved(keys:seq<string>) = EntityPropertyBag(Construction.pairs<Entity> _values,Seq.append _reserved keys)
    member internal _.With(key:string,value:Entity) =
        let entries = Construction.pairs<Entity> _values |> Seq.filter (fun (existing,_) -> existing <> key)
        EntityPropertyBag(Seq.append entries [key,value],_reserved)
    member internal _.Without(key:string) = EntityPropertyBag(Construction.pairs<Entity> _values |> Seq.filter (fun (existing,_) -> existing <> key),_reserved)
and [<AttachMembers>] EntityCollection(values:seq<Entity>) as this =
    inherit ImmutableObject()
    let _values = Construction.collection values
    do this.SealObject()
    member _.Count = _values.Count
    member _.Get(index:int) =
        if not (Construction.isIndex index) || index < 0 || index >= _values.Count then invalidArg "index" "Expected an integer index inside the collection."
        _values[index]
    static member withValues(value:EntityCollection,replacement:seq<Entity>) =
        Construction.required "value" value |> ignore
        EntityCollection(replacement)
and [<AttachMembers>] EntityNull() as this =
    inherit ImmutableObject()
    do this.SealObject()
    member _.IsNull = true
and [<AttachMembers>] EntityBlob(base64:string) as this =
    inherit ImmutableObject()
    let _base64 = Construction.text "base64" base64
    do
        Construction.validateBase64 _base64
        this.SealObject()
    member _.Base64 = _base64
