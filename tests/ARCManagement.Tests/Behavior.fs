module ARCManagement.Tests.Behavior

open System
open ARCBaseModel
open ARCManagement
open ARCManagement.Helper
open Fable.Pyxpecto

let private root () = Dataset(["process-provenance"], ["root"], id = "root")
let private rev (graph: ManagedGraph) = graph.readModel().Revision
let private same a b = Expect.isTrue (obj.ReferenceEquals(a,b)) "Shared reference"
let private snapshotSample name = Sample(name, id = "s")
let private dbPath () = Path.combineNative (Helpers.folder "immutable") "test.sqlite"

let tests = testList "Immutable management" [
    testCase "creation assigns IDs to persisted copies only" (fun _ ->
        use session = Session.createInMemory()
        let sample = Sample("leaf")
        let source = Dataset(["process-provenance"], ["root"], processes = [Process("p", input = EntityReference.Sample sample)])
        let graph = session.createArc(source)
        Expect.isNone source.Id "Detached root unchanged"
        Expect.isNone sample.Id "Detached child unchanged"
        Expect.isSome graph.Model.Id "Persisted root assigned"
        Expect.isSome graph.Model.Processes[0].Input "Persisted relationship"
        same (session.openArc(graph.ArcId)) graph
        Expect.isFalse graph.History.CanUndo "Initialization is not an edit")

    testCase "read missing values retains revision and get rejects absence" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let missing = graph.Sample.read("missing")
        Expect.isNone missing.Value "Missing value"
        Expect.throws (fun () -> graph.Sample.get("missing") |> ignore) "get requires existence"
        let saved = graph.Sample.add(missing.Revision, snapshotSample "A")
        Expect.equal saved.Value.Name "A" "Insert using observed revision")

    testCase "focused writes return new snapshots and retain old values" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(rev graph, snapshotSample "A")
        let edited = graph.Sample.setName(added.Revision, "s", "B")
        Expect.equal added.Value.Name "A" "Old value immutable"
        Expect.equal edited.Value.Name "B" "Updated result"
        Expect.equal (graph.Sample.get("s")).Name "B" "Fresh SQL read"
        Expect.isTrue edited.Changed "Changed result"
        Expect.isSome edited.Operation "Recorded operation"
        graph.close()
        session.close()
        Expect.equal edited.Value.Name "B" "Snapshot survives closure")

    testCase "stale sequential writes and wrong ARC tokens fail" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let other = session.createArc(root())
        let added = graph.Sample.add(rev graph, snapshotSample "A")
        let edited = graph.Sample.setName(added.Revision,"s","B")
        Expect.throws (fun () -> graph.Sample.setName(added.Revision,"s","lost") |> ignore) "Stale token"
        Expect.throws (fun () -> other.Sample.add(edited.Revision,Sample("foreign")) |> ignore) "Wrong ARC"
        Expect.equal (graph.Sample.get("s")).Name "B" "No overwritten changes")

    testCase "no-op leaves revision history and redo unchanged" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(rev graph,snapshotSample "A")
        let changed = graph.Sample.setName(added.Revision,"s","B")
        let undone = graph.History.undo(changed.Revision)
        let unchanged = graph.Sample.setName(undone.Revision,"s","A")
        Expect.isFalse unchanged.Changed "Explicit unchanged result"
        Expect.isNone unchanged.Operation "No operation"
        Expect.equal unchanged.Revision.Value undone.Revision.Value "Revision unchanged"
        Expect.isTrue graph.History.CanRedo "No-op preserves redo"
        graph.History.redo(unchanged.Revision) |> ignore
        Expect.equal (graph.Sample.get("s")).Name "B" "Redo survives")

    testCase "upsert clears collections and never changes the ARC root" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let sample = Sample("A", additionalProperties = [Annotation("v")], id = "s")
        let saved = graph.Sample.add(rev graph,sample)
        let replaced = graph.Sample.upsert(saved.Revision,snapshotSample "B")
        Expect.equal replaced.Value.AdditionalProperties.Count 0 "Full replacement clears"
        let standalone = graph.Dataset.upsert(replaced.Revision,Dataset(["administrative"],["standalone"],id="other"))
        Expect.equal graph.Model.Id (Some "root") "Root designation fixed"
        Expect.equal standalone.Value.Id (Some "other") "Standalone inserted"
        Expect.throws (fun () -> graph.Sample.add(standalone.Revision,snapshotSample "again") |> ignore) "Strict insertion rejects existing ID")

    testCase "nonrecursive upsert rejects changed children and grandchildren" (fun _ ->
        use session = Session.createInMemory()
        let sample = snapshotSample "A"
        let process = Process("p",input=EntityReference.Sample sample,id="p")
        let graph = session.createArc(Dataset(["process-provenance"],["root"],processes=[process],id="root"))
        let read = graph.readModel()
        let editedSample = Sample.withName(sample,"B")
        let editedProcess = Process.withInput(process,EntityReference.Sample editedSample)
        let editedRoot = Dataset.withProcesses(read.Value,[editedProcess])
        Expect.throws (fun () -> graph.Dataset.upsert(read.Revision,editedRoot) |> ignore) "No silently discarded grandchild"
        Expect.equal (graph.Sample.get("s")).Name "A" "Rejected atomically"
        let saved = graph.Dataset.upsert(read.Revision,editedRoot,recursive=true)
        Expect.equal (graph.Sample.get("s")).Name "B" "Recursive edit applied"
        Expect.equal saved.Value.Processes[0].Name "p" "Returned complete value")

    testCase "recursive ID collisions fail without partially adopting values" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let a,b = snapshotSample "A",snapshotSample "B"
        let candidate = Dataset(["process-provenance"],["root"],id="root",processes=[Process("a",input=EntityReference.Sample a);Process("b",input=EntityReference.Sample b)])
        let before = rev graph
        Expect.throws (fun () -> graph.Dataset.upsert(before,candidate,recursive=true) |> ignore) "Conflicting definitions"
        Expect.isNone (graph.Sample.read("s")).Value "Nothing inserted"
        Expect.equal (rev graph).Value before.Value "Revision unchanged")

    testCase "sharing and duplicates survive persistence and undo redo" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let child = Dataset(["administrative"],["child"])
        let saved = graph.Dataset.upsert(rev graph,Dataset.withHasParts(graph.Model,[child;child]))
        same saved.Value.HasParts[0] saved.Value.HasParts[1]
        graph.History.undo(saved.Revision) |> ignore
        Expect.equal graph.Model.HasParts.Count 0 "Undo exact state"
        graph.History.redo(rev graph) |> ignore
        same graph.Model.HasParts[0] graph.Model.HasParts[1]
        Expect.equal saved.Value.HasParts.Count 2 "Previously returned value unaffected")

    testCase "default deletion rejects references and preserves detached children" (fun _ ->
        use session = Session.createInMemory()
        let child = Dataset(["administrative"],["child"],id="child")
        let graph = session.createArc(Dataset(["process-provenance"],["root"],hasParts=[child],id="root"))
        Expect.throws (fun () -> graph.Dataset.delete(rev graph,"child") |> ignore) "Incoming root reference"
        Expect.throws (fun () -> graph.Dataset.delete(rev graph,"root",cascade=true) |> ignore) "Root protected"
        graph.Dataset.setHasParts(rev graph,"root",[]) |> ignore
        graph.Dataset.delete(rev graph,"child") |> ignore
        Expect.isNone (graph.Dataset.read("child")).Value "Unreferenced target deleted")

    testCase "cascade deletes exclusive descendants but protects shared descendants" (fun _ ->
        use session = Session.createInMemory()
        let shared = Dataset(["administrative"],["shared"],id="shared")
        let exclusive = Dataset(["administrative"],["exclusive"],id="exclusive")
        let target = Dataset(["administrative"],["target"],id="target",hasParts=[shared;exclusive])
        let graph = session.createArc(Dataset(["process-provenance"],["root"],id="root",hasParts=[target;shared;target]))
        let saved = graph.Dataset.delete(rev graph,"target",cascade=true)
        Expect.isNone (graph.Dataset.read("target")).Value "Target removed"
        Expect.isNone (graph.Dataset.read("exclusive")).Value "Exclusive descendant removed"
        Expect.isSome (graph.Dataset.read("shared")).Value "Shared descendant retained"
        Expect.equal graph.Model.HasParts.Count 1 "All target occurrences detached"
        graph.History.undo(saved.Revision) |> ignore
        Expect.equal graph.Model.HasParts.Count 3 "Exact duplicate positions restored"
        same graph.Model.HasParts[0] graph.Model.HasParts[2])

    testCase "required references block cascading deletion" (fun _ ->
        use session = Session.createInMemory()
        let sample = snapshotSample "A"
        let descriptor = Descriptor(EntityReference.Sample sample,id="d")
        let graph = session.createArc(Dataset(["semantic-designation"],["root"],descriptors=[descriptor],id="root"))
        let before = rev graph
        Expect.throws (fun () -> graph.Sample.delete(before,"s",cascade=true) |> ignore) "Required describes blocks"
        Expect.equal (rev graph).Value before.Value "Atomic failure")

    testCase "database reopen retains standalone history and list order" (fun _ ->
        let path = dbPath()
        let session = Session.createFile(path)
        let graph = session.createArc(root())
        graph.Sample.add(rev graph,snapshotSample "A") |> ignore
        graph.Sample.add(rev graph,Sample("B",id="b")) |> ignore
        let arcId = graph.ArcId
        session.close()
        use reopened = Session.openFile(path)
        let restored = reopened.openArc(arcId)
        Expect.equal (restored.Sample.list() |> Seq.map (fun v -> v.Name) |> Seq.toList) ["A";"B"] "Insertion order"
        restored.History.undo(rev restored) |> ignore
        Expect.isNone (restored.Sample.read("b")).Value "Standalone registration undone"
        restored.History.redo(rev restored) |> ignore
        Expect.equal (restored.Sample.list() |> Seq.map (fun v -> v.Name) |> Seq.toList) ["A";"B"] "Redo restores ordinal")

    testCase "another connection invalidates cache and stale writes" (fun _ ->
        let path = dbPath()
        use firstSession = Session.createFile(path)
        let first = firstSession.createArc(root())
        let added = first.Sample.add(rev first,snapshotSample "A")
        use secondSession = Session.openFile(path)
        let second = secondSession.openArc(first.ArcId)
        second.Sample.setName((rev second),"s","remote") |> ignore
        Expect.equal (first.Sample.get("s")).Name "remote" "Fresh read observes SQL"
        Expect.equal added.Value.Name "A" "Old snapshot remains stable"
        Expect.throws (fun () -> first.Sample.setName(added.Revision,"s","lost") |> ignore) "Stale write rejected")

    testCase "unrelated database is rejected without initialization" (fun _ ->
        let path = dbPath()
        use connection = PolyglotSQLite.Sqlite.OpenFile(path)
        connection.Execute("CREATE TABLE unrelated(value TEXT)")
        Expect.throws (fun () -> Session.openFile(path) |> ignore) "Required schema missing"
        Expect.equal (connection.Query("SELECT name FROM sqlite_master WHERE type='table'")).Count 1 "No tables added")
]
