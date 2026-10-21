module ARCManagement.Tests.Safety

open System
open ARCBaseModel
open ARCManagement
open ARCManagement.Internal
open Fable.Pyxpecto
open PolyglotSQLite

let private root () = Dataset(["process-provenance"],["root"],id="root")
let private rev (graph:ManagedGraph) = ARCManagement.Internal.Store.revision graph.Context.Repository.Connection graph.ArcId
let private count (db:SqliteConnection) table = (db.Scalar("SELECT count(*) FROM "+table) |> Option.get).AsInteger()

let tests = testList "Mutation safety regressions" [
    testCase "external iterators cannot reenter a managed command" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let before = rev graph
        let subject = graph.Model
        let source = seq {
            graph.Dataset.setTitle(subject,"nested write") |> ignore
            yield "extra"
        }
        Expect.throws (fun () -> graph.Dataset.setAdditionalTypes(subject,source) |> ignore) "Nested write rejected under gate"
        Expect.isNone graph.Model.Title "Iterator could not commit"
        Expect.equal (rev graph) before "Outer failure unchanged"
        Expect.isFalse graph.History.CanUndo "No hidden operation")

    testCase "provider failure rolls back revision values history and journal" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(Sample("before",id="s"))
        let before = rev graph
        let db = session.Repository.Connection
        let history,journal = count db "history",count db "journal"
        db.ExecuteScript("CREATE TRIGGER reject_name BEFORE INSERT ON entity_value WHEN NEW.property='name' AND NEW.value_text='blocked' BEGIN SELECT RAISE(ABORT,'blocked'); END;")
        Expect.throws (fun () -> graph.Sample.setName(graph.Sample.get("s"),"blocked") |> ignore) "Injected provider error"
        Expect.equal (graph.Sample.get("s")).Name "before" "Value rolled back"
        Expect.equal (rev graph) before "Revision rolled back"
        Expect.equal (count db "history") history "History rolled back"
        Expect.equal (count db "journal") journal "Journal rolled back"
        db.Execute("DROP TRIGGER reject_name")
        Expect.equal (graph.Sample.setName(graph.Sample.get("s"),"after")).Value.Name "after" "Graph usable after rollback")

    testCase "revision exhaustion is exact and never overflows into REAL" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let db = session.Repository.Connection
        db.Execute("UPDATE session SET revision=$revision",[SqlParameter("revision",SqlValue.Integer(Int64.MaxValue))])
        let before = rev graph
        Expect.equal before Int64.MaxValue "Internal revision retains exact int64"
        Expect.throws (fun () -> graph.Dataset.setTitle(graph.Dataset.get("root"),"overflow") |> ignore) "Revision exhausted"
        Expect.isNone graph.Model.Title "No edit"
        let storageType = (db.Scalar("SELECT typeof(revision) FROM session") |> Option.get).AsText()
        Expect.equal storageType "integer" "No REAL promotion")

    testCase "history commands act on current history without bookkeeping" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let first = graph.Dataset.setTitle(graph.Dataset.get("root"),"first")
        let second = graph.Dataset.setTitle(graph.Dataset.get("root"),"second")
        Expect.equal graph.Model.Title (Some "second") "Latest state retained"
        Expect.equal second.Operation.Value.Action "apply" "Apply metadata"
        let undone = graph.History.undo()
        Expect.equal undone.Operation.Value.Action "undo" "Undo metadata"
        Expect.equal graph.Model.Title (Some "first") "Current undo works"
        let redone = graph.History.redo()
        Expect.equal redone.Operation.Value.Action "redo" "Redo metadata")

    testCase "surviving extension references block cascade without data removal" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(Sample("target",id="s"))
        let linked = graph.Entity.setObjectProperty(graph.Entity.get("root"),"sample",graph.Sample.get("s"))
        Expect.throws (fun () -> graph.Sample.delete(graph.Sample.get("s"),cascade=true) |> ignore) "Explicit extension edit required"
        Expect.isTrue (graph.Entity.hasProperty(graph.Model,"sample")) "Extension data retained"
        let removed = graph.Entity.removeProperty(graph.Entity.get("root"),"sample")
        graph.Sample.delete(graph.Sample.get("s"),cascade=true) |> ignore
        Expect.isNone (graph.Sample.read("s")) "Explicit removal permits delete")

    testCase "changing copied IDs inserts while active type collisions fail" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let original = graph.Sample.add(Sample("A",id="s"))
        let copied = Sample.withId(original.Value,"other")
        let inserted = graph.Sample.upsert(copied)
        Expect.equal (graph.Sample.list()).Count 2 "Changing ID did not rename"
        Expect.equal (graph.Sample.get("s")).Name "A" "Original retained"
        Expect.throws (fun () -> graph.Data.upsert(Data("file",id="s")) |> ignore) "Active kind fixed")
]
