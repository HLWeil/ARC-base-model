namespace ARCBaseModel
open Fable.Core

/// Portable recursive values; each erased alternative has a distinct runtime representation.
[<Erase; RequireQualifiedAccess>]
type Entity =
    | Object of EntityObject
    | Collection of EntityCollection
    | Number of float
    | Text of string
    | Bool of bool
    | Null of EntityNull
    | Blob of EntityBlob
/// Shared identity and type data, replaced as a unit by the copy methods.
and internal EntityCommon =
    { Id: string option
      AdditionalTypes: seq<string>
      Properties: EntityPropertyBag }
/// A typed object with optional identity and named extension values.
and [<AttachMembers>] EntityObject(entityType: string, ?id: string, ?additionalTypes: seq<string>) =
    let _type = Construction.required "entityType" entityType
    let mutable common =
        { Id = id
          AdditionalTypes = Construction.optionalCollection "additionalTypes" additionalTypes
          Properties = EntityPropertyBag.Empty() }
    let mutable reserved = set ["type"; "id"; "additionalTypes"]
    member _.Type = _type
    member _.Id = common.Id
    member _.AdditionalTypes = common.AdditionalTypes
    member _.EntityProperties = common.Properties
    member internal _.Common with get() = common and set(value) = common <- value
    member internal _.Reserved = reserved
    member internal _.ReserveProperties(names: string list) = reserved <- Set.union reserved (Set.ofList names)
    member internal this.CloneObject() : EntityObject =
#if FABLE_COMPILER
        unbox<EntityObject> (Construction.shallowCopy (box this))
#else
        this.MemberwiseClone() :?> EntityObject
#endif
    static member internal Copy<'T when 'T :> EntityObject>(value: 'T, change: 'T -> unit) : 'T =
        let source = Construction.required "value" value :> EntityObject
        let copy = unbox<'T> (source.CloneObject())
        change copy
        copy
    static member withId<'T when 'T :> EntityObject>(value: 'T, replacement: string option) : 'T =
        EntityObject.Copy(value, fun copy -> copy.Common <- { copy.Common with Id = replacement })
    static member withAdditionalTypes<'T when 'T :> EntityObject>(value: 'T, replacement: seq<string>) : 'T =
        let types = Construction.collection "additionalTypes" replacement
        EntityObject.Copy(value, fun copy -> copy.Common <- { copy.Common with AdditionalTypes = types })
    static member withProperty<'T when 'T :> EntityObject>(value: 'T, key: string, replacement: Entity) : 'T =
        Construction.required "key" key |> ignore
        Construction.required "replacement" replacement |> ignore
        if (Construction.required "value" value).Reserved.Contains(key) then
            invalidArg "key" "Reserved core property name."
        EntityObject.Copy(value, fun copy ->
            copy.Common <- { copy.Common with Properties = copy.Common.Properties.With(key, replacement) })
    static member withoutProperty<'T when 'T :> EntityObject>(value: 'T, key: string) : 'T =
        Construction.required "key" key |> ignore
        EntityObject.Copy(value, fun copy ->
            copy.Common <- { copy.Common with Properties = copy.Common.Properties.Without(key) })
/// Case-sensitive extension properties. The owning object reserves its core property names.
and [<AttachMembers>] EntityPropertyBag internal (values: Map<string, Entity>) =
    let keys = Construction.collection "keys" (values |> Map.toSeq |> Seq.map fst)
    static member internal Empty() = EntityPropertyBag(Map.empty)
    member _.Keys : seq<string> = keys
    member _.Contains(key: string) = (values.TryFind(Construction.required "key" key)).IsSome
    member _.Get(key: string) =
        match values.TryFind(Construction.required "key" key) with
        | Some value -> value
        | None -> invalidArg "key" "Missing extension property."
    member internal _.With(key: string, value: Entity) = EntityPropertyBag(values.Add(key, value))
    member internal _.Without(key: string) = EntityPropertyBag(values.Remove(key))
    member internal _.Values = values
/// An eagerly copied immutable collection preserving order, duplicates, and object references.
and [<AttachMembers>] EntityCollection(values: seq<Entity>) =
    let items = Seq.toArray values
    let count = items.Length
    member _.Count = count
    member _.Get(index: int) =
        if index < 0 || index >= count then invalidArg "index" "Index outside collection."
        items.[index]
/// Explicit null, distinct from a missing property.
and [<AttachMembers>] EntityNull() =
    member _.IsNull = true
/// Canonical standard padded base64, validated and preserved verbatim.
and [<AttachMembers>] EntityBlob(base64: string) =
    let _base64 = Construction.required "base64" base64
    do
        let alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
        if base64.Length % 4 <> 0 then invalidArg "base64" "Invalid base64 length."
        let mutable padding = 0
        for i = 0 to base64.Length - 1 do
            let c = base64.[i]
            if c = '=' then padding <- padding + 1
            elif padding > 0 || alphabet.IndexOf(c) < 0 then invalidArg "base64" "Invalid base64 character or padding."
        if padding > 2 then invalidArg "base64" "Invalid base64 padding."
        if padding > 0 then
            let last = alphabet.IndexOf(base64.[base64.Length - padding - 1])
            if (padding = 2 && last % 16 <> 0) || (padding = 1 && last % 4 <> 0) then
                invalidArg "base64" "Noncanonical base64 padding bits."
    member _.Base64 = _base64
