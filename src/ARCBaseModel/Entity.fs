namespace ARCBaseModel
open Fable.Core
open System.Collections.Generic

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
/// A typed object with optional identity and named extension values.
and [<AttachMembers>] EntityObject(entityType: string, ?id: string, ?additionalTypes: seq<string>) =
    let _type = Construction.required "entityType" entityType
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes
    let _properties = EntityPropertyBag()
    do _properties.Reserve(["type"; "id"; "additionalTypes"])
    member _.Type = _type
    member _.Id with get() = _id and set(value) = _id <- value
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    member _.EntityProperties = _properties
    member _.AddEntityProperty(key: string, value: Entity) = _properties.Add(key, value)
    member _.SetEntityProperty(key: string, value: Entity) = _properties.Set(key, value)
    member _.RemoveEntityProperty(key: string) = _properties.Remove(key)
/// Case-sensitive extension properties; core names are reserved.
and [<AttachMembers>] EntityPropertyBag() =
    let _values = Dictionary<string, Entity>()
    let _reserved = HashSet<string>()
    let check key =
        Construction.required "key" key |> ignore
        if _reserved.Contains(key) then invalidArg "key" "Reserved core property name."
    member internal _.Reserve(keys: seq<string>) =
        for key in keys do _reserved.Add(key) |> ignore
    member _.Keys = ResizeArray<string>(_values.Keys)
    member _.Contains(key: string) =
        let found, _ = _values.TryGetValue(key)
        found
    member _.Get(key: string) =
        match _values.TryGetValue(key) with
        | true, value -> value
        | false, _ -> invalidArg "key" "Missing extension property."
    member _.Add(key: string, value: Entity) =
        check key
        if _values.ContainsKey(key) then invalidArg "key" "Duplicate extension property."
        _values.Add(key, value)
    member _.Set(key: string, value: Entity) =
        check key
        _values.[key] <- value
    member _.Remove(key: string) = _values.Remove(key)
/// An eagerly copied collection preserving order, duplicates, and object references.
and [<AttachMembers>] EntityCollection(values: seq<Entity>) =
    let _values = ResizeArray<Entity>(values)
    member _.Count = _values.Count
    member _.Get(index: int) =
        if index < 0 || index >= _values.Count then invalidArg "index" "Index outside collection."
        _values.[index]
    member _.Set(index: int, value: Entity) =
        if index < 0 || index >= _values.Count then invalidArg "index" "Index outside collection."
        _values.[index] <- value
    member _.Add(value: Entity) = _values.Add(value)
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
