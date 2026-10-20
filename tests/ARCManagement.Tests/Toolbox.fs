module ARCManagement.Tests.Toolbox

open System
open ARCBaseModel
open ARCManagement
open ARCManagement.Helper
open ARCtrl
open Fable.Pyxpecto

let private root () = Dataset(["process-provenance"],["root"],id="root")
let private rev (g:ManagedGraph) = ARCManagement.Internal.Store.revision g.Context.Repository.Connection g.ArcId
let private same a b = Expect.isTrue (obj.ReferenceEquals(a,b)) "Exact facade identity"

let tests = testList "Immutable workspaces" [
    testCase "entries isolate IDs revisions history and immutable input reuse" (fun _ ->
        use session = Session.createInMemory()
        let source = root()
        let first,second = session.createArc(source),session.createArc(source)
        first.Sample.add(Sample("first",id="s")) |> ignore
        second.Sample.add(Sample("second",id="s")) |> ignore
        first.Sample.setName(first.Sample.get("s"),"changed") |> ignore
        first.History.undo() |> ignore
        Expect.equal (first.Sample.get("s")).Name "first" "Independent undo"
        Expect.equal (second.Sample.get("s")).Name "second" "Other ARC untouched"
        Expect.notEqual first.ArcId second.ArcId "Independent ARC IDs"
        Expect.equal (session.listArcs()).Count 2 "Lightweight metadata"
        Expect.isNone session.DatabasePath "Memory path absent")

    testCase "workspace forwards exact graph groups and closes borrowed graph only" (fun _ ->
        use session = Session.createInMemory()
        let graph,sibling = session.createArc(root()),session.createArc(root())
        let path = Path.combineNative (Helpers.folder "binding") "workspace"
        let arc = ARC.bind(graph,path)
        let another = ARC.fromGraph(graph)
        same arc.ManagedGraph graph
        same arc.Dataset graph.Dataset
        same another.ManagedGraph graph
        let held = graph.Model
        arc.close()
        Expect.throws (fun () -> another.Model |> ignore) "Shared wrappers invalidated"
        sibling.Sample.add(Sample("still open")) |> ignore
        Expect.equal held.Identifiers[0] "root" "Snapshot survives graph closure")

    testCase "save is explicit and standalone edits do not dirty root export" (fun _ ->
        let path = Path.combineNative (Helpers.folder "save") "workspace"
        use arc = ARC.create(path,root())
        let graph = arc.ManagedGraph
        Expect.isFalse (Path.fileExists(Path.combineNative path "arc.yml")) "No implicit export"
        arc.save()
        Expect.isFalse arc.IsDirty "Successful checkpoint"
        let sample = graph.Sample.add(Sample("standalone",id="s"))
        Expect.isFalse arc.IsDirty "Standalone creation does not affect root"
        Expect.isTrue arc.HasSessionOnlyObjects "Standalone objects tracked"
        graph.Sample.setName(graph.Sample.get("s"),"changed") |> ignore
        Expect.isFalse arc.IsDirty "Standalone edit does not affect export"
        graph.Dataset.setTitle(graph.Dataset.get("root"),"pending") |> ignore
        Expect.isTrue arc.IsDirty "Reachable edit pending"
        let yaml = graph.toYaml()
        Expect.isFalse (yaml.Contains("standalone")) "Root-only export"
        Expect.isTrue arc.IsDirty "toYaml does not checkpoint"
        arc.save()
        Expect.isFalse arc.IsDirty "Publication checkpoints root")

    testCase "workspace reopen retains unsaved SQL values and history" (fun _ ->
        let path = Path.combineNative (Helpers.folder "resume") "workspace"
        let arc = ARC.create(path,root())
        let graph = arc.ManagedGraph
        graph.Sample.add(Sample("standalone",id="s")) |> ignore
        graph.Dataset.setTitle(graph.Dataset.get("root"),"unsaved") |> ignore
        let held = graph.Model
        arc.close()
        Expect.isFalse (Path.fileExists(Path.combineNative path "arc.yml")) "Close does not export"
        use resumed = ARC.openFolder(path,"sql")
        Expect.equal resumed.Model.Title (Some "unsaved") "SQL resumes working value"
        Expect.equal (resumed.Sample.get("s")).Name "standalone" "Standalone resumes"
        resumed.History.undo() |> ignore
        Expect.isNone resumed.Model.Title "Undo restored database"
        Expect.equal held.Title (Some "unsaved") "Old snapshot untouched"
        resumed.History.redo() |> ignore
        Expect.equal resumed.Model.Title (Some "unsaved") "Redo persisted")

    testCase "save does not invalidate focused subjects or unchanged definition baselines" (fun _ ->
        let path = Path.combineNative (Helpers.folder "checkpoint") "workspace"
        use arc = ARC.create(path,root())
        let before = arc.Model
        arc.save()
        Expect.isFalse (arc.Dataset.upsert(before,baseline=before)).Changed "Checkpoint changes metadata only"
        arc.Dataset.setTitle(before,"after save") |> ignore
        Expect.isTrue arc.IsDirty "Focused edits remain usable")

    testCase "external YAML conflicts require explicit source and preserve SQL" (fun _ ->
        let path = Path.combineNative (Helpers.folder "conflict") "workspace"
        use arc = ARC.create(path,root())
        arc.Dataset.setTitle(arc.Dataset.get("root"),"original") |> ignore
        arc.save()
        arc.Dataset.setTitle(arc.Dataset.get("root"),"SQL") |> ignore
        let yamlPath = Path.combineNative path "arc.yml"
        Path.writeFileText yamlPath (Path.readFileText(yamlPath).Replace("original","external"))
        Expect.throws (fun () -> arc.save()) "External publication blocked"
        Expect.equal arc.Model.Title (Some "SQL") "SQL unchanged"
        Expect.isTrue arc.IsDirty "Pending state retained"
        Expect.throws (fun () -> arc.recoverFolder("auto")) "Both sides changed"
        arc.recoverFolder("sql")
        arc.save()
        Expect.isTrue (Path.readFileText(yamlPath).Contains("SQL")) "Explicit SQL preference saved")

    testCase "import folder borrows session and leaves supplied YAML unchanged" (fun _ ->
        let path = Helpers.folder "folder-import"
        let text = DatasetYaml.encode(root())
        Path.writeFileText (Path.combineNative path "arc.yml") text
        use session = Session.createInMemory()
        let sibling = session.createArc(root())
        let arc = ARC.importFolder(session,path)
        Expect.isFalse arc.IsDirty "Import initializes baseline"
        Expect.equal (Path.readFileText(Path.combineNative path "arc.yml")) text "No publication on import"
        arc.close()
        sibling.Sample.add(Sample("still usable")) |> ignore
        Expect.equal (session.listArcs()).Count 2 "Borrowed session remains open")

    testCase "published file with failed checkpoint requires explicit recovery" (fun _ ->
        let path = Path.combineNative (Helpers.folder "checkpoint-failure") "workspace"
        use arc = ARC.create(path,root())
        let graph = arc.ManagedGraph
        graph.Dataset.setTitle(graph.Dataset.get("root"),"before") |> ignore
        arc.save()
        let edited = graph.Dataset.setTitle(graph.Dataset.get("root"),"after")
        let before = rev graph
        let db = graph.Context.Repository.Connection
        db.ExecuteScript("CREATE TRIGGER reject_checkpoint BEFORE UPDATE ON session WHEN NEW.saved_graph IS NOT OLD.saved_graph BEGIN SELECT RAISE(ABORT,'checkpoint failure'); END;")
        Expect.throws (fun () -> arc.save()) "Injected checkpoint failure"
        Expect.equal (rev graph) before "SQL revision rolled back"
        Expect.equal graph.Model.Title (Some "after") "Working value retained"
        Expect.isTrue arc.IsDirty "Last successful checkpoint retained"
        let yamlPath = Path.combineNative path "arc.yml"
        Expect.isTrue (Path.readFileText(yamlPath).Contains("after")) "File publication already succeeded"
        Expect.throws (fun () -> arc.save()) "Retry requires source selection"
        db.Execute("DROP TRIGGER reject_checkpoint")
        arc.recoverFolder("sql")
        arc.save()
        Expect.isFalse arc.IsDirty "Explicit recovery permits checkpoint")

    testCase "YAML recovery archives standalone state and history" (fun _ ->
        let path = Path.combineNative (Helpers.folder "yaml-recovery") "workspace"
        use arc = ARC.create(path,root())
        let graph = arc.ManagedGraph
        graph.Dataset.setTitle(graph.Dataset.get("root"),"original") |> ignore
        arc.save()
        graph.Sample.add(Sample("standalone",id="s")) |> ignore
        graph.Dataset.setTitle(graph.Dataset.get("root"),"working") |> ignore
        let held = graph.Model
        let yamlPath = Path.combineNative path "arc.yml"
        Path.writeFileText yamlPath (Path.readFileText(yamlPath).Replace("original","external"))
        arc.recoverFolder("yml")
        Expect.equal graph.Model.Title (Some "external") "Explicit YAML source adopted"
        Expect.isNone (graph.Sample.read("s")) "Displaced standalone entry removed from current registry"
        Expect.equal held.Title (Some "working") "Displaced snapshot remains readable"
        Expect.isFalse graph.History.CanUndo "Replacement establishes a new history baseline"
        let db = graph.Context.Repository.Connection
        let archived = db.Query("SELECT state FROM arc_archive")
        Expect.equal archived.Count 1 "One displaced entry archived"
        let prior = ARCManagement.Internal.Codec.decodeState(archived[0].Get(0).AsText())
        Expect.isTrue (prior.Entities |> List.exists (fun row -> row.Id="s")) "Archive retains standalone state"
        Expect.isTrue ((db.Scalar("SELECT count(*) FROM archived_history_change") |> Option.get).AsInteger()>0L) "Incremental history archived")
]
