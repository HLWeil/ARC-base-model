module ManagementPrototype.Tests.CoreSql

open System.Globalization
open Fable.Pyxpecto
open PolyglotSQLite
open ARCtrl.Internal
open ARCSession.Internal

let private fixtures = "schemas/sql/"
let private withCore action =
    let database = Store.openDatabase ":memory:"
    try
        database.Connection.ExecuteScript(Files.read(fixtures + "001_core.sql"))
        database.Connection.ExecuteScript(Files.read(fixtures + "seed_example.sql"))
        action database.Connection
    finally database.Close()

let private cases =
    Files.read("tests/ManagementPrototype.Tests/CoreSql.cases.tsv")
    |> fun text -> text.Replace("\r", "").Split('\n')
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
    testCase "deferred defaults and ordinary statements use existing PolyglotSQLite transactions" (fun _ -> withCore (fun sql ->
        sql.WithTransaction(fun () ->
            sql.Execute("INSERT INTO formal_parameter(id,type,default_value_id) VALUES('p','FormalParameter','a')") |> ignore
            sql.Execute("INSERT INTO annotation(id,type,name,instance_of_id) VALUES('a','Annotation','Default','p')") |> ignore
            Store.validateCore sql)
        Expect.throws (fun () -> sql.WithTransaction(fun () ->
            sql.Execute("INSERT INTO sample(id,type,name) VALUES('rollback','Sample','Rollback')") |> ignore
            invalidOp "Injected failure")) "Transaction rolled back"
        Expect.equal (sql.Scalar("SELECT count(*) FROM sample WHERE id='rollback'") |> Option.get |> fun v -> v.AsInteger()) 0L "Statement did not commit surrounding transaction"))
]
