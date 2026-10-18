module ARCManagement.Tests.Safety

open System
open ARCBaseModel
open ARCManagement
open ARCManagement.Internal
open Fable.Pyxpecto
open PolyglotSQLite

let private root () = Dataset(["process-provenance"],["root"],id="root")
let private rev (graph:ManagedGraph) = graph.readModel().Revision
let private count (db:SqliteConnection) table = (db.Scalar("SELECT count(*) FROM "+table) |> Option.get).AsInteger()

let tests = testList "Mutation safety regressions" [
    testCase "external iterators cannot reenter a managed command" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let before = rev graph
        let source = seq {
            graph.Dataset.setTitle(before,"root","nested write") |> ignore
            yield "extra"
        }
        Expect.throws (fun () -> graph.Dataset.setAdditionalTypes(before,"root",source) |> ignore) "Nested write rejected under gate"
        Expect.isNone graph.Model.Title "Iterator could not commit"
        Expect.equal (rev graph).Value before.Value "Outer failure unchanged"
        Expect.isFalse graph.History.CanUndo "No hidden operation")

    testCase "provider failure rolls back revision values history and journal" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(rev graph,Sample("before",id="s"))
        let db = session.Repository.Connection
        let history,journal = count db "history",count db "journal"
        db.ExecuteScript("CREATE TRIGGER reject_name BEFORE INSERT ON entity_value WHEN NEW.property='name' AND NEW.value_text='blocked' BEGIN SELECT RAISE(ABORT,'blocked'); END;")
        Expect.throws (fun () -> graph.Sample.setName(added.Revision,"s","blocked") |> ignore) "Injected provider error"
        Expect.equal (graph.Sample.get("s")).Name "before" "Value rolled back"
        Expect.equal (rev graph).Value added.Revision.Value "Revision rolled back"
        Expect.equal (count db "history") history "History rolled back"
        Expect.equal (count db "journal") journal "Journal rolled back"
        db.Execute("DROP TRIGGER reject_name")
        Expect.equal (graph.Sample.setName(added.Revision,"s","after")).Value.Name "after" "Graph usable after rollback")

    testCase "revision exhaustion is exact and never overflows into REAL" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let db = session.Repository.Connection
        db.Execute("UPDATE session SET revision=$revision",[SqlParameter("revision",SqlValue.Integer(Int64.MaxValue))])
        let before = rev graph
        Expect.isTrue (before.Value.EndsWith("9223372036854775807")) "Opaque string retains exact int64"
        Expect.throws (fun () -> graph.Dataset.setTitle(before,"root","overflow") |> ignore) "Revision exhausted"
        Expect.isNone graph.Model.Title "No edit"
        let storageType = (db.Scalar("SELECT typeof(revision) FROM session") |> Option.get).AsText()
        Expect.equal storageType "integer" "No REAL promotion")

    testCase "stale history commands do not undo a later edit" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let first = graph.Dataset.setTitle(rev graph,"root","first")
        let second = graph.Dataset.setTitle(first.Revision,"root","second")
        Expect.throws (fun () -> graph.History.undo(first.Revision) |> ignore) "Stale undo"
        Expect.equal graph.Model.Title (Some "second") "Latest state retained"
        Expect.equal second.Operation.Value.Action "apply" "Apply metadata"
        let undone = graph.History.undo(second.Revision)
        Expect.equal undone.Operation.Value.Action "undo" "Undo metadata"
        Expect.equal graph.Model.Title (Some "first") "Current undo works"
        let redone = graph.History.redo(undone.Revision)
        Expect.equal redone.Operation.Value.Action "redo" "Redo metadata")

    testCase "surviving extension references block cascade without data removal" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(rev graph,Sample("target",id="s"))
        let linked = graph.Entity.setObjectProperty(added.Revision,"root","sample","s")
        Expect.throws (fun () -> graph.Sample.delete(linked.Revision,"s",cascade=true) |> ignore) "Explicit extension edit required"
        Expect.isTrue (graph.Entity.hasProperty("root","sample")) "Extension data retained"
        let removed = graph.Entity.removeProperty(linked.Revision,"root","sample")
        graph.Sample.delete(removed.Revision,"s",cascade=true) |> ignore
        Expect.isNone (graph.Sample.read("s")).Value "Explicit removal permits delete")

    testCase "changing copied IDs inserts while active type collisions fail" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let original = graph.Sample.add(rev graph,Sample("A",id="s"))
        let copied = Sample.withId(original.Value,"other")
        let inserted = graph.Sample.upsert(original.Revision,copied)
        Expect.equal (graph.Sample.list()).Count 2 "Changing ID did not rename"
        Expect.equal (graph.Sample.get("s")).Name "A" "Original retained"
        Expect.throws (fun () -> graph.Data.upsert(inserted.Revision,Data("file",id="s")) |> ignore) "Active kind fixed")
]
