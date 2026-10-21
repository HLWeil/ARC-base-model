module ARCManagement.Tests.CoreSql

open ARCManagement.Helper

open System.Globalization
open Fable.Pyxpecto
open PolyglotSQLite
open ARCManagement.Internal

let private fixtures = "schemas/sql/"
let private withCore action =
    let database = Store.openDatabase ":memory:"
    try
        database.Connection.ExecuteScript(Path.readFileText(fixtures + "001_core.sql"))
        database.Connection.ExecuteScript(Path.readFileText(fixtures + "seed_example.sql"))
        action database.Connection
    finally database.Close()

let private text (row: SqlRow) name = row.GetByName(name).AsText()
let private integer (row: SqlRow) name = row.GetByName(name).AsInteger()
let private optionalText (row: SqlRow) name = let value = row.GetByName(name) in if value.IsNull then "" else value.AsText()

/// Lower case with whitespace runs reduced to one space.
let private normalize (value: string) =
    let builder = System.Text.StringBuilder()
    let mutable pending = false
    for c in value.ToLowerInvariant() do
        if c = ' ' || c = '\n' || c = '\r' || c = '\t' then pending <- builder.Length > 0
        else
            if pending then builder.Append(' ') |> ignore
            pending <- false
            builder.Append(c) |> ignore
    builder.ToString()

/// The text of every CHECK(...) clause in a table definition, sorted.
let private checks (definition: string) =
    let lower = definition.ToLowerInvariant()
    let result = ResizeArray<string>()
    let mutable start = lower.IndexOf("check(")
    while start >= 0 do
        let mutable depth = 0
        let mutable index = start + 5
        let mutable finished = false
        while not finished do
            if lower[index] = '(' then depth <- depth + 1
            elif lower[index] = ')' then
                depth <- depth - 1
                finished <- depth = 0
            index <- index + 1
        result.Add(normalize (lower.Substring(start, index - start)))
        start <- lower.IndexOf("check(", index)
    result |> Seq.sort |> List.ofSeq

let private columns (db: SqliteConnection) table =
    db.Query("PRAGMA table_info(" + table + ")")
    |> Seq.map (fun row -> text row "name", text row "type", integer row "notnull", optionalText row "dflt_value", integer row "pk")
    |> List.ofSeq

let private foreignKeys (db: SqliteConnection) table =
    db.Query("PRAGMA foreign_key_list(" + table + ")")
    |> Seq.groupBy (fun row -> integer row "id")
    |> Seq.map (fun (_, rows) ->
        let rows = rows |> Seq.sortBy (fun row -> integer row "seq") |> List.ofSeq
        text rows.Head "table", rows |> List.map (fun row -> text row "from", text row "to"), text rows.Head "on_delete", text rows.Head "on_update")
    |> List.ofSeq

let private indexes (db: SqliteConnection) table =
    db.Query("PRAGMA index_list(" + table + ")")
    |> Seq.filter (fun row -> text row "origin" = "c")
    |> Seq.map (fun row ->
        db.Query("PRAGMA index_info(" + text row "name" + ")")
        |> Seq.sortBy (fun column -> integer column "seqno")
        |> Seq.map (fun column -> text column "name")
        |> List.ofSeq)
    |> List.ofSeq

let private definitions (db: SqliteConnection) kind =
    db.Query("SELECT name,sql FROM sqlite_master WHERE type=$kind ORDER BY name", [SqlParameter("kind", SqlValue.Text kind)])
    |> Seq.map (fun row -> text row "name", text row "sql")
    |> List.ofSeq

let private cases =
    Path.readFileLines("tests/ARCManagement.Tests/CoreSql.cases.tsv")
    |> Array.filter (fun line -> line <> "")
    |> Array.map (fun line ->
        let fields = line.Split('\t')
        if fields.Length <> 3 then failwith ("Invalid core SQL case: " + line)
        let kind, statement, expected = fields[0], fields[1], fields[2]
        testCase (kind + ": " + statement) (fun _ -> withCore (fun sql ->
            match kind with
            | "crud" ->
                let statements = statement.Split(';')
                for command in statements[0..statements.Length-2] do
                    sql.Execute(command) |> ignore
                Expect.equal (sql.Scalar(statements[statements.Length-1]) |> Option.get |> fun value -> value.AsText()) expected "Entity and association CRUD round trip"
                Store.validateCore sql
            | "reject" -> Expect.throws (fun () -> sql.Execute(statement) |> ignore) "DDL rejects invalid shape"
            | "accept" -> sql.Execute(statement) |> ignore; Store.validateCore sql
            | "invalid" ->
                sql.Execute(statement) |> ignore
                Expect.throws (fun () -> Store.validateCore sql) "Completed collections and profile declarations are validated"
            | _ ->
                let value = sql.Scalar(statement) |> Option.get
                match kind with
                | "integer" -> Expect.equal (value.AsInteger()) (int64 expected) "Integer value"
                | "real" -> Expect.equal (value.AsReal()) (System.Double.Parse(expected,CultureInfo.InvariantCulture)) "Native numeric value"
                | "text" -> Expect.equal (value.AsText()) expected "Text value"
                | "null" -> Expect.isTrue value.IsNull "Absent value"
                | _ -> failwith ("Unknown case kind: " + kind))))

let tests = testList "Core SQL profiles" [
    yield! cases
    testCase "all thirteen entity discriminators are enforced" (fun _ -> withCore (fun sql ->
        for table, discriminator in [
            "annotation","Annotation"; "agent","Agent"; "organization","Organization"
            "scholarly_article","ScholarlyArticle"; "data","Data"; "sample","Sample"
            "process","Process"; "recipe","Recipe"; "descriptor","Descriptor"
            "dataset","Dataset"; "formal_parameter","FormalParameter"
            "defined_term","DefinedTerm"; "defined_term_set","DefinedTermSet"
        ] do
            Expect.equal (sql.Scalar("SELECT type FROM " + table + " LIMIT 1") |> Option.get |> fun v -> v.AsText()) discriminator "Normative discriminator"
            Expect.throws (fun () -> sql.Execute("UPDATE " + table + " SET type='Wrong'") |> ignore) "No legacy discriminator"))
    testCase "property table storage matches the core SQL profile" (fun _ -> withCore (fun sql ->
        let hasColumn table column =
            (sql.Scalar("SELECT count(*) FROM pragma_table_info('" + table + "') WHERE name='" + column + "'") |> Option.get |> fun v -> v.AsInteger()) = 1L
        for spec in Schema.entities do
            for column in ["id"; "type"] do
                Expect.isTrue (hasColumn spec.Table column) (spec.Table + "." + column)
            for property in spec.Properties do
                let name = spec.Kind + "." + property.Name
                let expectColumn table column = Expect.isTrue (hasColumn table column) (name + " column " + table + "." + column)
                match property.Storage with
                | Schema.Storage.Column column -> expectColumn spec.Table column
                | Schema.Storage.TextOrNumberColumns(text, number) -> expectColumn spec.Table text; expectColumn spec.Table number
                | Schema.Storage.TextOrLinkColumns(text, link) -> expectColumn spec.Table text; expectColumn spec.Table link
                | Schema.Storage.SampleOrDataColumns(sample, data) -> expectColumn spec.Table sample; expectColumn spec.Table data
                | Schema.Storage.Association(table, valueColumn) ->
                    expectColumn table (spec.Table + "_id")
                    expectColumn table "position"
                    expectColumn table valueColumn
                | Schema.Storage.Endpoint direction ->
                    Expect.isTrue (direction = "input" || direction = "output") (name + " direction")
                    expectColumn "process_io" "process_id"
                    expectColumn "process_io" "direction"))
    testCase "deferred defaults and ordinary statements use existing PolyglotSQLite transactions" (fun _ -> withCore (fun sql ->
        sql.WithTransaction(fun () ->
            sql.Execute("INSERT INTO formal_parameter(id,type,default_value_id) VALUES('p','FormalParameter','a')") |> ignore
            sql.Execute("INSERT INTO annotation(id,type,name,instance_of_id) VALUES('a','Annotation','Default','p')") |> ignore
            Store.validateCore sql)
        Expect.throws (fun () -> sql.WithTransaction(fun () ->
            sql.Execute("INSERT INTO sample(id,type,name) VALUES('rollback','Sample','Rollback')") |> ignore
            invalidOp "Injected failure")) "Transaction rolled back"
        Expect.equal (sql.Scalar("SELECT count(*) FROM sample WHERE id='rollback'") |> Option.get |> fun v -> v.AsInteger()) 0L "Statement did not commit surrounding transaction"))
    testCase "scoped core tables conform to the normative profile" (fun _ ->
        let core = Store.openDatabase ":memory:"
        let scoped = Store.openDatabase ":memory:"
        try
            core.Connection.ExecuteScript(Path.readFileText(fixtures + "001_core.sql"))
            Store.initialize scoped.Connection
            Store.validateRepository scoped.Connection
            let coreTables = definitions core.Connection "table"
            let scopedTables = definitions scoped.Connection "table" |> Map.ofList
            Expect.equal coreTables.Length 52 "Core table inventory"
            for table, coreSql in coreTables do
                Expect.isTrue (Map.containsKey table scopedTables) ("Scoped table " + table)
                // Columns, types, nullability, defaults and primary key.
                let scopedColumns = columns scoped.Connection table
                let coreColumns = columns core.Connection table
                match scopedColumns with
                | (name, typ, notNull, _, key) :: rest ->
                    Expect.equal (name, typ, notNull) ("arc_id", "TEXT", 1L) (table + " scope column")
                    Expect.isTrue (key > 0L) (table + " scope in key")
                    Expect.equal (rest |> List.map (fun (n, t, nn, d, _) -> n, t, nn, d)) (coreColumns |> List.map (fun (n, t, nn, d, _) -> n, t, nn, d)) (table + " columns")
                | [] -> failwith (table + " has no columns")
                let keyOf columns = columns |> List.filter (fun (_, _, _, _, key) -> key > 0L) |> List.sortBy (fun (_, _, _, _, key) -> key) |> List.map (fun (name, _, _, _, _) -> name)
                Expect.equal (keyOf scopedColumns) ("arc_id" :: keyOf coreColumns) (table + " primary key")
                // Foreign keys, with the registry reference of entity tables checked separately.
                let scopedKeys = foreignKeys scoped.Connection table
                let registry, others = scopedKeys |> List.partition (fun (target, _, _, _) -> target = "entity")
                let isEntityTable = coreColumns |> List.exists (fun (name, _, _, _, _) -> name = "type")
                if isEntityTable then Expect.equal registry [("entity", [("arc_id", "arc_id"); ("id", "id")], "CASCADE", "NO ACTION")] (table + " registry reference")
                else Expect.equal registry [] (table + " has no registry reference")
                for _, pairs, _, _ in others do Expect.equal (List.head pairs) ("arc_id", "arc_id") (table + " reference scope")
                let stripped = others |> List.map (fun (target, pairs, onDelete, onUpdate) -> target, List.tail pairs, onDelete, onUpdate) |> List.sort
                Expect.equal stripped (foreignKeys core.Connection table |> List.sort) (table + " foreign keys")
                // Indexes keep their key columns behind the scope column. The scoped schema may add single-column target indexes.
                let scopedIndexes = indexes scoped.Connection table
                for index in scopedIndexes do Expect.equal (List.head index) "arc_id" (table + " index scope")
                let strippedIndexes = scopedIndexes |> List.map List.tail
                for index in indexes core.Connection table do Expect.isTrue (List.contains index strippedIndexes) (table + " index " + String.concat "," index)
                for index in strippedIndexes do
                    if not (List.contains index (indexes core.Connection table)) then Expect.equal index.Length 1 (table + " added index")
                // CHECK constraints stay unchanged.
                Expect.equal (checks scopedTables[table]) (checks coreSql) (table + " checks")
            let views = definitions scoped.Connection "view" |> List.map fst
            Expect.equal views ["core_validation_errors"; "process_edges"] "Scoped views"
            for view in views do
                Expect.equal (columns scoped.Connection view |> List.head |> fun (name, _, _, _, _) -> name) "arc_id" (view + " scope column")
        finally
            core.Close()
            scoped.Close())
]
