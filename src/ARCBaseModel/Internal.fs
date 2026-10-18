namespace ARCBaseModel

open System
open System.Collections.Generic
open Fable.Core

module internal Construction =
    let mutable ValidatedContainers = 0
    let required name value =
        if isNull (box value) then nullArg name
        value
#if FABLE_COMPILER_PYTHON
    [<Emit("isinstance($0, str)")>]
    let isText (_value:obj):bool = nativeOnly
    [<Emit("type($0) in (int, float) or isinstance($0, type(__import__('fable_library.core', fromlist=['float64']).float64(0.0)))")>]
    let isNumber (_value:obj):bool = nativeOnly
    [<Emit("type($0) is bool")>]
    let isBoolean (_value:obj):bool = nativeOnly
    [<Emit("type($0) is int")>]
    let isIndex (_value:int):bool = nativeOnly
    [<Emit("type('_ImmutableTuple', (tuple,), {'__slots__': (), 'System_Collections_Generic_IReadOnlyCollection_1_get_Count': lambda self: __import__('fable_library.core', fromlist=['int32']).int32(len(self)), 'System_Collections_Generic_IReadOnlyList_1_get_ItemZ524259A4': lambda self, index: self[int(index)]})")>]
    let private createTupleType(): obj = nativeOnly
    let private tupleType = createTupleType()
    [<Emit("$0($1)")>]
    let private tupleValues (_kind:obj) (_values:seq<'T>):IReadOnlyList<'T> = nativeOnly
    let frozen values = tupleValues tupleType values
    [<Emit("__import__('types').MappingProxyType(dict($0))")>]
    let map (_pairs:seq<string*'T>):obj = nativeOnly
    [<Emit("$1 in $0")>]
    let contains<'T> (_map:obj) (_key:string):bool = nativeOnly
    [<Emit("$0[$1]")>]
    let get<'T> (_map:obj) (_key:string):'T = nativeOnly
    [<Emit("list($0.items())")>]
    let pairs<'T> (_map:obj):seq<string*'T> = nativeOnly
    [<Emit("list($0.keys())")>]
    let keys<'T> (_map:obj):seq<string> = nativeOnly
    [<Emit("object.__new__(type($0))")>]
    let allocateClone (_source:obj):obj = nativeOnly
    [<Emit("object.__setattr__($0, $1, $2)")>]
    let assign (_target:obj) (_name:string) (_value:obj):unit = nativeOnly
    [<Emit("vars($0).items()")>]
    let attributes (_source:obj):seq<string*obj> = nativeOnly
    [<Emit("object.__getattribute__($0, $1)")>]
    let attribute (_source:obj) (_name:string):obj = nativeOnly
    [<Emit("__import__('types').MappingProxyType(object.__getattribute__($0, '__dict__'))")>]
    let attributeView (_source:obj):obj = nativeOnly
    let clone source =
        let target = allocateClone source
        for name,value in attributes source do assign target name value
        assign target "_sealed" (box false)
        target
    [<Emit("getattr($0, '_sealed', False)")>]
    let isSealed (_value:obj):bool = nativeOnly
    [<Emit("object.__delattr__($0, $1)")>]
    let deleteAttribute (_value:obj) (_name:string):unit = nativeOnly
    let freeze (_value:obj) = ()
#else
#if FABLE_COMPILER
    [<Emit("typeof $0 === 'string'")>]
    let isText (_value:obj):bool = jsNative
    [<Emit("typeof $0 === 'number'")>]
    let isNumber (_value:obj):bool = jsNative
    [<Emit("typeof $0 === 'boolean'")>]
    let isBoolean (_value:obj):bool = jsNative
    [<Emit("Number.isInteger($0)")>]
    let isIndex (_value:int):bool = jsNative
    [<Emit("Object.freeze(Object.defineProperty(Array.from($0), 'System.Collections.Generic.IReadOnlyList`1.get_ItemZ524259A4', {value: function(index) { return this[index]; }}))")>]
    let frozen (_values:seq<'T>):IReadOnlyList<'T> = jsNative
    [<Emit("Object.freeze(Object.fromEntries($0))")>]
    let map (_pairs:seq<string*'T>):obj = jsNative
    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let contains<'T> (_map:obj) (_key:string):bool = jsNative
    [<Emit("$0[$1]")>]
    let get<'T> (_map:obj) (_key:string):'T = jsNative
    [<Emit("Object.entries($0)")>]
    let pairs<'T> (_map:obj):seq<string*'T> = jsNative
    [<Emit("Object.keys($0)")>]
    let keys<'T> (_map:obj):seq<string> = jsNative
    [<Emit("Object.assign(Object.create(Object.getPrototypeOf($0)), $0, {_sealed:false})")>]
    let clone (_source:obj):obj = jsNative
    [<Emit("Object.freeze($0)")>]
    let freeze (_value:obj):unit = jsNative
#else
    let isText (value:obj) = value :? string
    let isNumber (value:obj) = value :? float
    let isBoolean (value:obj) = value :? bool
    let isIndex (_value:int) = true
    let frozen (values:seq<'T>):IReadOnlyList<'T> = Array.AsReadOnly(Seq.toArray values)
    let map (pairs:seq<string*'T>):obj =
        let values = Dictionary<string,'T>(StringComparer.Ordinal)
        for key,value in pairs do values.Add(key,value)
        box (System.Collections.ObjectModel.ReadOnlyDictionary<string,'T>(values))
    let contains<'T> (map:obj) key = (unbox<IReadOnlyDictionary<string,'T>> map).ContainsKey key
    let get<'T> (map:obj) key = (unbox<IReadOnlyDictionary<string,'T>> map)[key]
    let pairs<'T> (map:obj) = (unbox<IReadOnlyDictionary<string,'T>> map) |> Seq.map (fun pair -> pair.Key,pair.Value)
    let keys<'T> (map:obj) = (unbox<IReadOnlyDictionary<string,'T>> map).Keys
    let freeze (_value:obj) = ()
#endif
#endif
    let text name value =
        if not (isText (box value)) then invalidArg name "Expected text."
        value
    let collection values = frozen (required "values" values)
    let optionalCollection values = defaultArg values Seq.empty |> collection

    let validateBase64 (base64:string):unit =
        let alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
        if (base64.Length &&& 3) <> 0 then invalidArg "base64" "Invalid base64 length."
        let mutable padding = 0
        for i=0 to base64.Length-1 do
            let c=base64[i]
            if c='=' then padding <- padding+1
            elif padding>0 || alphabet.IndexOf(c)<0 then invalidArg "base64" "Invalid base64 character or padding."
        if padding>2 then invalidArg "base64" "Invalid base64 padding."
        if padding>0 then
            let last=alphabet.IndexOf(base64[base64.Length-padding-1])
            if (padding=2 && (last &&& 15)<>0) || (padding=1 && (last &&& 3)<>0) then invalidArg "base64" "Noncanonical base64 padding bits."

[<AttachMembers>]
type internal ImmutableCollections =
    static member OfSeq(values:seq<'T>):IReadOnlyList<'T> = Construction.collection values
    static member ValidationCount = Construction.ValidatedContainers

[<AttachMembers>]
type ImmutableObject() =
    let mutable _sealed = false
    member internal _.EnsureUnsealed() =
        if _sealed then invalidOp "Immutable values cannot be changed."
    member internal this.SealObject() =
        this.EnsureUnsealed()
        _sealed <- true
        Construction.freeze (box this)
    member internal _.IsSealed = _sealed
    member private _.ResetClone() = _sealed <- false
    member internal this.CloneObject() : obj =
#if FABLE_COMPILER
        Construction.clone (box this)
#else
        let clone = this.MemberwiseClone() :?> ImmutableObject
        clone.ResetClone()
        box clone
#endif
#if FABLE_COMPILER_PYTHON
    [<CompiledName("__getattribute__")>]
    member this.ReadAttribute(name:string):obj =
        if name="__dict__" && Construction.isSealed (box this) then Construction.attributeView (box this)
        else Construction.attribute (box this) name
    [<CompiledName("__setattr__")>]
    member this.AssignAttribute(name:string,value:obj) =
        if Construction.isSealed (box this) then invalidOp "Immutable values cannot be changed."
        Construction.assign (box this) name value
    [<CompiledName("__delattr__")>]
    member this.DeleteAttribute(name:string) =
        if Construction.isSealed (box this) then invalidOp "Immutable values cannot be changed."
        Construction.deleteAttribute (box this) name
#endif
