module PolyglotSQLite.Tests.Repository

open PolyglotSQLite
open Fable.Pyxpecto
open PolyglotSQLite.Tests.Drivers

let private encode (group: string, id: int64, name: string) =
    ResizeArray [SqlValue.Text(group); SqlValue.Integer(id); SqlValue.Text(name)]

let private decode (row: SqlRow) =
    row.Get(0).AsText(), row.Get(1).AsInteger(), row.Get(2).AsText()

let tests = testList "generic repositories" [
    testCase "R10 composite-key CRUD quotes every identifier and binds values" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE \"order details\" (\"group\" TEXT, \"key part\" INTEGER, \"a\"\"b\" TEXT, PRIMARY KEY (\"group\", \"key part\"))")
            let table = Table("order details", ["group"; "key part"; "a\"b"], ["group"; "key part"], encode, decode)
            let repository = TableRepository(db, table)
            repository.Insert("b", 2L, "last")
            repository.Insert("a", 2L, "second")
            repository.Insert("a", 1L, "first'; DROP TABLE x; --")
            Suspect.sequenceEqual (repository.List()) [("a", 1L, "first'; DROP TABLE x; --"); ("a", 2L, "second"); ("b", 2L, "last")] "List ordered by declared primary key"
            let key = [SqlValue.Text("a"); SqlValue.Integer(1L)]
            Expect.equal (repository.Get(key)) (Some("a", 1L, "first'; DROP TABLE x; --")) "Composite key lookup"
            repository.Update("a", 1L, "updated")
            Expect.equal (repository.Get(key)) (Some("a", 1L, "updated")) "Update excludes key fields"
            repository.Delete(key)
            Expect.isNone (repository.Get(key)) "Delete uses both key columns"
            repository.Update("missing", 0L, "ignored")
            repository.Delete([SqlValue.Text("missing"); SqlValue.Integer(0L)])
            Expect.equal (repository.List().Count) 2 "Missing update/delete are no-ops"

    testCase "R11 mutable metadata inputs cannot alter existing table behavior" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE copied (group_name TEXT, id INTEGER, name TEXT, PRIMARY KEY (group_name, id))")
            let columns = [|"group_name"; "id"; "name"|]
            let keys = [|"group_name"; "id"|]
            let table = Table("copied", columns, keys, encode, decode)
            columns.[0] <- "invalid_changed_column"
            keys.[0] <- "invalid_changed_key"
            let exposedColumns, exposedKeys = table.Columns, table.PrimaryKey
            exposedColumns.[0] <- "invalid_changed_public_column"
            exposedKeys.[0] <- "invalid_changed_public_key"
            let repository = TableRepository(db, table)
            repository.Insert("group", 1L, "name")
            Expect.equal (repository.Get([SqlValue.Text("group"); SqlValue.Integer(1L)])) (Some("group", 1L, "name")) "Table owns metadata copies"
            db.Execute("CREATE TABLE unicode_names (\"é\" TEXT, id INTEGER, \"É\" TEXT, PRIMARY KEY (\"é\", id))")
            let unicode = Table("unicode_names", ["é"; "id"; "É"], ["é"; "id"], encode, decode)
            let unicodeRepository = TableRepository(db, unicode)
            unicodeRepository.Insert("lower accent", 1L, "upper accent")
            Expect.equal (unicodeRepository.Get([SqlValue.Text("lower accent"); SqlValue.Integer(1L)])) (Some("lower accent", 1L, "upper accent")) "SQLite folds ASCII identifiers only; Unicode case variants remain distinct"

    testCase "R11 invalid table metadata rejected before SQL" <| fun _ ->
        for name, columns, keys in [
            "", ["id"], ["id"]
            "x\u0000y", ["id"], ["id"]
            "table", [], ["id"]
            "table", ["id"; "id"], ["id"]
            "table", ["id"; "ID"], ["id"]
            "table", [""], [""]
            "table", ["id"], []
            "table", ["id"], ["missing"]
            "table", ["id"], ["ID"]
            "table", ["id"], ["id"; "id"]
            "table", ["id"], ["id"; "ID"]
        ] do
            Expect.throws
                (fun () -> Table(name, columns, keys, (fun (value: int64) -> ResizeArray [SqlValue.Integer(value)]), (fun row -> row.Get(0).AsInteger())) |> ignore)
                "Malformed metadata rejected"

    testCase "R11 encoded row and key arity checked before mutation" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE shape (id INTEGER PRIMARY KEY, name TEXT)")
            let wrong = Table("shape", ["id"; "name"], ["id"], (fun (id: int64) -> ResizeArray [SqlValue.Integer(id)]), (fun row -> row.Get(0).AsInteger()))
            let repository = TableRepository(db, wrong)
            Expect.throws (fun () -> repository.Insert(1L)) "Incomplete encoded row rejected"
            Expect.throws (fun () -> repository.Update(1L)) "Incomplete update rejected"
            Expect.throws (fun () -> repository.Get([]) |> ignore) "Missing key rejected"
            Expect.throws (fun () -> repository.Delete([SqlValue.Integer(1L); SqlValue.Integer(2L)])) "Extra key rejected"
            Expect.equal (integer db "SELECT count(*) FROM shape") 0L "Validation preceded execution"

    testCase "key-only tables reject update and ambiguous Get rejects multiple rows" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE key_only (id INTEGER)")
            let table = Table("key_only", ["id"], ["id"], (fun value -> ResizeArray [SqlValue.Integer(value)]), (fun row -> row.Get(0).AsInteger()))
            let repository = TableRepository(db, table)
            repository.Insert(1L)
            Expect.throws (fun () -> repository.Update(1L)) "No columns are updatable"
            repository.Insert(1L)
            Expect.throws (fun () -> repository.Get([SqlValue.Integer(1L)]) |> ignore) "Malformed schema cannot turn Get into arbitrary row selection"

    testCase "nullable composite keys remain addressable and duplicate NULL keys are ambiguous" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE nullable_keys (part TEXT, ordinal INTEGER, value TEXT, PRIMARY KEY (part, ordinal))")
            let table = Table("nullable_keys", ["part"; "ordinal"; "value"], ["part"; "ordinal"],
                              (fun (row: ResizeArray<SqlValue>) -> row),
                              (fun row -> ResizeArray [row.Get(0); row.Get(1); row.Get(2)]))
            let repository = TableRepository(db, table)
            let key = [SqlValue.Null(); SqlValue.Integer(1L)]
            let makeRow value = ResizeArray [SqlValue.Null(); SqlValue.Integer(1L); SqlValue.Text(value)]
            repository.Insert(makeRow "initial")
            let found = repository.Get(key).Value
            Expect.isTrue found.[0].IsNull "SQLite-allowed NULL key preserved"
            Expect.equal (found.[2].AsText()) "initial" "NULL key lookup uses null-safe equality"
            repository.Update(makeRow "updated")
            Expect.equal (repository.Get(key).Value.[2].AsText()) "updated" "NULL key can be updated"
            repository.Delete(key)
            Expect.isNone (repository.Get(key)) "NULL key can be deleted"
            repository.Insert(makeRow "duplicate one")
            repository.Insert(makeRow "duplicate two")
            Expect.throws (fun () -> repository.Get(key) |> ignore) "SQLite permits duplicate NULL primary keys; Get must report ambiguity"

    testCase "decoder absence is rejected while missing rows and false zero empty values remain distinct" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE decoded_values (id INTEGER PRIMARY KEY)")
            let mutable decodeCalls = 0
            let absent = Table<string>("decoded_values", ["id"], ["id"],
                                       (fun _ -> ResizeArray [SqlValue.Integer(1L)]),
                                       (fun _ -> decodeCalls <- decodeCalls + 1; null))
            let repository = TableRepository(db, absent)
            let key = [SqlValue.Integer(1L)]
            Expect.isNone (repository.Get(key)) "No row is normal absence"
            Expect.equal decodeCalls 0 "Missing lookup does not invoke decoder"
            db.Execute("INSERT INTO decoded_values VALUES (1)")
            Expect.throws (fun () -> repository.Get(key) |> ignore) "Present row cannot decode to absence"
            Expect.throws (fun () -> repository.List() |> ignore) "List validates decoded rows too"
            Expect.equal decodeCalls 2 "Both operations invoked decoder"
            let checkDecoded value =
                let table = Table("decoded_values", ["id"], ["id"], (fun _ -> ResizeArray [SqlValue.Integer(1L)]), (fun _ -> value))
                let valid = TableRepository(db, table)
                Expect.equal (valid.Get(key)) (Some value) "Falsy row value is not absence"
                Expect.equal (valid.List().[0]) value "List retains falsy row value"
            checkDecoded false
            checkDecoded 0
            checkDecoded ""
]
