namespace PolyglotSQLite

open System
open Fable.Core

module internal ValueBoundary =
    let required name value =
        if isNull (box value) then nullArg name
        value

#if FABLE_COMPILER_PYTHON
    [<Emit("isinstance($0, str)")>]
    let validText (_value: string) : bool = nativeOnly
    [<Emit("(type($0) is int or isinstance($0, __import__('fable_library.core', fromlist=['int32']).int32)) and -2147483648 <= int($0) <= 2147483647")>]
    let validIndex (_value: int) : bool = nativeOnly
    [<Emit("__import__('fable_library.core', fromlist=['int32']).int32(int($0))")>]
    let index (_value: int) : int = nativeOnly
    [<Emit("(type($0) is int or isinstance($0, __import__('fable_library.core', fromlist=['int64']).int64)) and -9223372036854775808 <= int($0) <= 9223372036854775807")>]
    let validInteger (_value: int64) : bool = nativeOnly
    [<Emit("__import__('fable_library.core', fromlist=['int64']).int64(int($0))")>]
    let integer (_value: int64) : int64 = nativeOnly
    [<Emit("type($0) in (int, float) or isinstance($0, __import__('fable_library.core', fromlist=['float64']).float64)")>]
    let validReal (_value: float) : bool = nativeOnly
    [<Emit("__import__('fable_library.core', fromlist=['float64']).float64(float($0))")>]
    let real (_value: float) : float = nativeOnly
    [<Emit("isinstance($0, (bytes, bytearray)) or (isinstance($0, __import__('fable_library.array_', fromlist=['Array']).Array) and all((type(item) is int or isinstance(item, __import__('fable_library.core', fromlist=['uint8']).uint8)) and 0 <= int(item) <= 255 for item in $0))")>]
    let validBlob (_value: byte[]) : bool = nativeOnly
    [<Emit("__import__('fable_library.array_', fromlist=['Array']).Array([__import__('fable_library.core', fromlist=['uint8']).uint8(int(item)) for item in $0])")>]
    let copyBlob (_value: byte[]) : byte[] = nativeOnly
    [<Emit("int($0)")>]
    let nativeInteger (_value: int64) : obj = nativeOnly
    [<Emit("float($0)")>]
    let nativeReal (_value: float) : obj = nativeOnly
    [<Emit("bytes(int(item) for item in $0)")>]
    let nativeBlob (_value: byte[]) : obj = nativeOnly
    [<Emit("int($0)")>]
    let nativeCount (_value: int) : obj = nativeOnly
#else
#if FABLE_COMPILER
    [<Emit("typeof $0 === 'string'")>]
    let validText (_value: string) : bool = jsNative
    [<Emit("Number.isInteger($0) && $0 >= -2147483648 && $0 <= 2147483647")>]
    let validIndex (_value: int) : bool = jsNative
    let index value = value
    [<Emit("typeof $0 === 'bigint' && $0 >= -9223372036854775808n && $0 <= 9223372036854775807n")>]
    let validInteger (_value: int64) : bool = jsNative
    let integer value = value
    [<Emit("typeof $0 === 'number'")>]
    let validReal (_value: float) : bool = jsNative
    let real value = value
    [<Emit("$0 instanceof Uint8Array")>]
    let validBlob (_value: byte[]) : bool = jsNative
    [<Emit("new Uint8Array($0)")>]
    let copyBlob (_value: byte[]) : byte[] = jsNative
#else
    let validText (value: string) = not (isNull value)
    let validIndex (_value: int) = true
    let index value = value
    let validInteger (_value: int64) = true
    let integer value = value
    let validReal (_value: float) = true
    let real value = value
    let validBlob (value: byte[]) = not (isNull value)
    let copyBlob (value: byte[]) = Array.copy value
#endif
#endif

type private StoredValue =
    | Null
    | Text of string
    | Integer of int64
    | Real of float
    | Blob of byte[]

/// An immutable SQLite storage value. Readers are strict; no implicit coercion is performed.
[<AttachMembers>]
type SqlValue private (stored: StoredValue) =
    let wrong expected = invalidOp $"SQLite value is not {expected}."

    member _.Kind =
        match stored with
        | Null -> "null"
        | Text _ -> "text"
        | Integer _ -> "integer"
        | Real _ -> "real"
        | Blob _ -> "blob"

    member _.IsNull = match stored with Null -> true | _ -> false
    member _.AsText() = match stored with Text value -> value | _ -> wrong "TEXT"

#if FABLE_COMPILER_PYTHON
    [<CompiledName("_readIntegerFSharp")>]
#endif
    member _.AsInteger() = match stored with Integer value -> value | _ -> wrong "INTEGER"

#if FABLE_COMPILER_PYTHON
    [<CompiledName("_readRealFSharp")>]
#endif
    member _.AsReal() = match stored with Real value -> value | _ -> wrong "REAL"

#if FABLE_COMPILER_PYTHON
    [<CompiledName("_readBlobFSharp")>]
#endif
    member _.AsBlob() = match stored with Blob value -> ValueBoundary.copyBlob value | _ -> wrong "BLOB"

#if FABLE_COMPILER_PYTHON
    [<CompiledName("AsInteger")>]
    member this.NativeInteger() = ValueBoundary.nativeInteger (this.AsInteger())
    [<CompiledName("AsReal")>]
    member this.NativeReal() = ValueBoundary.nativeReal (this.AsReal())
    [<CompiledName("AsBlob")>]
    member this.NativeBlob() = ValueBoundary.nativeBlob (this.AsBlob())
#endif

    static member Null() = SqlValue(Null)
    static member Text(value: string) =
        if not (ValueBoundary.validText value) then invalidArg "value" "TEXT requires a string."
        SqlValue(Text value)
    static member Integer(value: int64) =
        if not (ValueBoundary.validInteger value) then invalidArg "value" "INTEGER requires a signed 64-bit integer."
        SqlValue(Integer(ValueBoundary.integer value))
    static member Real(value: float) =
        if not (ValueBoundary.validReal value) then invalidArg "value" "REAL requires a number, excluding Boolean values."
        let value = ValueBoundary.real value
        if Double.IsNaN value then invalidArg "value" "SQLite cannot preserve NaN; use an explicit NULL if intended."
        SqlValue(Real value)
    static member Blob(value: byte[]) =
        if not (ValueBoundary.validBlob value) then invalidArg "value" "BLOB requires a byte buffer."
        SqlValue(Blob(ValueBoundary.copyBlob value))

/// A named binding; canonical names use a leading '$'.
[<AttachMembers>]
type SqlParameter(name: string, value: SqlValue) =
    let _name =
        ValueBoundary.required "name" name |> ignore
        let bare = if name.StartsWith("$") then name.Substring(1) else name
        let letter c = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c = '_'
        if bare.Length = 0 || not (letter bare[0]) || (bare |> Seq.exists (fun c -> not (letter c || (c >= '0' && c <= '9')))) then
            invalidArg "name" "Parameter names must match [A-Za-z_][A-Za-z0-9_]*, optionally prefixed with '$'."
        "$" + bare
    let _value = ValueBoundary.required "value" value
    member _.Name = _name
    member _.Value = _value

/// An ordered result row, including repeated column names. Name lookup rejects ambiguity.
[<AttachMembers>]
type SqlRow(columnNames: seq<string>, values: seq<SqlValue>) =
    let _names = ResizeArray<string>(ValueBoundary.required "columnNames" columnNames)
    let _values = ResizeArray<SqlValue>(ValueBoundary.required "values" values)
    do
        if _names.Count <> _values.Count then invalidArg "values" "Column names and values must have equal lengths."
        for name in _names do ValueBoundary.required "columnNames" name |> ignore
        for value in _values do ValueBoundary.required "values" value |> ignore
    let checkIndex index =
        if not (ValueBoundary.validIndex index) then invalidArg "index" "Column index must be an integer."
        let index = ValueBoundary.index index
        if index < 0 || index >= _values.Count then invalidArg "index" "Column index is outside the row."
        index
    let find name =
        ValueBoundary.required "name" name |> ignore
        let mutable found = -1
        for index = 0 to _names.Count - 1 do
            if _names[index] = name then
                if found >= 0 then invalidArg "name" $"Column name '{name}' is ambiguous; use its ordinal."
                found <- index
        found
#if FABLE_COMPILER_PYTHON
    // CompiledName is ignored on properties by the pinned backend. Redirect F#
    // reads explicitly and emit the native property from a decorated method.
    [<Emit("$0._countFSharp()")>]
    member _.Count = _values.Count
    [<CompiledName("_countFSharp")>]
    member internal _.ReadCount() = _values.Count
    [<CompiledName("Count"); Py.Decorate("property")>]
    member _.NativeCount() = ValueBoundary.nativeCount _values.Count
#else
    member _.Count = _values.Count
#endif
    member _.GetColumnName(index: int) = _names[checkIndex index]
    member _.Get(index: int) = _values[checkIndex index]
    member _.TryGetByName(name: string) =
        let index = find name
        if index < 0 then None else Some _values[index]
    member _.GetByName(name: string) =
        let index = find name
        if index < 0 then invalidArg "name" $"Column '{name}' does not exist."
        _values[index]

module internal Parameters =
    let copyAndValidate (parameters: seq<SqlParameter> option) =
        let result = ResizeArray<SqlParameter>()
        let names = Collections.Generic.HashSet<string>()
        match parameters with
        | None -> ()
        | Some values ->
            for parameter in ValueBoundary.required "parameters" values do
                ValueBoundary.required "parameters" parameter |> ignore
                if not (names.Add parameter.Name) then invalidArg "parameters" $"Duplicate parameter '{parameter.Name}'."
                result.Add parameter
        result
