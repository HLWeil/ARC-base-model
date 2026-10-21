namespace ARCBaseModel

open Fable.Core

/// Construction helpers without identity, normalization, or validation policies.
module internal Construction =
    let required name value =
        if isNull (box value) then nullArg name
        value

#if FABLE_COMPILER_PYTHON
    [<Emit("tuple($0)")>]
    let freeze (values: seq<'T>) : seq<'T> = jsNative

    [<Emit("__import__('copy').copy($0)")>]
    let shallowCopy (value: obj) : obj = jsNative
#else
#if FABLE_COMPILER
    [<Emit("Object.freeze(Array.from($0))")>]
    let freeze (values: seq<'T>) : seq<'T> = jsNative

    [<Emit("Object.assign(Object.create(Object.getPrototypeOf($0)), $0)")>]
    let shallowCopy (value: obj) : obj = jsNative
#else
    let freeze (values: seq<'T>) : seq<'T> =
        System.Collections.ObjectModel.ReadOnlyCollection<'T>(Seq.toArray values) :> seq<'T>
#endif
#endif

    /// Consumes the input once and returns an immutable collection.
    let collection name (values: seq<'T>) : seq<'T> = freeze (required name values)

    let optionalCollection name (values: seq<'T> option) : seq<'T> =
        match values with
        | Some items -> collection name items
        | None -> freeze Seq.empty
