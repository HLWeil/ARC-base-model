module ARCManagement.Tests.CoreSql

open ARCManagement.Helper

open System.Collections.Generic
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

/// Lower case with whitespace runs reduced to one space. String literals keep their case and spacing.
let private normalize (value: string) =
    let builder = System.Text.StringBuilder()
    let mutable pending = false
    let mutable quoted = false
    for c in value do
        if quoted then
            builder.Append(c) |> ignore
            if c = '\'' then quoted <- false
        elif c = ' ' || c = '\n' || c = '\r' || c = '\t' then pending <- builder.Length > 0
        else
            if pending then builder.Append(' ') |> ignore
            pending <- false
            if c = '\'' then quoted <- true
            builder.Append(System.Char.ToLowerInvariant c) |> ignore
    builder.ToString()

/// The text of every CHECK(...) clause in a table definition, sorted.
let private checks (definition: string) =
    let lower = normalize definition
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
        result.Add(lower.Substring(start, index - start))
        start <- lower.IndexOf("check(", index)
    result |> Seq.sort |> List.ofSeq

/// Every REFERENCES clause of a table definition as target table and the text after the column list
/// (ON DELETE action and deferral marker). The registry reference of the scoped schema is left out.
let private references (definition: string) =
    let lower = normalize definition
    let result = ResizeArray<string * string>()
    let mutable start = lower.IndexOf("references ")
    while start >= 0 do
        let opening = lower.IndexOf("(", start)
        let closing = lower.IndexOf(")", opening)
        let mutable stop = closing + 1
        while stop < lower.Length && lower[stop] <> ',' && lower[stop] <> ')' do stop <- stop + 1
        result.Add(lower.Substring(start + 11, opening - start - 11).Trim(), lower.Substring(closing + 1, stop - closing - 1).Trim())
        start <- lower.IndexOf("references ", stop)
    result |> Seq.filter (fun (target, _) -> target <> "entity") |> Seq.sort |> List.ofSeq

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
    testCase "typed schema agrees with foreign keys, nullability and coverage of the core SQL profile" (fun _ -> withCore (fun sql ->
        let tableOf kind = (Schema.tryEntity kind).Value.Table
        let target table column =
            sql.Query("SELECT \"table\" FROM pragma_foreign_key_list('" + table + "') WHERE \"from\"='" + column + "'")
            |> Seq.map (fun row -> row.Get(0).AsText()) |> List.ofSeq
        let notNull table column =
            sql.Scalar("SELECT \"notnull\" FROM pragma_table_info('" + table + "') WHERE name='" + column + "'") |> Option.get |> fun v -> v.AsInteger()
        let mapped = Dictionary<string, Set<string>>()
        let mutable associations = Set.empty
        for spec in Schema.entities do
            let mutable columns = Set.empty
            for property in spec.Properties do
                let name = spec.Kind + "." + property.Name
                match property.Storage with
                | Schema.Storage.Column column ->
                    columns <- columns.Add column
                    if property.Required then Expect.equal (notNull spec.Table column) 1L (name + " is NOT NULL")
                    match property.Shape with
                    | Schema.Shape.Link [kind] -> Expect.equal (target spec.Table column) [tableOf kind] (name + " target table")
                    | Schema.Shape.Link _ -> failwith (name + " has no single target")
                    | _ -> ()
                | Schema.Storage.TextOrNumberColumns(a, b) | Schema.Storage.TextOrLinkColumns(a, b) | Schema.Storage.SampleOrDataColumns(a, b) ->
                    columns <- columns.Add(a).Add(b)
                | Schema.Storage.Association(table, valueColumn) ->
                    associations <- associations.Add table
                    match property.Shape with
                    | Schema.Shape.Links [kind] -> Expect.equal (target table valueColumn) [tableOf kind] (name + " target table")
                    | _ -> ()
                | Schema.Storage.Endpoint _ -> associations <- associations.Add "process_io"
            mapped[spec.Table] <- columns
        for spec in Schema.entities do
            let stored =
                sql.Query("SELECT name FROM pragma_table_info('" + spec.Table + "')")
                |> Seq.map (fun row -> row.Get(0).AsText()) |> Seq.filter (fun name -> name <> "id" && name <> "type") |> Set.ofSeq
            Expect.equal mapped[spec.Table] stored (spec.Kind + " columns are all mapped")
        let coreTables = sql.Query("SELECT name FROM sqlite_master WHERE type='table'") |> Seq.map (fun row -> row.Get(0).AsText()) |> Set.ofSeq
        let entityTables = Schema.entities |> List.map (fun spec -> spec.Table) |> Set.ofList
        Expect.equal coreTables (Set.union entityTables associations) "Every core table is an entity table or a mapped association"))
    testCase "equal entity IDs in two ARCs validate and join independently" (fun _ ->
        let scoped = Store.openDatabase ":memory:"
        try
            let db = scoped.Connection
            Store.initialize db
            let run (statement: string) = db.Execute(statement) |> ignore
            // Profiles per dataset as (A, B). The datasets d2 and d4 are invalid in A, d1, d3 and d5 in B.
            let conforms = [
                "d1", ("process-provenance", "process-provenance"); "d2", ("other", "administrative"); "d3", ("process-provenance", "administrative")
                "d4", ("process-provenance", "semantic-designation"); "d5", ("administrative", "process-provenance") ]
            let seed (arc: string) (pick: string * string -> string) =
                let q (value: string) = "'" + value + "'"
                db.WithTransaction(fun () ->
                    run ("INSERT INTO arc(arc_id,root_id,cursor,revision) VALUES(" + q arc + ",'d1',0,0)")
                    let ids = ResizeArray<string>()
                    let entity (id: string) (kind: string) =
                        ids.Add id
                        run ("INSERT INTO entity(arc_id,id,type,ordinal) VALUES(" + q arc + "," + q id + "," + q kind + "," + string ids.Count + ")")
                    for sample in ["in"; "out"] do
                        entity sample "Sample"
                        run ("INSERT INTO sample(arc_id,id,type,name) VALUES(" + q arc + "," + q sample + ",'Sample','s')")
                    entity "pr" "Process"
                    run ("INSERT INTO process(arc_id,id,type,name) VALUES(" + q arc + ",'pr','Process','p')")
                    run ("INSERT INTO process_io(arc_id,process_id,direction,sample_id) VALUES(" + q arc + ",'pr','input','in')")
                    run ("INSERT INTO process_io(arc_id,process_id,direction,sample_id) VALUES(" + q arc + ",'pr','output','out')")
                    entity "ds" "Descriptor"
                    run ("INSERT INTO descriptor(arc_id,id,type,sample_id) VALUES(" + q arc + ",'ds','Descriptor','in')")
                    for id, profiles in conforms do
                        entity id "Dataset"
                        run ("INSERT INTO dataset(arc_id,id,type,title) VALUES(" + q arc + "," + q id + ",'Dataset'," + (if id = "d5" then "'t'" else "NULL") + ")")
                        run ("INSERT INTO dataset_conforms_to(arc_id,dataset_id,position,value) VALUES(" + q arc + "," + q id + ",0," + q (pick profiles) + ")")
                        if not (arc = "B" && id = "d1") then
                            run ("INSERT INTO dataset_identifier(arc_id,dataset_id,position,value) VALUES(" + q arc + "," + q id + ",0,'i')")
                        if id = "d3" then run ("INSERT INTO dataset_process(arc_id,dataset_id,position,process_id) VALUES(" + q arc + ",'d3',0,'pr')")
                        if id = "d4" then run ("INSERT INTO dataset_descriptor(arc_id,dataset_id,position,descriptor_id) VALUES(" + q arc + ",'d4',0,'ds')"))
            seed "A" fst
            seed "B" snd
            let errors =
                db.Query("SELECT arc_id,dataset_id,message FROM core_validation_errors ORDER BY arc_id,dataset_id")
                |> Seq.map (fun row -> text row "arc_id", text row "dataset_id", text row "message") |> List.ofSeq
            Expect.equal errors [
                "A", "d2", "conformsTo must declare a base profile"
                "A", "d4", "descriptors require semantic-designation"
                "B", "d1", "identifiers must be nonempty"
                "B", "d3", "processes require process-provenance"
                "B", "d5", "administrative properties require administrative" ] "Each arm fires for exactly one ARC"
            let edges =
                db.Query("SELECT arc_id,process_id,input_id,output_id FROM process_edges ORDER BY arc_id")
                |> Seq.map (fun row -> text row "arc_id", text row "process_id", text row "input_id", text row "output_id") |> List.ofSeq
            Expect.equal edges ["A", "pr", "in", "out"; "B", "pr", "in", "out"] "Each ARC owns one edge"
        finally scoped.Close())
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
                // ON DELETE actions and the deferral marker come from the normative clause text.
                Expect.equal (references scopedTables[table]) (references coreSql) (table + " reference actions")
                // CHECK constraints stay unchanged, including the case of string literals.
                Expect.equal (checks scopedTables[table]) (checks coreSql) (table + " checks")
            let views = definitions scoped.Connection "view" |> List.map fst
            Expect.equal (List.sort views) (definitions core.Connection "view" |> List.map fst |> List.sort) "Scoped views"
            for view in views do
                Expect.equal (columns scoped.Connection view |> List.head |> fun (name, _, _, _, _) -> name) "arc_id" (view + " scope column")
        finally
            core.Close()
            scoped.Close())
]
