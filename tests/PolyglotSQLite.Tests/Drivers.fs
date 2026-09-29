module PolyglotSQLite.Tests.Drivers

open System
open PolyglotSQLite
open Fable.Pyxpecto

let withDatabase action =
    let database = Sqlite.OpenInMemory()
    try action database
    finally database.Close()

let integer (database: PolyglotSQLite.SqliteConnection) sql =
    database.Scalar(sql).Value.AsInteger()

let tests = testList "drivers" [
    testCase "R05 R06 all SQLite storage classes round trip without coercion" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE values_test (position INTEGER PRIMARY KEY, value)")
            let expected = [|
                SqlValue.Null(); SqlValue.Text(""); SqlValue.Text("quote'; DROP TABLE values_test; --")
                SqlValue.Integer(Int64.MinValue); SqlValue.Integer(-9007199254740993L)
                SqlValue.Integer(0L); SqlValue.Integer(9007199254740993L); SqlValue.Integer(Int64.MaxValue)
                SqlValue.Real(0.125); SqlValue.Real(1.0); SqlValue.Real(Double.PositiveInfinity); SqlValue.Real(Double.NegativeInfinity)
                SqlValue.Blob([||]); SqlValue.Blob([|0uy; 128uy; 255uy|])
            |]
            for i = 0 to expected.Length - 1 do
                db.Execute("INSERT INTO values_test VALUES ($position, $value)", [SqlParameter("position", SqlValue.Integer(int64 i)); SqlParameter("value", expected.[i])])
            let actual = db.Query("SELECT value, typeof(value) AS storage FROM values_test ORDER BY position")
            Expect.equal actual.Count expected.Length "All rows survive"
            for i = 0 to expected.Length - 1 do
                let value = actual.[i].Get(0)
                Expect.equal value.Kind expected.[i].Kind "Native storage class survives"
                Expect.equal (actual.[i].Get(1).AsText()) expected.[i].Kind "SQLite reports matching storage class"
                match value.Kind with
                | "null" -> Expect.isTrue value.IsNull "Explicit NULL"
                | "text" -> Expect.equal (value.AsText()) (expected.[i].AsText()) "Text round trip"
                | "integer" -> Expect.equal (value.AsInteger()) (expected.[i].AsInteger()) "Exact integer round trip"
                | "real" -> Expect.equal (value.AsReal()) (expected.[i].AsReal()) "Real round trip"
                | "blob" -> Suspect.sequenceEqual (value.AsBlob()) (expected.[i].AsBlob()) "BLOB round trip"
                | kind -> failwith ("Unexpected kind " + kind)

    testCase "R03 R04 scalar uses SELECT ordinal rather than column names" <| fun _ ->
        withDatabase <| fun db ->
            Expect.equal (integer db "SELECT 11 AS z, 22 AS a") 11L "Alphabetic name order is irrelevant"
            Expect.equal (db.Scalar("SELECT 'first' AS \"10\", 'second' AS \"2\"").Value.AsText()) "first" "JS property enumeration is irrelevant"

    testCase "R07 R08 duplicate rows and missing scalar versus NULL" <| fun _ ->
        withDatabase <| fun db ->
            let row = db.Query("SELECT 1 AS x, 2 AS x").[0]
            Expect.equal row.Count 2 "Duplicate columns retained"
            Expect.equal (row.Get(0).AsInteger()) 1L "First duplicate"
            Expect.equal (row.Get(1).AsInteger()) 2L "Second duplicate"
            Expect.throws (fun () -> row.GetByName("x") |> ignore) "Name is ambiguous"
            Expect.isTrue (db.Scalar("SELECT NULL").Value.IsNull) "NULL is a present value"
            Expect.isNone (db.Scalar("SELECT 1 WHERE 0")) "No row is absent"

    testCase "R09 duplicate parameters fail before execution" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE parameters (value)")
            Expect.throws
                (fun () -> db.Execute("INSERT INTO parameters VALUES ($x)", [SqlParameter("x", SqlValue.Integer(1L)); SqlParameter("$x", SqlValue.Integer(2L))]))
                "Normalized duplicates are rejected"
            Expect.equal (integer db "SELECT count(*) FROM parameters") 0L "No value is silently selected or inserted"
            Expect.equal (db.Scalar("SELECT $x", [SqlParameter("$x", SqlValue.Text("safe';--"))]).Value.AsText()) "safe';--" "Values stay bound"

    testCase "parameter sequences are consumed once and unsupported placeholder forms are rejected" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE bindings (value)")
            let mutable enumerations = 0
            let parameters = seq {
                enumerations <- enumerations + 1
                if enumerations > 1 then failwith "Binding sequence was enumerated twice"
                yield SqlParameter("value", SqlValue.Text("one enumeration"))
            }
            db.Execute("INSERT INTO bindings VALUES ($value)", parameters)
            Expect.equal enumerations 1 "Consume caller sequence once"
            for placeholder in ["@value"; ":value"; "?1"] do
                Expect.throws
                    (fun () -> db.Execute("INSERT INTO bindings VALUES (" + placeholder + ")", [SqlParameter("value", SqlValue.Text("unsupported"))]))
                    "Only canonical named placeholders supported"
            Expect.equal (integer db "SELECT count(*) FROM bindings") 1L "Unsupported forms made no changes"

    testCase "R02 single-statement methods reject batches before any statement executes" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE single_statement (value)")
            for parameters in [None; Some ([] : SqlParameter list); Some [SqlParameter("x", SqlValue.Integer(5L))]] do
                let sql = if parameters |> Option.exists (fun p -> not p.IsEmpty) then "INSERT INTO single_statement VALUES ($x); INSERT INTO single_statement VALUES (2)" else "INSERT INTO single_statement VALUES (1); INSERT INTO single_statement VALUES (2)"
                Expect.throws (fun () -> db.Execute(sql, ?parameters = (parameters |> Option.map (fun p -> p :> seq<_>)))) "Execution rejects batches consistently"
            Expect.throws (fun () -> db.Query("SELECT 1; SELECT 2") |> ignore) "Query has same contract"
            Expect.throws (fun () -> db.Scalar("SELECT 1; SELECT 2") |> ignore) "Scalar has same contract"
            Expect.throws (fun () -> db.Execute("PRAGMA user_version=21; PRAGMA user_version=22")) "PRAGMA batches rejected"
            Expect.equal (integer db "PRAGMA user_version") 0L "Rejected PRAGMA did not execute"
            Expect.equal (integer db "SELECT count(*) FROM single_statement") 0L "Rejected insert batches did not execute"

    testCase "R02 SQLite statement boundaries respect quotes comments and trigger bodies" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE source (value TEXT)")
            db.Execute("CREATE TABLE audit (value TEXT)")
            db.Execute("CREATE TRIGGER copy_value AFTER INSERT ON source BEGIN INSERT INTO audit VALUES (NEW.value); INSERT INTO audit VALUES (CASE WHEN NEW.value = ';' THEN 'case;end' ELSE 'other' END); END;")
            db.Execute("/* leading ; */ INSERT INTO source VALUES (';'); -- trailing ;\n /* more ; */")
            Expect.equal (integer db "SELECT count(*) FROM audit") 2L "Trigger body has multiple internal statements"
            Expect.equal (db.Scalar("SELECT value FROM audit ORDER BY rowid DESC").Value.AsText()) "case;end" "CASE END is not trigger END"
            Expect.equal (db.Scalar("SELECT ';' AS \"a;b\"; ; -- empty trailing statements\n ;").Value.AsText()) ";" "Empty terminators are not executable extra statements"
            Expect.equal (db.Query("SELECT 1 AS [a;b], 2 AS `c;d`").[0].GetColumnName(1)) "c;d" "Quoted identifiers do not contain boundaries"
            db.Execute("INSERT INTO source VALUES ('it''s;quoted')")
            Expect.throws (fun () -> db.Execute("CREATE TRIGGER rejected AFTER INSERT ON audit BEGIN SELECT 1; END; INSERT INTO source VALUES ('extra')")) "Trailing statement after trigger is a batch"
            Expect.equal (integer db "SELECT count(*) FROM sqlite_master WHERE name='rejected'") 0L "No rejected trigger was created"

    testCase "raw transaction commands cannot bypass managed scopes" <| fun _ ->
        withDatabase <| fun db ->
            for sql in ["BEGIN"; "BEGIN TRANSACTION"; "COMMIT"; "END TRANSACTION"; "ROLLBACK"; "SAVEPOINT external"; "RELEASE external"; "/* comment */ BEGIN"; "\011\012BEGIN"; "\uFEFFBEGIN"] do
                Expect.throws (fun () -> db.Execute(sql)) "Use managed transaction API"

    testCase "scripts run separately and clean up an unfinished transaction" <| fun _ ->
        withDatabase <| fun db ->
            db.ExecuteScript("CREATE TABLE scripts (value); INSERT INTO scripts VALUES ('committed');")
            Expect.throws (fun () -> db.ExecuteScript("BEGIN; INSERT INTO scripts VALUES ('pending');")) "Open script transaction is an error"
            Expect.equal (integer db "SELECT count(*) FROM scripts") 1L "Pending remainder rolled back"
            Expect.throws (fun () -> db.ExecuteScript("INSERT INTO scripts VALUES ('earlier'); BEGIN; INSERT INTO scripts VALUES ('rolled back'); INSERT INTO missing_table VALUES (1);")) "Script SQL error propagates"
            Expect.equal (integer db "SELECT count(*) FROM scripts") 2L "Earlier committed statement remains but pending transaction rolls back"
            db.WithTransaction(fun () -> db.Execute("INSERT INTO scripts VALUES ('usable')"))
            Expect.equal (integer db "SELECT count(*) FROM scripts") 3L "Connection remains usable"

    testCase "foreign keys enabled and owned closure is idempotent" <| fun _ ->
        let db = Sqlite.OpenInMemory()
        Expect.equal (integer db "PRAGMA foreign_keys") 1L "Foreign keys enabled"
        db.Execute("CREATE TABLE parent (id INTEGER PRIMARY KEY)")
        db.Execute("CREATE TABLE child (parent_id REFERENCES parent(id))")
        Expect.throws (fun () -> db.Execute("INSERT INTO child VALUES (99)")) "Foreign keys actually enforce relationships"
        db.Close()
        db.Close()
        Expect.throws (fun () -> db.Query("SELECT 1") |> ignore) "Closed connection cannot query"
        Expect.throws (fun () -> db.BeginTransaction() |> ignore) "Closed connection cannot transact"

#if !FABLE_COMPILER
    testCase "borrowed .NET connections stay open and restore foreign keys" <| fun _ ->
        use native = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
        native.Open()
        let raw sql =
            use command = native.CreateCommand()
            command.CommandText <- sql
            command.ExecuteScalar() |> Convert.ToInt64
        raw "PRAGMA foreign_keys=OFF" |> ignore
        let db = Sqlite.WrapConnection(native)
        Expect.equal (integer db "PRAGMA foreign_keys") 1L "Wrapper enables enforcement"
        db.Close()
        Expect.equal (raw "PRAGMA foreign_keys") 0L "Borrowed setting restored"
        Expect.equal (raw "SELECT 7") 7L "Borrowed handle remains usable"
        native.Close()
        Expect.throws (fun () -> Sqlite.WrapConnection(native) |> ignore) "Closed native handle rejected"

    testCase "R01 wrapping an active .NET transaction does not commit it" <| fun _ ->
        use native = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
        native.Open()
        use command = native.CreateCommand()
        command.CommandText <- "CREATE TABLE external_transaction (value)"
        command.ExecuteNonQuery() |> ignore
        use transaction = native.BeginTransaction()
        command.Transaction <- transaction
        command.CommandText <- "INSERT INTO external_transaction VALUES (1)"
        command.ExecuteNonQuery() |> ignore
        Expect.throws (fun () -> Sqlite.WrapConnection(native) |> ignore) "Active external transaction rejected"
        transaction.Rollback()
        command.Transaction <- null
        command.CommandText <- "SELECT count(*) FROM external_transaction"
        Expect.equal (command.ExecuteScalar() |> Convert.ToInt64) 0L "Wrapper did not commit pending work"

    testCase "borrowed release does not roll back unexpected external work" <| fun _ ->
        use native = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
        native.Open()
        let db = Sqlite.WrapConnection(native)
        db.Execute("CREATE TABLE external_release (value)")
        use command = native.CreateCommand()
        command.CommandText <- "BEGIN; INSERT INTO external_release VALUES (1)"
        command.ExecuteNonQuery() |> ignore
        Expect.throws (fun () -> db.Close()) "Exclusive-use violation must not lose caller work"
        command.CommandText <- "SELECT count(*) FROM external_release"
        Expect.equal (command.ExecuteScalar() |> Convert.ToInt64) 1L "Unexpected external work still pending"
        command.CommandText <- "ROLLBACK"
        command.ExecuteNonQuery() |> ignore
        db.Close()
        command.CommandText <- "SELECT count(*) FROM external_release"
        Expect.equal (command.ExecuteScalar() |> Convert.ToInt64) 0L "Caller can roll back and then release wrapper"
#endif
]
