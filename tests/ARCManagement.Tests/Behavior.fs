module ARCManagement.Tests.Behavior

open System
open ARCBaseModel
open ARCManagement
open ARCManagement.Helper
open Fable.Pyxpecto

let private root () = Dataset(["process-provenance"], ["root"], id = "root")
let private rev (graph:ManagedGraph) = ARCManagement.Internal.Store.revision graph.Context.Repository.Connection graph.ArcId
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

    testCase "read returns an option and get rejects absence" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let missing = graph.Sample.read("missing")
        Expect.isNone missing "Missing value"
        Expect.throws (fun () -> graph.Sample.get("missing") |> ignore) "get requires existence"
        let saved = graph.Sample.add(snapshotSample "A")
        Expect.equal saved.Value.Name "A" "Insert without bookkeeping")

    testCase "focused writes return new snapshots and retain old values" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(snapshotSample "A")
        let edited = graph.Sample.setName(graph.Sample.get("s"),"B")
        Expect.equal added.Value.Name "A" "Old value immutable"
        Expect.equal edited.Value.Name "B" "Updated result"
        Expect.equal (graph.Sample.get("s")).Name "B" "Fresh SQL read"
        Expect.isTrue edited.Changed "Changed result"
        Expect.isSome edited.Operation "Recorded operation"
        graph.close()
        session.close()
        Expect.equal edited.Value.Name "B" "Snapshot survives closure")

    testCase "focused commands accept older values and preserve other stored properties" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(snapshotSample "A")
        graph.Sample.add(Sample("unrelated")) |> ignore
        graph.Sample.setAdditionalTypes(added.Value,["new type"]) |> ignore
        let edited = graph.Sample.setName(added.Value,"B")
        Expect.equal edited.Value.Name "B" "Older value identifies current stored entity"
        Expect.equal (Seq.toList edited.Value.AdditionalTypes) ["new type"] "Other properties preserved"
        Expect.equal added.Value.Name "A" "Original remains readable")

    testCase "no-op leaves revision history and redo unchanged" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let added = graph.Sample.add(snapshotSample "A")
        let changed = graph.Sample.setName(graph.Sample.get("s"),"B")
        let undone = graph.History.undo()
        let before = rev graph
        let unchanged = graph.Sample.setName(graph.Sample.get("s"),"A")
        Expect.isFalse unchanged.Changed "Explicit unchanged result"
        Expect.isNone unchanged.Operation "No operation"
        Expect.equal (rev graph) before "Revision unchanged"
        Expect.isTrue graph.History.CanRedo "No-op preserves redo"
        graph.History.redo() |> ignore
        Expect.equal (graph.Sample.get("s")).Name "B" "Redo survives")

    testCase "upsert clears collections and never changes the ARC root" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let sample = Sample("A", additionalProperties = [Annotation("v")], id = "s")
        let saved = graph.Sample.add(sample)
        let replaced = graph.Sample.upsert(snapshotSample "B",baseline=saved.Value)
        Expect.equal replaced.Value.AdditionalProperties.Count 0 "Full replacement clears"
        let standalone = graph.Dataset.upsert(Dataset(["administrative"],["standalone"],id="other"))
        Expect.equal graph.Model.Id (Some "root") "Root designation fixed"
        Expect.equal standalone.Value.Id (Some "other") "Standalone inserted"
        Expect.throws (fun () -> graph.Sample.add(snapshotSample "again") |> ignore) "Strict insertion rejects existing ID")

    testCase "nonrecursive upsert rejects changed children and grandchildren" (fun _ ->
        use session = Session.createInMemory()
        let sample = snapshotSample "A"
        let process = Process("p",input=EntityReference.Sample sample,id="p")
        let graph = session.createArc(Dataset(["process-provenance"],["root"],processes=[process],id="root"))
        let read = graph.readModel()
        let editedSample = Sample.withName(sample,"B")
        let editedProcess = Process.withInput(process,EntityReference.Sample editedSample)
        let editedRoot = Dataset.withProcesses(read,[editedProcess])
        Expect.throws (fun () -> graph.Dataset.upsert(editedRoot,baseline=read) |> ignore) "No silently discarded grandchild"
        Expect.equal (graph.Sample.get("s")).Name "A" "Rejected atomically"
        let saved = graph.Dataset.upsert(editedRoot,baseline=read,recursive=true)
        Expect.equal (graph.Sample.get("s")).Name "B" "Recursive edit applied"
        Expect.equal saved.Value.Processes[0].Name "p" "Returned complete value")

    testCase "recursive ID collisions fail without partially adopting values" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let a,b = snapshotSample "A",snapshotSample "B"
        let candidate = Dataset(["process-provenance"],["root"],id="root",processes=[Process("a",input=EntityReference.Sample a);Process("b",input=EntityReference.Sample b)])
        let before = rev graph
        Expect.throws (fun () -> graph.Dataset.upsert(candidate,baseline=graph.Model,recursive=true) |> ignore) "Conflicting definitions"
        Expect.isNone (graph.Sample.read("s")) "Nothing inserted"
        Expect.equal (rev graph) before "Revision unchanged")

    testCase "sharing and duplicates survive persistence and undo redo" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let child = Dataset(["administrative"],["child"])
        let original = graph.Model
        let saved = graph.Dataset.upsert(Dataset.withHasParts(original,[child;child]),baseline=original)
        same saved.Value.HasParts[0] saved.Value.HasParts[1]
        graph.History.undo() |> ignore
        Expect.equal graph.Model.HasParts.Count 0 "Undo exact state"
        graph.History.redo() |> ignore
        same graph.Model.HasParts[0] graph.Model.HasParts[1]
        Expect.equal saved.Value.HasParts.Count 2 "Previously returned value unaffected")

    testCase "default deletion rejects references and preserves detached children" (fun _ ->
        use session = Session.createInMemory()
        let child = Dataset(["administrative"],["child"],id="child")
        let graph = session.createArc(Dataset(["process-provenance"],["root"],hasParts=[child],id="root"))
        Expect.throws (fun () -> graph.Dataset.delete(graph.Dataset.get("child")) |> ignore) "Incoming root reference"
        Expect.throws (fun () -> graph.Dataset.delete(graph.Dataset.get("root"),cascade=true) |> ignore) "Root protected"
        graph.Dataset.setHasParts(graph.Dataset.get("root"),[]) |> ignore
        graph.Dataset.delete(graph.Dataset.get("child")) |> ignore
        Expect.isNone (graph.Dataset.read("child")) "Unreferenced target deleted")

    testCase "cascade deletes exclusive descendants but protects shared descendants" (fun _ ->
        use session = Session.createInMemory()
        let shared = Dataset(["administrative"],["shared"],id="shared")
        let exclusive = Dataset(["administrative"],["exclusive"],id="exclusive")
        let target = Dataset(["administrative"],["target"],id="target",hasParts=[shared;exclusive])
        let graph = session.createArc(Dataset(["process-provenance"],["root"],id="root",hasParts=[target;shared;target]))
        let saved = graph.Dataset.delete(graph.Dataset.get("target"),cascade=true)
        Expect.isNone (graph.Dataset.read("target")) "Target removed"
        Expect.isNone (graph.Dataset.read("exclusive")) "Exclusive descendant removed"
        Expect.isSome (graph.Dataset.read("shared")) "Shared descendant retained"
        Expect.equal graph.Model.HasParts.Count 1 "All target occurrences detached"
        graph.History.undo() |> ignore
        Expect.equal graph.Model.HasParts.Count 3 "Exact duplicate positions restored"
        same graph.Model.HasParts[0] graph.Model.HasParts[2])

    testCase "required references block cascading deletion" (fun _ ->
        use session = Session.createInMemory()
        let sample = snapshotSample "A"
        let descriptor = Descriptor(EntityReference.Sample sample,id="d")
        let graph = session.createArc(Dataset(["semantic-designation"],["root"],descriptors=[descriptor],id="root"))
        let before = rev graph
        Expect.throws (fun () -> graph.Sample.delete(graph.Sample.get("s"),cascade=true) |> ignore) "Required describes blocks"
        Expect.equal (rev graph) before "Atomic failure")

    testCase "database reopen retains standalone history and list order" (fun _ ->
        let path = dbPath()
        let session = Session.createFile(path)
        let graph = session.createArc(root())
        graph.Sample.add(snapshotSample "A") |> ignore
        graph.Sample.add(Sample("B",id="b")) |> ignore
        let arcId = graph.ArcId
        session.close()
        use reopened = Session.openFile(path)
        let restored = reopened.openArc(arcId)
        Expect.equal (restored.Sample.list() |> Seq.map (fun v -> v.Name) |> Seq.toList) ["A";"B"] "Insertion order"
        restored.History.undo() |> ignore
        Expect.isNone (restored.Sample.read("b")) "Standalone registration undone"
        restored.History.redo() |> ignore
        Expect.equal (restored.Sample.list() |> Seq.map (fun v -> v.Name) |> Seq.toList) ["A";"B"] "Redo restores ordinal")

    testCase "another connection invalidates cache and stale replacements fail" (fun _ ->
        let path = dbPath()
        use firstSession = Session.createFile(path)
        let first = firstSession.createArc(root())
        let added = first.Sample.add(snapshotSample "A")
        use secondSession = Session.openFile(path)
        let second = secondSession.openArc(first.ArcId)
        second.Sample.setName(second.Sample.get("s"),"remote") |> ignore
        Expect.equal (first.Sample.get("s")).Name "remote" "Fresh read observes SQL"
        Expect.equal added.Value.Name "A" "Old snapshot remains stable"
        try
            first.Sample.upsert(Sample.withName(added.Value,"lost"),baseline=added.Value) |> ignore
            failwith "Missing snapshot conflict"
        with :? SnapshotConflictException as error -> Expect.equal error.EntityId "s" "Conflicting entity identified"
        let focused = first.Sample.setAdditionalTypes(added.Value,["new type"])
        Expect.equal focused.Value.Name "remote" "Focused edit preserves remote fields")

    testCase "full replacements require a baseline and ignore unrelated edits" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let original = graph.Sample.add(snapshotSample "A").Value
        let edited = Sample.withName(original,"B")
        Expect.throws (fun () -> graph.Sample.upsert(edited) |> ignore) "Baseline required"
        graph.Dataset.setTitle(graph.Model,"unrelated") |> ignore
        let saved = graph.Sample.upsert(edited,baseline=original)
        Expect.equal saved.Value.Name "B" "Unrelated change does not invalidate baseline"
        Expect.throws (fun () -> graph.Sample.upsert(edited,baseline=Sample("wrong",id="other")) |> ignore) "Baseline ID must match"
        Expect.throws (fun () -> graph.Sample.upsert(original,baseline=original) |> ignore) "Stale replacement rejected even if it equals its baseline"
        let unchanged = graph.Sample.upsert(saved.Value,baseline=saved.Value)
        Expect.isFalse unchanged.Changed "Current baseline allows no-op"
        graph.Sample.delete(saved.Value) |> ignore
        Expect.throws (fun () -> graph.Sample.upsert(edited,baseline=saved.Value) |> ignore) "Deleted baseline cannot resurrect entity")

    testCase "focused subjects require IDs and reject known foreign ARC values" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let other = session.createArc(root())
        let original = graph.Sample.add(snapshotSample "A").Value
        other.Sample.add(snapshotSample "other") |> ignore
        Expect.throws (fun () -> graph.Sample.setName(Sample("detached"),"bad") |> ignore) "Subject ID required"
        Expect.throws (fun () -> other.Sample.setName(original,"bad") |> ignore) "Known foreign ARC rejected"
        Expect.throws (fun () -> other.Sample.upsert(original,baseline=original) |> ignore) "Known foreign baseline rejected"
        Expect.throws (fun () -> graph.Sample.setName(Sample("missing",id="missing"),"bad") |> ignore) "Unknown subject rejected"
        Expect.equal (other.Sample.get("s")).Name "other" "Foreign target preserved")

    testCase "recursive baselines protect edited descendants" (fun _ ->
        use session = Session.createInMemory()
        let leaf = snapshotSample "A"
        let process = Process("p",id="p",input=EntityReference.Sample leaf)
        let graph = session.createArc(Dataset(["process-provenance"],["root"],id="root",processes=[process]))
        let original = graph.Model
        graph.Sample.setAdditionalTypes(graph.Sample.get("s"),["remote"]) |> ignore
        let changedLeaf = Sample.withName(leaf,"B")
        let edited = Dataset.withProcesses(original,[Process.withInput(original.Processes[0],EntityReference.Sample changedLeaf)])
        Expect.throws (fun () -> graph.Dataset.upsert(edited,baseline=original,recursive=true) |> ignore) "Changed descendant baseline rejected"
        Expect.equal (graph.Sample.get("s")).Name "A" "No partial write"
        Expect.equal (Seq.toList (graph.Sample.get("s")).AdditionalTypes) ["remote"] "Remote change preserved")

    testCase "recursive baselines protect extension-only targets" (fun _ ->
        use session = Session.createInMemory()
        let leaf = snapshotSample "A"
        let source = Dataset.withEntityProperty(root(),"sample",Entity.Object leaf)
        let graph = session.createArc(source)
        let original = graph.Model
        graph.Sample.setName(graph.Sample.get("s"),"remote") |> ignore
        let edited = Dataset.withEntityProperty(original,"sample",Entity.Object(Sample.withName(leaf,"local")))
        try
            graph.Dataset.upsert(edited,baseline=original,recursive=true) |> ignore
            failwith "Missing extension target baseline conflict"
        with :? SnapshotConflictException as error -> Expect.equal error.EntityId "s" "Extension target checked"
        Expect.equal (graph.Sample.get("s")).Name "remote" "Extension target preserved"
        let current = graph.Model
        let saved = graph.Dataset.upsert(edited,baseline=current,recursive=true)
        Expect.isTrue saved.Changed "Fresh extension baseline permits replacement"
        Expect.equal (graph.Sample.get("s")).Name "local" "Extension target replaced")

    testCase "recursive replacements require baselines for newly attached existing entities" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let standalone = graph.Process.add(Process("outside",id="outside")).Value
        let original = graph.Model
        let edited = Dataset.withProcesses(original,[Process.withName(standalone,"changed")])
        Expect.throws (fun () -> graph.Dataset.upsert(edited,baseline=original,recursive=true) |> ignore) "Existing definition missing from baseline"
        Expect.equal (graph.Process.get("outside")).Name "outside" "No unrelated overwrite"
        let linked = graph.Dataset.upsert(Dataset.withProcesses(original,[standalone]),baseline=original)
        Expect.equal linked.Value.Processes[0].Name "outside" "Unchanged existing reference can be attached without recursion")

    testCase "unrelated database is rejected without initialization" (fun _ ->
        let path = dbPath()
        use connection = PolyglotSQLite.Sqlite.OpenFile(path)
        connection.Execute("CREATE TABLE unrelated(value TEXT)")
        Expect.throws (fun () -> Session.openFile(path) |> ignore) "Required schema missing"
        Expect.equal (connection.Query("SELECT name FROM sqlite_master WHERE type='table'")).Count 1 "No tables added")
]
