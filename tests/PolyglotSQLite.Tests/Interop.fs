module PolyglotSQLite.Tests.Interop

open System
open PolyglotSQLite

// Deliberately independent of ARC and generated fixture output. Every runtime
// writes these values, and every other runtime checks the same fixed oracle.
let private expected () = [|
    SqlValue.Null()
    SqlValue.Text("quote'; snowman \u2603; empty follows")
    SqlValue.Text("")
    SqlValue.Integer(Int64.MinValue)
    SqlValue.Integer(Int64.MaxValue)
    SqlValue.Integer(9007199254740993L)
    SqlValue.Real(0.125)
    SqlValue.Real(1.0)
    SqlValue.Blob([|0uy; 1uy; 128uy; 255uy|])
    SqlValue.Blob([||])
|]

let write path =
    let db = Sqlite.OpenFile(path)
    try
        db.Execute("CREATE TABLE fixture (position INTEGER PRIMARY KEY, value)")
        let values = expected ()
        db.WithTransaction(fun () ->
            for i = 0 to values.Length - 1 do
                db.Execute("INSERT INTO fixture VALUES ($position, $value)", [SqlParameter("position", SqlValue.Integer(int64 i)); SqlParameter("value", values.[i])]))
    finally db.Close()

let read path =
    let db = Sqlite.OpenFile(path)
    try
        let rows = db.Query("SELECT position, value FROM fixture ORDER BY position")
        let values = expected ()
        if rows.Count <> values.Length then failwith "Interoperability fixture row count differs."
        for i = 0 to values.Length - 1 do
            if rows.[i].Get(0).AsInteger() <> int64 i then failwith "Interoperability key differs."
            let actual, oracle = rows.[i].Get(1), values.[i]
            if actual.Kind <> oracle.Kind then failwith ("Interoperability storage kind differs at " + string i)
            let equal =
                match oracle.Kind with
                | "null" -> actual.IsNull
                | "text" -> actual.AsText() = oracle.AsText()
                | "integer" -> actual.AsInteger() = oracle.AsInteger()
                | "real" -> actual.AsReal() = oracle.AsReal()
                | "blob" -> actual.AsBlob() = oracle.AsBlob()
                | _ -> false
            if not equal then failwith ("Interoperability value differs at " + string i)
    finally db.Close()

let tryRun (args: string array) =
    match args with
    | [|"--write-fixture"; path|] -> write path; true
    | [|"--read-fixture"; path|] -> read path; true
    | _ -> false
