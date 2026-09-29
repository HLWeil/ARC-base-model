namespace PolyglotSQLite

open System
open Fable.Core

module private Identifiers =
    let validate name (value: string) =
        ValueBoundary.required name value |> ignore
        if value.Length = 0 || value.IndexOf('\000') >= 0 then invalidArg name "SQLite identifiers must be nonempty and cannot contain NUL."
        value

    let quote value = "\"" + (validate "identifier" value).Replace("\"", "\"\"") + "\""

    // SQLite folds ASCII identifier letters only. Avoid .NET-only comparers and
    // Unicode case folding, which would reject valid distinct quoted names.
    let comparisonKey (value: string) =
        value.ToCharArray()
        |> Array.map (fun c -> if c >= 'A' && c <= 'Z' then char (int c + 32) else c)
        |> String

    let copyDistinct name (items: seq<string>) =
        let result = ResizeArray<string>()
        let seen = Collections.Generic.HashSet<string>()
        for item in ValueBoundary.required name items do
            validate name item |> ignore
            if not (seen.Add (comparisonKey item)) then invalidArg name ("Duplicate SQLite identifier '" + item + "'.")
            result.Add item
        if result.Count = 0 then invalidArg name "At least one identifier is required."
        result

/// Immutable metadata and caller-owned codecs. Container inputs and getters are copied.
[<AttachMembers>]
type Table<'T>(name: string, columns: seq<string>, primaryKey: seq<string>,
               encode: 'T -> ResizeArray<SqlValue>, decode: SqlRow -> 'T) =
    let _name = Identifiers.validate "name" name
    let _columns = Identifiers.copyDistinct "columns" columns
    let _primaryKey = Identifiers.copyDistinct "primaryKey" primaryKey
    let _encode = ValueBoundary.required "encode" encode
    let _decode = ValueBoundary.required "decode" decode
    do
        for key in _primaryKey do
            if not (_columns.Contains key) then
                invalidArg "primaryKey" ("Key '" + key + "' must exactly match a declared column name.")

    member _.Name = _name
    member _.Columns = ResizeArray<string>(_columns)
    member _.PrimaryKey = ResizeArray<string>(_primaryKey)

    member internal _.Encode(row: 'T) =
        let values = ResizeArray<SqlValue>(ValueBoundary.required "encode" (_encode row))
        if values.Count <> _columns.Count then invalidArg "encode" "Encoder value count does not match the table columns."
        for value in values do ValueBoundary.required "encode" value |> ignore
        values

    member internal _.Decode(row: SqlRow) =
        // Get reserves native absence for a missing database row. Reject an
        // absent decoded value instead of leaking Fable's Some(null) wrapper.
        let result = _decode row
        if isNull (box result) then invalidOp "Table decoders must return a non-null row value."
        result

/// Small parameterized CRUD operations; schema and identity policies belong to the caller.
[<AttachMembers>]
type TableRepository<'T>(connection: SqliteConnection, table: Table<'T>) =
    let _connection = ValueBoundary.required "connection" connection
    let _table = ValueBoundary.required "table" table
    let columns = _table.Columns
    let keys = _table.PrimaryKey
    let tableName = Identifiers.quote _table.Name
    let columnList = columns |> Seq.map Identifiers.quote |> String.concat ", "
    let keyIndexes = keys |> Seq.map (fun key -> columns.IndexOf key) |> ResizeArray
    let nonKeyIndexes =
        seq { for index = 0 to columns.Count - 1 do if not (keyIndexes.Contains index) then yield index }
        |> ResizeArray
    let parameterName index = "$p" + string index
    let whereClause =
        // Ordinary SQLite rowid tables can have NULL in non-INTEGER primary keys.
        // IS preserves ordinary equality while also matching those key values.
        keys |> Seq.mapi (fun index key -> Identifiers.quote key + " IS " + parameterName index) |> String.concat " AND "
    let bindings (values: seq<SqlValue>) =
        values |> Seq.mapi (fun index value -> SqlParameter(parameterName index, value)) |> ResizeArray
    let keyBindings (keyValues: seq<SqlValue>) =
        let values = ResizeArray<SqlValue>(ValueBoundary.required "keyValues" keyValues)
        if values.Count <> keys.Count then invalidArg "keyValues" "Key value count does not match the table primary key."
        bindings values

    member _.Insert(row: 'T) =
        let values = _table.Encode row
        let placeholders = values |> Seq.mapi (fun index _ -> parameterName index) |> String.concat ", "
        _connection.Execute("INSERT INTO " + tableName + " (" + columnList + ") VALUES (" + placeholders + ")", bindings values)

    member _.Update(row: 'T) =
        if nonKeyIndexes.Count = 0 then invalidOp "The table has no non-primary-key columns to update."
        let values = _table.Encode row
        let assignments =
            nonKeyIndexes |> Seq.mapi (fun index column -> Identifiers.quote columns[column] + " = " + parameterName (keys.Count + index))
            |> String.concat ", "
        let ordered = seq {
            for index in keyIndexes do yield values[index]
            for index in nonKeyIndexes do yield values[index]
        }
        _connection.Execute("UPDATE " + tableName + " SET " + assignments + " WHERE " + whereClause, bindings ordered)

    member _.Delete(keyValues: seq<SqlValue>) =
        _connection.Execute("DELETE FROM " + tableName + " WHERE " + whereClause, keyBindings keyValues)

    member _.Get(keyValues: seq<SqlValue>) =
        let rows = _connection.Query("SELECT " + columnList + " FROM " + tableName + " WHERE " + whereClause, keyBindings keyValues)
        if rows.Count > 1 then invalidOp "Expected at most one row for the supplied primary key."
        if rows.Count = 0 then None else Some(_table.Decode rows[0])

    member _.List() =
        let order = keys |> Seq.map Identifiers.quote |> String.concat ", "
        _connection.Query("SELECT " + columnList + " FROM " + tableName + " ORDER BY " + order)
        |> Seq.map _table.Decode |> ResizeArray
