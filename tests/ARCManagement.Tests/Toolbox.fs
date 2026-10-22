module ARCManagement.Tests.Toolbox

open ARCManagement.Helper

open System
open ARCBaseModel
open ARCtrl
open ARCManagement.Internal
open PolyglotSQLite
open Fable.Pyxpecto

let private folder () = Helpers.folder "toolbox"

let private root () = Dataset(["process-provenance"],["root"],id="root")
let private sql (session: ARCManagement.Session) = session.Repository.Connection
let private input (proc: ARCBaseModel.Process) =
    match proc.Input with
    | Some(EntityReference.Sample sample) -> sample
    | _ -> failwith "Expected sample input"
let private same actual expected = Expect.isTrue (Object.ReferenceEquals(actual,expected)) "Shared object reference"
let private scalar (db: SqliteConnection) statement = db.Query(statement).[0].Get(0).AsInteger()

let tests = testList "ARC toolbox" [
    testCase "memory repository isolates identical IDs and operation histories" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        Expect.isNone session.DatabasePath "Memory has no file path"
        let first,second = session.createArc(root()),session.createArc(root())
        Expect.notEqual first.ArcId second.ArcId "ARC identity is separate from root identity"
        same (session.openArc(first.ArcId)) first
        first.Sample.upsert(Sample("first",id="sample")) |> ignore
        second.Sample.upsert(Sample("second",id="sample")) |> ignore
        first.Sample.setName(first.Sample.get("sample"),"changed") |> ignore
        second.Dataset.setTitle(second.Model,Some("other")) |> ignore
        first.History.undo()
        Expect.equal (first.Sample.get("sample")).Name "first" "Independent undo"
        Expect.equal (second.Sample.get("sample")).Name "second" "Other ARC unchanged"
        Expect.equal second.Model.Title (Some "other") "Other revision remains valid"
        Expect.equal (scalar (sql session) "SELECT count(*) FROM sample") 2L "Both ID-qualified rows"
        Expect.equal (session.listArcs()).Count 2 "Both entries"
        Expect.isNone (session.listArcs()).[0].Folder "Unbound metadata")

    testCase "values read from another ARC are accepted and resolve by ID in the destination" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        let sample = first.Sample.create("sample")
        let proc = second.Process.create("process")
        Expect.throws (fun () -> second.Process.setInput(proc, Some(EntityReference.Sample sample)) |> ignore) "A target that is not stored in the destination is rejected"
        Expect.throws (fun () -> second.Entity.setProperty(second.Model, "foreign", Entity.Object(sample)) |> ignore) "An extension target must be stored too"
        let copied = second.Sample.add(sample)
        Expect.equal copied.Id sample.Id "The foreign value keeps its ID"
        let linked = second.Process.setInput(proc, Some(EntityReference.Sample sample))
        Expect.equal (input linked).Name "sample" "The link resolves to the stored copy"
        second.Entity.setProperty(second.Model, "foreign", Entity.Object(sample)) |> ignore
        Expect.equal (Seq.length (second.Sample.list())) 1 "One stored copy"
        let third = session.createArc(first.Model)
        Expect.notEqual third.ArcId first.ArcId "A managed root creates an independent entry"
        Expect.equal third.Model.Id first.Model.Id "The supplied root ID is kept"
        use otherSession = ARCManagement.Session.createInMemory()
        let other = otherSession.createArc(first.Model)
        Expect.equal (other.Sample.add(sample)).Id sample.Id "Values are accepted across repositories"
        Expect.isNone (first.Process.list() |> Seq.tryHead) "The source ARC is unaffected")

    testCase "SQL foreign keys reject cross ARC targets and typed endpoints" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        first.Sample.upsert(Sample("sample",id="foreign")) |> ignore
        let proc = second.Process.create("process")
        let db = sql session
        let parameters = [SqlParameter("arc",SqlValue.Text(second.ArcId));SqlParameter("id",SqlValue.Text(proc.Id.Value))]
        Expect.equal (scalar db "PRAGMA foreign_keys") 1L "Enforcement enabled"
        Expect.throws (fun () -> db.WithTransaction(fun () -> db.Execute("INSERT INTO entity_additional_type(arc_id,entity_id,position,value) VALUES($arc,'foreign',0,'x')",parameters))) "Foreign registry entry"
        Expect.throws (fun () -> db.WithTransaction(fun () -> db.Execute("INSERT INTO process_io(arc_id,process_id,direction,sample_id) VALUES($arc,$id,'input','foreign')",parameters))) "Foreign typed endpoint"
        Expect.throws (fun () -> db.WithTransaction(fun () -> db.Execute("INSERT INTO entity_extension(arc_id,owner_id,property,path,storage,target_id) VALUES($arc,$id,'foreign','','object','foreign')",parameters))) "Foreign extension"
        Expect.equal (db.Query("PRAGMA foreign_key_check")).Count 0 "No dangling references")

    testCase "closing ARC leaves repository usable and reopening creates a fresh projection" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        let id = first.ArcId
        let previous = first.Model
        let operations = first.Sample
        first.close()
        first.close()
        Expect.throws (fun () -> operations.create("closed") |> ignore) "Cached operations invalidated"
        second.Sample.create("still active") |> ignore
        let reopened = session.openArc(id)
        Expect.isFalse (Object.ReferenceEquals(previous,reopened.Model)) "Fresh projection"
        session.close()
        session.close()
        Expect.throws (fun () -> reopened.Model |> ignore) "All ARC handles invalidated"
        Expect.throws (fun () -> session.listArcs() |> ignore) "Closed repository")

    testCase "file reopening is lazy and retains independent histories" (fun _ ->
        let path = Path.combineNative (folder()) "collection.sqlite"
        let session = ARCManagement.Session.createFile(path)
        let first,second = session.createArc(root()),session.createArc(root())
        let firstId,secondId = first.ArcId,second.ArcId
        first.Dataset.setTitle(first.Model,Some("first")) |> ignore
        second.Dataset.setTitle(second.Model,Some("second")) |> ignore
        session.close()
        use resumed = ARCManagement.Session.openFile(path)
        Expect.equal (resumed.listArcs()).Count 2 "Metadata only"
        let reopened = resumed.openArc(firstId)
        reopened.History.undo()
        Expect.isNone reopened.Model.Title "Restored first history"
        Expect.equal (resumed.openArc(secondId)).Model.Title (Some "second") "Other ARC retained"
        // Corrupt one ARC registry entry to prove enumeration and opening another ARC are lazy.
        (sql resumed).Execute("UPDATE entity SET type='Sample' WHERE arc_id=$arc AND id='root'",[SqlParameter("arc",SqlValue.Text(secondId))])
        resumed.openArc(secondId).close()
        Expect.equal (resumed.listArcs()).Count 2 "No graph parsing during enumeration"
        same (resumed.openArc(firstId)) reopened
        Expect.throws (fun () -> resumed.openArc(secondId) |> ignore) "Invalid graph fails only when opened")

    testCase "file factories reject existing missing and unrelated databases" (fun _ ->
        let path = Path.combineNative (folder()) "collection.sqlite"
        Expect.throws (fun () -> ARCManagement.Session.openFile(path) |> ignore) "Missing file"
        Expect.isFalse (Path.pathExists path) "Open never creates"
        let session = ARCManagement.Session.createFile(path)
        session.createArc(root()) |> ignore
        session.close()
        Expect.throws (fun () -> ARCManagement.Session.createFile(path) |> ignore) "Existing file"
        use reopened = ARCManagement.Session.openFile(path)
        Expect.equal (reopened.listArcs()).Count 1 "No replacement"
        let unrelated = Path.combineNative (folder()) "unrelated.sqlite"
        use db = Sqlite.OpenFile(unrelated)
        db.Execute("CREATE TABLE unrelated(value TEXT)")
        Expect.throws (fun () -> ARCManagement.Session.openFile(unrelated) |> ignore) "Unrelated database"
        let malformed = Path.combineNative (folder()) "malformed.sqlite"
        let empty = ARCManagement.Session.createFile(malformed)
        empty.close()
        use broken = Sqlite.OpenFile(malformed)
        broken.Execute("DROP TABLE process_io")
        broken.Execute("CREATE TABLE process_io(arc_id TEXT NOT NULL,process_id TEXT NOT NULL,direction TEXT NOT NULL,sample_id TEXT,data_id TEXT,PRIMARY KEY(arc_id,process_id,direction))")
        Expect.throws (fun () -> ARCManagement.Session.openFile(malformed) |> ignore) "Correct version with missing relationship constraints rejected")

    testCase "folder binding and importing are independent of database storage" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let graph = session.createArc(root())
        Expect.throws (fun () -> ARC.fromGraph(graph) |> ignore) "Binding required"
        let path = folder()
        let first = ARC.bind(graph,path)
        Expect.isFalse (Path.pathExists(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)) "Bind does not save"
        first.save()
        Expect.isFalse first.IsDirty "Export establishes checkpoint"
        first.close()
        let reopened = ARC.fromGraph(session.openArc(first.ArcId))
        Expect.equal reopened.FolderBinding (Some path) "Binding retained"
        let other = session.createArc(root())
        Expect.throws (fun () -> ARC.bind(other,path) |> ignore) "Existing ARC requires import"
        use importedSession = ARCManagement.Session.createInMemory()
        let imported = ARC.importFolder(importedSession,path)
        let importedAgain = ARC.importFolder(importedSession,path)
        Expect.notEqual imported.ArcId importedAgain.ArcId "Each import creates a new entry"
        Expect.isFalse (Object.ReferenceEquals(imported.Model,importedAgain.Model)) "Repeated imports have independent projections"
        Expect.equal (Seq.item 0 imported.Model.Identifiers) "root" "Imported graph"
        Expect.isFalse imported.IsDirty "Import establishes baseline"
        imported.Dataset.setTitle(imported.Model,Some("native")) |> ignore
        imported.save()
        Expect.throws (fun () -> reopened.save()) "External export invalidates old binding baseline")

    testCase "failed saves retain one ARC's checkpoint without changing another" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let first = ARC.bind(session.createArc(root()),folder())
        let second = ARC.bind(session.createArc(root()),folder())
        first.save()
        second.save()
        let db = sql session
        let checkpoint = (Store.metadata db first.ArcId).GetByName("saved_graph").AsText()
        first.Dataset.setTitle(first.Model,Some("pending")) |> ignore
        db.ExecuteScript("CREATE TRIGGER fail_checkpoint BEFORE UPDATE OF baseline ON arc WHEN NEW.arc_id='" + first.ArcId + "' BEGIN SELECT RAISE(ABORT,'injected'); END;")
        Expect.throws first.save "Checkpoint commit failure"
        Expect.equal ((Store.metadata db first.ArcId).GetByName("saved_graph").AsText()) checkpoint "Previous checkpoint retained"
        Expect.isTrue first.IsDirty "Edits stay pending"
        Expect.isFalse second.IsDirty "Other checkpoint unchanged"
        db.Execute("DROP TRIGGER fail_checkpoint")
        first.recoverFolder("sql")
        first.save()
        Expect.isFalse first.IsDirty "Explicit recovery allows retry"
        second.Dataset.setTitle(second.Model,Some("other")) |> ignore
        second.History.undo()
        Expect.isFalse second.IsDirty "Other history and checkpoint remain independent")

    testCase "folder recovery archives only the displaced ARC including history" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let path = folder()
        let first = ARC.bind(session.createArc(root()),path)
        let second = session.createArc(root())
        first.Dataset.setTitle(first.Model,Some("original")) |> ignore
        first.save()
        first.Sample.create("standalone") |> ignore
        second.Dataset.setTitle(second.Model,Some("unaffected")) |> ignore
        let yaml = Path.readFileText(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)
        Path.writeFileText (Path.combineNative path ARCtrl.Helper.Layout.ARCFileName) (yaml.Replace("title: original","title: external"))
        Expect.throws (fun () -> first.recoverFolder("auto")) "Session-only objects block automatic reload"
        first.recoverFolder("yml")
        Expect.equal first.Model.Title (Some "external") "Replacement graph"
        Expect.equal (Seq.length (first.Sample.list())) 0 "Fresh registry"
        Expect.isFalse first.History.CanUndo "Fresh history"
        Expect.equal second.Model.Title (Some "unaffected") "Other ARC untouched"
        second.History.undo()
        Expect.isNone second.Model.Title "Other history intact"
        let db = sql session
        Expect.equal (scalar db "SELECT count(*) FROM arc_archive") 1L "One ARC archived"
        Expect.equal (scalar db "SELECT count(*) FROM archived_history") 2L "History retained"
        let archived = Codec.decodeState(db.Query("SELECT state FROM arc_archive").[0].Get(0).AsText())
        Expect.isTrue (archived.Entities |> List.exists (fun row -> row.Kind = "Sample")) "Standalone objects retained")

    testCase "SQL rollback in one ARC preserves both projections and histories" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        let db = sql session
        db.ExecuteScript("CREATE TRIGGER fail_sample BEFORE INSERT ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected'); END;")
        let rejected = Sample("reject")
        Expect.throws (fun () -> first.Sample.add(rejected) |> ignore) "Injected write failure"
        Expect.isNone rejected.Id "Assigned ID rolled back"
        Expect.equal (Seq.length (first.Sample.list())) 0 "Projection rolled back"
        Expect.isFalse first.History.CanUndo "History unchanged"
        second.Sample.create("accepted") |> ignore
        Expect.equal (Seq.length (second.Sample.list())) 1 "Other ARC remains writable"
        Expect.equal (db.Query("PRAGMA foreign_key_check")).Count 0 "Foreign keys intact")

    testCase "separate connections refresh per ARC" (fun _ ->
        let path = Path.combineNative (folder()) "revision.sqlite"
        use firstSession = ARCManagement.Session.createFile(path)
        let first,second = firstSession.createArc(root()),firstSession.createArc(root())
        use otherSession = ARCManagement.Session.openFile(path)
        let reader = otherSession.openArc(first.ArcId)
        let other = otherSession.openArc(second.ArcId)
        first.Dataset.setTitle(first.Model,Some("first writer")) |> ignore
        Expect.equal reader.Model.Title (Some "first writer") "The next read sees the other connection's commit"
        let held = reader.Model
        first.Dataset.setDescription(first.Model,Some("described")) |> ignore
        let result = reader.Dataset.setLicense(held,Some("license"))
        Expect.equal result.Description (Some "described") "A write applies to the refreshed state"
        Expect.equal result.Title (Some "first writer") "Earlier commits are kept"
        Expect.isNone held.Description "The held value is unchanged"
        Expect.equal first.Model.License (Some "license") "The first connection sees the second write"
        other.Dataset.setTitle(other.Model,Some("independent")) |> ignore
        Expect.isNone second.Model.Description "Other ARC unaffected"
        Expect.equal second.Model.Title (Some "independent") "Other ARC refreshed"
        reader.close()
        Expect.equal (otherSession.openArc(first.ArcId)).Model.Title (Some "first writer") "Reopening reads the same state")

    testCase "workspace wrappers delegate one graph and borrowed close preserves siblings" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let graph = session.createArc(root())
        let sibling = session.createArc(root())
        let path = folder()
        let workspace = ARC.bind(graph,path)
        let otherWrapper = ARC.fromGraph(graph)
        same workspace.ManagedGraph graph
        Expect.equal workspace.Model.Id graph.Model.Id "Forwarded model"
        same workspace.Sample graph.Sample
        same workspace.Dataset graph.Dataset
        same workspace.History graph.History
        same otherWrapper.ManagedGraph (session.openArc(graph.ArcId))
        let sample = workspace.Sample.create("shared")
        Expect.equal (graph.Sample.get(sample.Id.Value)).Name "shared" "Both facades read the same state"
        workspace.save()
        let db = sql session
        let checkpoint = (Store.metadata db graph.ArcId).GetByName("saved_graph").AsText()
        let revision = (Store.metadata db graph.ArcId).GetByName("revision").AsInteger()
        graph.toYaml() |> ignore
        Expect.equal ((Store.metadata db graph.ArcId).GetByName("revision").AsInteger()) revision "Text export does not advance revision"
        Expect.equal ((Store.metadata db graph.ArcId).GetByName("saved_graph").AsText()) checkpoint "Text export leaves checkpoint intact"
        graph.Dataset.setTitle(graph.Model,Some("pending")) |> ignore
        graph.toYaml() |> ignore
        Expect.isTrue workspace.IsDirty "Text export does not save pending changes"
        Expect.equal ((Store.metadata db graph.ArcId).GetByName("saved_graph").AsText()) checkpoint "Pending export leaves checkpoint intact"
        otherWrapper.close()
        Expect.throws (fun () -> workspace.save()) "Closing one wrapper invalidates all wrappers"
        Expect.throws (fun () -> graph.toYaml() |> ignore) "Shared graph closed"
        sibling.Sample.create("still active") |> ignore
        let reopened = session.openArc(graph.ArcId)
        Expect.isFalse (Object.ReferenceEquals(reopened,graph)) "Reopening creates a new facade"
        Path.writeFileText (Path.combineNative path ARCtrl.Helper.Layout.ARCFileName) "external invalid YAML"
        let rebound = ARC.fromGraph(reopened)
        Expect.equal rebound.Model.Title (Some "pending") "Wrapping retained binding performs no filesystem reconciliation"
        session.close()
        Expect.throws (fun () -> rebound.Model |> ignore) "Session closure invalidates workspace")

    testCase "private workspace sessions close even after their graph was closed" (fun _ ->
        let workspace = ARC.create(folder(),root())
        let graph = workspace.ManagedGraph
        let repository = graph.Context.Repository
        let borrowed = ARC.fromGraph(graph)
        borrowed.close()
        repository.EnsureOpen()
        workspace.close()
        workspace.close()
        Expect.throws repository.EnsureOpen "Owning wrapper releases private connection after graph closure")

    testCase "workspace YAML import initializes graph binding and baseline atomically" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let sibling = session.createArc(root())
        let path = folder()
        let detached = Dataset(["administrative"],["imported"])
        detached.State <- { detached.State with HasParts = [detached] }
        Path.writeFileText (Path.combineNative path ARCtrl.Helper.Layout.ARCFileName) (ARCManagement.DatasetYaml.encode(detached))
        let db = sql session
        db.ExecuteScript("CREATE TRIGGER fail_workspace BEFORE INSERT ON arc WHEN NEW.folder IS NOT NULL BEGIN SELECT RAISE(ABORT,'injected'); END;")
        Expect.throws (fun () -> ARC.importFolder(session,path) |> ignore) "Injected import failure"
        Expect.equal (session.listArcs()).Count 1 "Failed import leaves no partial entry or binding"
        same (session.openArc(sibling.ArcId)) sibling
        db.Execute("DROP TRIGGER fail_workspace")
        let imported = ARC.importFolder(session,path)
        Expect.isSome imported.Model.Id "Workspace import applies management IDs"
        same (Seq.item 0 imported.Model.HasParts) imported.Model
        Expect.equal imported.Folder path "Binding initialized"
        Expect.isFalse imported.IsDirty "Import establishes canonical checkpoint"
        Expect.isFalse imported.History.CanUndo "Import starts with empty history"
        imported.close()
        sibling.Sample.create("still active") |> ignore)

    testCase "equal entity IDs in two ARCs stay isolated in views" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let build conformsTo =
            let proc = ARCBaseModel.Process("p", id="p", input=EntityReference.Sample(Sample("in",id="in")), output=EntityReference.Sample(Sample("out",id="out")))
            Dataset(conformsTo,["root"],id="root",processes=[proc])
        let first = session.createArc(build ["process-provenance"])
        let second = session.createArc(build ["administrative"])
        let db = sql session
        let countFor view (arc: ARCManagement.ManagedGraph) =
            db.Query("SELECT count(*) FROM " + view + " WHERE arc_id=$arc",[SqlParameter("arc",SqlValue.Text(arc.ArcId))]).[0].Get(0).AsInteger()
        Expect.equal (countFor "process_edges" first) 1L "First ARC edge"
        Expect.equal (countFor "process_edges" second) 1L "Second ARC edge"
        Expect.equal (scalar db "SELECT count(*) FROM process_edges") 2L "No cross-ARC joins"
        Expect.equal (countFor "core_validation_errors" first) 0L "First ARC is valid"
        Expect.equal (countFor "core_validation_errors" second) 1L "Second ARC lacks process-provenance"
        Expect.equal (scalar db "SELECT count(*) FROM core_validation_errors") 1L "Errors stay with their ARC"
        Expect.equal (scalar db "SELECT count(*) FROM sample") 4L "Equal sample IDs coexist")

    testCase "a title edit on a large ARC changes a constant number of rows" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let processes = [for index in 1 .. 60 -> ARCBaseModel.Process("process " + string index, input=EntityReference.Sample(Sample("in " + string index)), output=EntityReference.Sample(Sample("out " + string index)))]
        let graph = session.createArc(Dataset(["process-provenance"],["root"],id="root",processes=processes))
        let db = sql session
        Expect.isTrue (scalar db "SELECT count(*) FROM entity" > 180L) "Many entities stored"
        let before = scalar db "SELECT total_changes()"
        graph.Dataset.setTitle(graph.Model,Some("edited")) |> ignore
        let changed = scalar db "SELECT total_changes()" - before
        Expect.equal changed 4L "Revision bump, dataset row, history row and cursor update"
        Expect.equal (graph.Model.Title) (Some "edited") "Edit applied")

    testCase "appending to a long list inserts one association row" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let processes = [for index in 1 .. 100 -> ARCBaseModel.Process("process " + string index)]
        let graph = session.createArc(Dataset(["process-provenance"],["root"],id="root",processes=processes))
        let extra = graph.Process.create("extra")
        let db = sql session
        let before = scalar db "SELECT total_changes()"
        graph.Dataset.addProcess(graph.Model,extra) |> ignore
        let changed = scalar db "SELECT total_changes()" - before
        Expect.equal changed 4L "Revision bump, one dataset_process row, one history row and the cursor update"
        Expect.equal (scalar db "SELECT count(*) FROM dataset_process") 101L "All rows kept"
        Expect.equal (scalar db ("SELECT position FROM dataset_process WHERE process_id='" + extra.Id.Value + "'")) 100L "Appended at the end")

    testCase "upserting an unchanged entity is a no-op that keeps the redo branch" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let graph = session.createArc(root())
        let first = graph.Sample.create("first")
        graph.Sample.create("second") |> ignore
        graph.Sample.create("third") |> ignore
        graph.History.undo()
        let db = sql session
        let rows = scalar db "SELECT count(*) FROM history"
        let revision = scalar db "SELECT revision FROM arc"
        graph.Sample.upsert(Sample("first", id = first.Id.Value)) |> ignore
        Expect.isTrue graph.History.CanRedo "Redo branch untouched"
        Expect.equal (scalar db "SELECT count(*) FROM history") rows "No history row added"
        Expect.equal (scalar db "SELECT revision FROM arc") revision "Revision unchanged")

    testCase "undo and redo of a delete restore the entity, its links and its list position" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let graph = session.createArc(root())
        let a,b,c = graph.Sample.create("a"),graph.Sample.create("b"),graph.Sample.create("c")
        let proc = graph.Process.create("p")
        graph.Dataset.addProcess(graph.Model,proc) |> ignore
        graph.Process.setInput(proc, Some(EntityReference.Sample b)) |> ignore
        let db = sql session
        let names () = graph.Sample.list() |> Seq.map (fun sample -> sample.Name) |> List.ofSeq
        let linked () = scalar db ("SELECT count(*) FROM process_io WHERE sample_id='" + b.Id.Value + "'")
        let positions () =
            db.Query("SELECT id FROM entity WHERE type='Sample' ORDER BY ordinal") |> Seq.map (fun row -> row.Get(0).AsText()) |> List.ofSeq
        let expected = [a.Id.Value; b.Id.Value; c.Id.Value]
        graph.Sample.delete(b)
        Expect.equal (names()) ["a";"c"] "Deleted"
        Expect.isNone (graph.Process.get(proc.Id.Value)).Input "Link removed with the target"
        Expect.equal (linked()) 0L "Endpoint row removed"
        graph.History.undo()
        Expect.equal (names()) ["a";"b";"c"] "List position restored"
        Expect.equal (positions()) expected "Stored order restored"
        Expect.equal (linked()) 1L "Endpoint row restored"
        match (graph.Process.get(proc.Id.Value)).Input with Some(EntityReference.Sample sample) -> Expect.equal sample.Id b.Id "Restored input" | _ -> failwith "Expected restored input"
        graph.History.redo()
        Expect.equal (names()) ["a";"c"] "Redo deletes again"
        Expect.equal (linked()) 0L "Redo removes the endpoint row"
        graph.History.undo()
        Expect.equal (positions()) expected "Second undo restores the position")

    testCase "reopening a file repository rebuilds the same state" (fun _ ->
        let path = Path.combineNative (folder()) "state.sqlite"
        let session = ARCManagement.Session.createFile(path)
        let graph = session.createArc(root())
        let sample = graph.Sample.create("sample")
        let proc = graph.Process.create("process")
        graph.Dataset.addProcess(graph.Model,proc) |> ignore
        graph.Process.setInput(proc, Some(EntityReference.Sample sample)) |> ignore
        graph.Dataset.setTitle(graph.Model,Some("")) |> ignore
        graph.Entity.setProperty(graph.Model, "score", Entity.Number(0.5)) |> ignore
        graph.Entity.addProperty(graph.Model, "label", Entity.Text("x")) |> ignore
        graph.Sample.delete(graph.Sample.create("gone"))
        let withoutSupplied (state: State) = { state with Entities = state.Entities |> List.map (fun row -> { row with SuppliedId = None }) }
        let expected = withoutSupplied graph.Context.State
        let arcId = graph.ArcId
        session.close()
        use reopened = ARCManagement.Session.openFile(path)
        let again = reopened.openArc(arcId)
        Expect.isTrue (expected = withoutSupplied again.Context.State) "Rows, order and extensions are identical"
        Expect.isTrue (expected = withoutSupplied (fst (Store.load (sql reopened) arcId))) "Loading is repeatable")
    testCase "createArc rejects distinct objects that share an ID even when their definitions are equal" (fun _ ->
        use session = ARCManagement.Session.createInMemory()
        let proc = ARCBaseModel.Process("p", input = EntityReference.Sample(Sample("z", id = "dup")), output = EntityReference.Sample(Sample("z", id = "dup")))
        Expect.throws (fun () -> session.createArc(Dataset(["process-provenance"],["root"],processes = [proc])) |> ignore) "Distinct objects with one ID"
        Expect.equal (session.listArcs()).Count 0 "No entry was created")
]
