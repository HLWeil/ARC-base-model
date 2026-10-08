module ManagementPrototype.Tests.Toolbox

open ARCtrl.Helper

open System
open ARCBaseModel
open ARCtrl
open ARCtrl.Internal
open ARCSession.Internal
open PolyglotSQLite
open Fable.Pyxpecto

let private folder () = Helpers.folder "toolbox"

let private root () = Dataset(["process-provenance"],["root"],id="root")
let private sql (session: ARCSession.Session) = (unbox<ISessionOwner>(box session)).Repository.Connection
let private same actual expected = Expect.isTrue (Object.ReferenceEquals(actual,expected)) "Shared object reference"
let private scalar (db: SqliteConnection) statement = db.Query(statement).[0].Get(0).AsInteger()

let tests = testList "ARC toolbox" [
    testCase "memory repository isolates identical IDs and operation histories" (fun _ ->
        use session = ARCSession.Session.createInMemory()
        Expect.isNone session.DatabasePath "Memory has no file path"
        let first,second = session.createArc(root()),session.createArc(root())
        Expect.notEqual first.ArcId second.ArcId "ARC identity is separate from root identity"
        same (session.openArc(first.ArcId)) first
        first.Sample.upsert(Sample("first",id="sample"))
        second.Sample.upsert(Sample("second",id="sample"))
        first.Sample.setName(first.Sample.get("sample"),"changed") |> ignore
        second.Dataset.setTitle(second.Model,"other") |> ignore
        first.History.undo()
        Expect.equal (first.Sample.get("sample")).Name "first" "Independent undo"
        Expect.equal (second.Sample.get("sample")).Name "second" "Other ARC unchanged"
        Expect.equal second.Model.Title (Some "other") "Other revision remains valid"
        Expect.equal (scalar (sql session) "SELECT count(*) FROM sample") 2L "Both ID-qualified rows"
        Expect.equal (session.listArcs()).Count 2 "Both entries"
        Expect.isNone (session.listArcs()).[0].Folder "Unbound metadata")

    testCase "API rejects foreign targets and adopting a managed graph" (fun _ ->
        use session = ARCSession.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        let sample = first.Sample.create("sample")
        let proc = second.Process.create("process")
        Expect.throws (fun () -> second.Process.setInputSample(proc,sample) |> ignore) "Foreign endpoint"
        Expect.throws (fun () -> second.Sample.register(sample) |> ignore) "Foreign registration"
        Expect.throws (fun () -> session.createArc(first.Model) |> ignore) "Foreign root"
        Expect.throws (fun () -> second.Entity.setObjectProperty(second.Model,"foreign",sample) |> ignore) "Foreign extension"
        use otherSession = ARCSession.Session.createInMemory()
        Expect.throws (fun () -> otherSession.createArc(first.Model) |> ignore) "Ownership extends across repositories"
        let other = otherSession.createArc(root())
        Expect.throws (fun () -> other.Sample.register(sample) |> ignore) "Foreign registration across repositories"
        Expect.isNone proc.Input "Failure leaves model untouched")

    testCase "SQL foreign keys reject cross ARC targets and typed endpoints" (fun _ ->
        use session = ARCSession.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        first.Sample.upsert(Sample("sample",id="foreign"))
        let proc = second.Process.create("process")
        let db = sql session
        let parameters = [SqlParameter("arc",SqlValue.Text(second.ArcId));SqlParameter("id",SqlValue.Text(proc.Id.Value))]
        Expect.equal (scalar db "PRAGMA foreign_keys") 1L "Enforcement enabled"
        Expect.throws (fun () -> db.Execute("INSERT INTO entity_reference VALUES($arc,$id,'input',0,'foreign')",parameters)) "Foreign generic relationship"
        Expect.throws (fun () -> db.WithTransaction(fun () -> db.Execute("UPDATE process SET input_sample_id='foreign' WHERE arc_id=$arc AND id=$id",parameters))) "Foreign typed endpoint"
        Expect.throws (fun () -> db.Execute("INSERT INTO entity_extension(arc_id,owner_id,property,path,storage,target_id) VALUES($arc,$id,'foreign','','object','foreign')",parameters)) "Foreign extension"
        Expect.equal (db.Query("PRAGMA foreign_key_check")).Count 0 "No dangling references")

    testCase "closing ARC leaves repository usable and reopening creates a fresh projection" (fun _ ->
        use session = ARCSession.Session.createInMemory()
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
        let session = ARCSession.Session.createFile(path)
        let first,second = session.createArc(root()),session.createArc(root())
        let firstId,secondId = first.ArcId,second.ArcId
        first.Dataset.setTitle(first.Model,"first") |> ignore
        second.Dataset.setTitle(second.Model,"second") |> ignore
        session.close()
        use resumed = ARCSession.Session.openFile(path)
        Expect.equal (resumed.listArcs()).Count 2 "Metadata only"
        let reopened = resumed.openArc(firstId)
        reopened.History.undo()
        Expect.isNone reopened.Model.Title "Restored first history"
        Expect.equal (resumed.openArc(secondId)).Model.Title (Some "second") "Other ARC retained"
        // Corrupt one snapshot to prove enumeration and opening another ARC are lazy.
        (sql resumed).Execute("UPDATE session SET state='invalid' WHERE arc_id=$arc",[SqlParameter("arc",SqlValue.Text(secondId))])
        resumed.openArc(secondId).close()
        Expect.equal (resumed.listArcs()).Count 2 "No graph parsing during enumeration"
        same (resumed.openArc(firstId)) reopened
        Expect.throws (fun () -> resumed.openArc(secondId) |> ignore) "Invalid graph fails only when opened")

    testCase "file factories reject existing missing and unrelated databases" (fun _ ->
        let path = Path.combineNative (folder()) "collection.sqlite"
        Expect.throws (fun () -> ARCSession.Session.openFile(path) |> ignore) "Missing file"
        Expect.isFalse (Path.pathExists path) "Open never creates"
        let session = ARCSession.Session.createFile(path)
        session.createArc(root()) |> ignore
        session.close()
        Expect.throws (fun () -> ARCSession.Session.createFile(path) |> ignore) "Existing file"
        use reopened = ARCSession.Session.openFile(path)
        Expect.equal (reopened.listArcs()).Count 1 "No replacement"
        let unrelated = Path.combineNative (folder()) "unrelated.sqlite"
        use db = Sqlite.OpenFile(unrelated)
        db.Execute("CREATE TABLE unrelated(value TEXT)")
        Expect.throws (fun () -> ARCSession.Session.openFile(unrelated) |> ignore) "Unrelated database"
        let malformed = Path.combineNative (folder()) "malformed.sqlite"
        let empty = ARCSession.Session.createFile(malformed)
        empty.close()
        use broken = Sqlite.OpenFile(malformed)
        broken.Execute("DROP TABLE entity_reference")
        broken.Execute("CREATE TABLE entity_reference(arc_id TEXT NOT NULL,owner_id TEXT NOT NULL,property TEXT NOT NULL,position INTEGER NOT NULL,target_id TEXT NOT NULL,PRIMARY KEY(arc_id,owner_id,property,position))")
        Expect.throws (fun () -> ARCSession.Session.openFile(malformed) |> ignore) "Correct version with missing relationship constraints rejected")

    testCase "folder binding and importing are independent of database storage" (fun _ ->
        use session = ARCSession.Session.createInMemory()
        let first = session.createArc(root())
        Expect.throws (fun () -> first.save()) "Binding required"
        let path = folder()
        first.bindFolder(path)
        Expect.isFalse (Path.pathExists(Path.combineNative path Path.ARCFileName)) "Bind does not save"
        first.save()
        Expect.isFalse first.IsDirty "Export establishes checkpoint"
        first.close()
        let reopened = session.openArc(first.ArcId)
        Expect.equal reopened.FolderBinding (Some path) "Binding retained"
        let other = session.createArc(root())
        Expect.throws (fun () -> other.bindFolder(path)) "Existing ARC requires import"
        use importedSession = ARCSession.Session.createInMemory()
        let imported = ARC.importFolder(importedSession,path)
        let importedAgain = ARC.importFolder(importedSession,path)
        Expect.notEqual imported.ArcId importedAgain.ArcId "Each import creates a new entry"
        Expect.isFalse (Object.ReferenceEquals(imported.Model,importedAgain.Model)) "Repeated imports have independent projections"
        Expect.equal imported.Model.Identifiers[0] "root" "Imported graph"
        Expect.isFalse imported.IsDirty "Import establishes baseline"
        imported.Dataset.setTitle(imported.Model,"native") |> ignore
        imported.save()
        Expect.throws (fun () -> reopened.save()) "External export invalidates old binding baseline")

    testCase "failed saves retain one ARC's checkpoint without changing another" (fun _ ->
        use session = ARCSession.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        first.bindFolder(folder())
        second.bindFolder(folder())
        first.save()
        second.save()
        let db = sql session
        let checkpoint = (Store.metadata db first.ArcId).GetByName("saved_graph").AsText()
        first.Dataset.setTitle(first.Model,"pending") |> ignore
        db.ExecuteScript("CREATE TRIGGER fail_checkpoint BEFORE UPDATE OF baseline ON session WHEN NEW.arc_id='" + first.ArcId + "' BEGIN SELECT RAISE(ABORT,'injected'); END;")
        Expect.throws first.save "Checkpoint commit failure"
        Expect.equal ((Store.metadata db first.ArcId).GetByName("saved_graph").AsText()) checkpoint "Previous checkpoint retained"
        Expect.isTrue first.IsDirty "Edits stay pending"
        Expect.isFalse second.IsDirty "Other checkpoint unchanged"
        db.Execute("DROP TRIGGER fail_checkpoint")
        first.recoverFolder("sql")
        first.save()
        Expect.isFalse first.IsDirty "Explicit recovery allows retry"
        second.Dataset.setTitle(second.Model,"other") |> ignore
        second.History.undo()
        Expect.isFalse second.IsDirty "Other history and checkpoint remain independent")

    testCase "folder recovery archives only the displaced ARC including history" (fun _ ->
        use session = ARCSession.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        let path = folder()
        first.bindFolder(path)
        first.Dataset.setTitle(first.Model,"original") |> ignore
        first.save()
        first.Sample.create("standalone") |> ignore
        second.Dataset.setTitle(second.Model,"unaffected") |> ignore
        let yaml = Path.readFileText(Path.combineNative path Path.ARCFileName)
        Path.writeFileText (Path.combineNative path Path.ARCFileName) (yaml.Replace("title: original","title: external"))
        Expect.throws (fun () -> first.recoverFolder("auto")) "Session-only objects block automatic reload"
        first.recoverFolder("yml")
        Expect.equal first.Model.Title (Some "external") "Replacement graph"
        Expect.equal (first.Sample.list()).Count 0 "Fresh registry"
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
        use session = ARCSession.Session.createInMemory()
        let first,second = session.createArc(root()),session.createArc(root())
        let db = sql session
        db.ExecuteScript("CREATE TRIGGER fail_sample BEFORE INSERT ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected'); END;")
        let rejected = Sample("reject")
        Expect.throws (fun () -> first.Sample.register(rejected) |> ignore) "Injected write failure"
        Expect.isNone rejected.Id "Assigned ID rolled back"
        Expect.equal (first.Sample.list()).Count 0 "Projection rolled back"
        Expect.isFalse first.History.CanUndo "History unchanged"
        second.Sample.create("accepted") |> ignore
        Expect.equal (second.Sample.list()).Count 1 "Other ARC remains writable"
        Expect.equal (db.Query("PRAGMA foreign_key_check")).Count 0 "Foreign keys intact")

    testCase "separate connections check revisions per ARC" (fun _ ->
        let path = Path.combineNative (folder()) "revision.sqlite"
        use firstSession = ARCSession.Session.createFile(path)
        let first,second = firstSession.createArc(root()),firstSession.createArc(root())
        use otherSession = ARCSession.Session.openFile(path)
        let stale = otherSession.openArc(first.ArcId)
        let other = otherSession.openArc(second.ArcId)
        first.Dataset.setTitle(first.Model,"first writer") |> ignore
        Expect.throws (fun () -> stale.Dataset.setTitle(stale.Model,"stale") |> ignore) "Stale ARC rejected"
        Expect.isNone stale.Model.Title "Failed stale write leaves graph intact"
        other.Dataset.setTitle(other.Model,"independent") |> ignore
        stale.close()
        Expect.equal (otherSession.openArc(first.ArcId)).Model.Title (Some "first writer") "Explicit reopen recovers")
]
