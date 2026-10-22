module ARCManagement.Tests.Behavior

open ARCManagement.Helper

open System
open ARCManagement.Internal
open ARCBaseModel
open ARCtrl
open Fable.Pyxpecto

let private folder () = Helpers.folder "behavior"

let private temporaryFiles path =
    Path.getSubFiles path |> Array.filter (fun file ->
        let name = Path.split file |> Array.last
        name.StartsWith(".arc-") && name.EndsWith(".tmp"))

let private create () =
    let path = folder()
    path, ARC.create(path, Dataset(["process-provenance"], ["example"]))

let private input (proc: ARCBaseModel.Process) =
    match proc.Input with
    | Some(EntityReference.Sample sample) -> sample
    | _ -> failwith "Expected sample input"

let private output (proc: ARCBaseModel.Process) =
    match proc.Output with
    | Some(EntityReference.Sample sample) -> sample
    | _ -> failwith "Expected sample output"

let private withSql path action =
    use sql = PolyglotSQLite.Sqlite.OpenFile(path)
    action sql

let private count table path =
    withSql path (fun sql -> sql.Query("SELECT count(*) FROM " + table).[0].Get(0).AsInteger())

let private archives path =
    withSql (Path.combineNative (Path.combineNative path ARCtrl.Helper.Layout.ARCConfigFolderName) "arc.sqlite") (fun sql ->
        sql.Query("SELECT archive_id FROM arc_archive") |> Seq.map (fun row -> row.Get(0).AsText()) |> Seq.toArray)
let private archivedHistory path archiveId =
    withSql (Path.combineNative (Path.combineNative path ARCtrl.Helper.Layout.ARCConfigFolderName) "arc.sqlite") (fun sql ->
        sql.Query("SELECT count(*) FROM archived_history WHERE archive_id=$id", [PolyglotSQLite.SqlParameter("id",PolyglotSQLite.SqlValue.Text(archiveId))]).[0].Get(0).AsInteger())
let private archivedSamples path =
    withSql (Path.combineNative (Path.combineNative path ARCtrl.Helper.Layout.ARCConfigFolderName) "arc.sqlite") (fun sql ->
        sql.Query("SELECT state FROM arc_archive") |> Seq.exists (fun row ->
            let state = ARCManagement.Internal.Codec.decodeState(row.Get(0).AsText())
            state.Entities |> List.exists (fun entity -> entity.Kind = "Sample")))

let private externalTitle path title =
    let yaml = Path.combineNative path ARCtrl.Helper.Layout.ARCFileName
    Path.writeFileText yaml (Path.readFileText(yaml).Replace("title: original", "title: " + title))

let tests = testList "Management prototype" [
    testCase "create adopts a supplied graph without modifying it" <| fun _ ->
        let sample = Sample("leaf")
        let proc = ARCBaseModel.Process("measurement", input = EntityReference.Sample sample)
        let root = Dataset(["process-provenance"], ["example"], processes = [proc])
        use arc = ARC.create(folder(), root)
        Expect.isNone root.Id "Supplied root keeps its absent ID"
        Expect.isNone proc.Id "Supplied process keeps its absent ID"
        Expect.isNone sample.Id "Supplied sample keeps its absent ID"
        Expect.isSome arc.Model.Id "The stored root carries an ID"
        let stored = Seq.item 0 arc.Model.Processes
        Expect.equal (arc.Process.get(stored.Id.Value)).Name "measurement" "Stored process resolves by ID"
        Expect.equal (input stored).Name "leaf" "Stored input resolves"
        Expect.isSome (input stored).Id "Stored input carries an ID"
        Expect.isFalse arc.History.CanUndo "Initialization is not a user operation"

    testCase "results carry IDs while supplied inputs keep absent IDs" <| fun _ ->
        let _, created = create()
        use arc = created
        let supplied = Sample("leaf")
        let stored = arc.Sample.add(supplied)
        Expect.isNone supplied.Id "Supplied input is unchanged"
        Expect.isSome stored.Id "Result carries the assigned ID"
        Expect.equal (arc.Sample.get(stored.Id.Value)).Name "leaf" "Stored copy resolves by ID"
        let again = arc.Sample.add(Sample("leaf"))
        Expect.notEqual again.Id stored.Id "Every insertion gets its own ID"

    testCase "endpoint editing is reversible and SQL mirrors the graph" <| fun _ ->
        let _, created = create()
        use arc = created
        let sampleA = arc.Sample.create("A")
        let sampleB = arc.Sample.create("B")
        let proc = arc.Process.create("measurement")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        let withA = arc.Process.setInput(proc, Some(EntityReference.Sample sampleA))
        let withB = arc.Process.setInput(withA, Some(EntityReference.Sample sampleB))
        Expect.equal (input withB).Id sampleB.Id "Returned value carries the new input"
        Expect.equal (input withA).Id sampleA.Id "Earlier result is unchanged"
        let current () = arc.Process.get(proc.Id.Value)
        arc.History.undo()
        Expect.equal (input (current())).Id sampleA.Id "Undo restores the previous input"
        arc.History.redo()
        Expect.equal (input (current())).Id sampleB.Id "Redo reapplies the input"
        use sql = PolyglotSQLite.Sqlite.OpenFile(arc.DatabasePath)
        let result = sql.Query("SELECT sample_id FROM process_io WHERE direction='input'")
        Expect.equal (result[0].Get(0).AsText()) sampleB.Id.Value "SQL follows endpoint changes"
        let cleared = arc.Process.setInput(current(), None)
        Expect.isNone cleared.Input "Cleared endpoint"
        arc.History.undo()
        Expect.equal (input (current())).Id sampleB.Id "Undo restores the cleared endpoint"

    testCase "saved root graph excludes standalone objects and SQL resumes them" <| fun _ ->
        let path, arc = create()
        let standalone = arc.Sample.create("standalone")
        let proc = arc.Process.create("measurement")
        let sample = arc.Sample.create("leaf")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInput(proc, Some(EntityReference.Sample sample)) |> ignore
        arc.save()
        let yaml = Path.readFileText(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)
        Expect.isFalse (yaml.Contains("standalone")) "Only the root graph is exported"
        Expect.isFalse arc.IsDirty "Exported graph is saved"
        Expect.isTrue arc.HasSessionOnlyObjects "Standalone registry is tracked separately"
        let renamed = arc.Sample.setName(standalone, "unsaved standalone")
        Expect.equal renamed.Name "unsaved standalone" "Setter returns the updated value"
        Expect.equal standalone.Name "standalone" "Held value is unchanged"
        let id = standalone.Id.Value
        arc.close()
        use reopened = ARC.openFolder(path, "auto")
        Expect.equal (reopened.Sample.get(id)).Name "unsaved standalone" "SQL working state resumes"
        reopened.History.undo()
        Expect.equal (reopened.Sample.get(id)).Name "standalone" "History survives reopening"
        reopened.History.redo()
        Expect.equal (reopened.Sample.get(id)).Name "unsaved standalone" "Redo survives reopening"

    testCase "create and close retain unsaved working state without writing YAML" <| fun _ ->
        let path, arc = create()
        let proc = arc.Process.create("unsaved")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        let id = proc.Id.Value
        arc.close()
        Expect.isFalse (Path.pathExists(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)) "Close is not save"
        use resumed = ARC.openFolder(path, "auto")
        Expect.equal (Seq.item 0 resumed.Model.Processes).Id (Some id) "Membership resumes"
        Expect.isTrue resumed.IsDirty "Root graph still awaits export"
        resumed.History.undo()
        Expect.equal (Seq.length resumed.Model.Processes) 0 "Membership undo survives reopening"
        resumed.History.undo()
        Expect.equal (resumed.Process.list()).Count 0 "Creation undo survives reopening"
        resumed.History.redo()
        Expect.equal (resumed.Process.get(id)).Name "unsaved" "Redo uses the same stable ID"

    testCase "standalone datasets and processes resume without being exported" <| fun _ ->
        let path, arc = create()
        let ds = arc.Dataset.create("standalone dataset")
        let proc = arc.Process.create("standalone process")
        let sample = arc.Sample.create("standalone sample")
        arc.Process.setInput(proc, Some(EntityReference.Sample sample)) |> ignore
        arc.Dataset.addProcess(ds, proc) |> ignore
        arc.save()
        let yaml = Path.readFileText(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)
        Expect.isFalse (yaml.Contains("standalone")) "Entire standalone subgraph stays in SQL"
        let dsId, procId, sampleId = ds.Id.Value, proc.Id.Value, sample.Id.Value
        arc.close()
        use resumed = ARC.openFolder(path, "auto")
        let restored = resumed.Dataset.get(dsId)
        Expect.equal (Seq.item 0 restored.Processes).Id (Some procId) "Process membership resumes"
        Expect.equal (input (resumed.Process.get(procId))).Id (Some sampleId) "Endpoint resumes"

    testCase "saving preserves undo redo and starting a new branch clears redo" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, Some("first")) |> ignore
        arc.save()
        arc.Dataset.setTitle(arc.Model, Some("second")) |> ignore
        arc.History.undo()
        Expect.isFalse arc.IsDirty "Undo returns to the saved graph"
        arc.close()
        use resumed = ARC.openFolder(path, "auto")
        Expect.isTrue resumed.History.CanRedo "Redo cursor survives reopening"
        resumed.History.redo()
        Expect.equal resumed.Model.Title (Some "second") "Redo restores edits"
        resumed.save()
        resumed.History.undo()
        Expect.isTrue resumed.IsDirty "Undo after save is an unsaved change"
        resumed.Dataset.setTitle(resumed.Model, Some("new branch")) |> ignore
        Expect.isFalse resumed.History.CanRedo "New command replaces abandoned redo"
        Expect.equal (count "history" resumed.DatabasePath) 2L "Two commands on active branch"

    testCase "ordered re-parenting and detach are reversible" <| fun _ ->
        let _, created = create()
        use arc = created
        let a = arc.Dataset.create("a")
        let b = arc.Dataset.create("b")
        let nested = arc.Dataset.create("nested")
        let parts (dataset: Dataset) = dataset.HasParts |> Seq.map (fun part -> part.Id.Value) |> List.ofSeq
        let processes (dataset: Dataset) = dataset.Processes |> Seq.map (fun proc -> proc.Id.Value) |> List.ofSeq
        let current (dataset: Dataset) = arc.Dataset.get(dataset.Id.Value)
        arc.Dataset.addPart(arc.Model, a) |> ignore
        arc.Dataset.addPart(arc.Model, b) |> ignore
        arc.Dataset.addPart(a, nested) |> ignore
        arc.Dataset.removePart(arc.Model, a) |> ignore
        arc.Dataset.addPart(b, a) |> ignore
        Expect.equal (parts arc.Model) [b.Id.Value] "Root holds b only"
        Expect.equal (parts (current b)) [a.Id.Value] "b holds a"
        arc.History.undo()
        arc.History.undo()
        Expect.equal (parts arc.Model) [a.Id.Value; b.Id.Value] "Order restored"
        Expect.equal (parts (current a)) [nested.Id.Value] "Nested part kept"
        let p1 = arc.Process.create("one")
        let p2 = arc.Process.create("two")
        arc.Dataset.addProcess(a, p1) |> ignore
        arc.Dataset.addProcess(a, p2) |> ignore
        arc.Dataset.removeProcess(a, p1) |> ignore
        arc.Dataset.addProcess(b, p1) |> ignore
        Expect.equal (processes (current a)) [p2.Id.Value] "a holds p2"
        Expect.equal (processes (current b)) [p1.Id.Value] "b holds p1"
        arc.History.undo()
        arc.History.undo()
        Expect.equal (processes (current a)) [p1.Id.Value; p2.Id.Value] "Process order restored"
        arc.Dataset.removeProcess(a, p1) |> ignore
        Expect.equal (arc.Process.get(p1.Id.Value)).Name "one" "Detached process stays registered"
        arc.History.undo()
        Expect.equal (processes (current a)) [p1.Id.Value; p2.Id.Value] "Detach undone"
        arc.Dataset.removePart(a, nested) |> ignore
        Expect.equal (Seq.length (arc.Dataset.get(nested.Id.Value)).Identifiers) 1 "Detached dataset stays registered"
        arc.History.undo()
        Expect.equal (parts (current a)) [nested.Id.Value] "Part detach undone"

    testCase "dataset cascade and sample deletion restore links and shared endpoints" <| fun _ ->
        let _, created = create()
        use arc = created
        let child = arc.Dataset.create("child")
        let nested = arc.Dataset.create("nested")
        let shared = arc.Sample.create("shared")
        let proc = arc.Process.create("nested process")
        let outside = arc.Process.create("outside process")
        arc.Dataset.addPart(arc.Model, child) |> ignore
        arc.Dataset.addPart(child, nested) |> ignore
        arc.Dataset.addProcess(nested, proc) |> ignore
        arc.Dataset.addProcess(arc.Model, outside) |> ignore
        arc.Process.setInput(proc, Some(EntityReference.Sample shared)) |> ignore
        arc.Process.setOutput(outside, Some(EntityReference.Sample shared)) |> ignore
        arc.Dataset.delete(child)
        Expect.throws (fun () -> arc.Dataset.get(nested.Id.Value) |> ignore) "Descendant is removed"
        Expect.throws (fun () -> arc.Process.get(proc.Id.Value) |> ignore) "Nested process is removed"
        Expect.equal (arc.Sample.get(shared.Id.Value)).Name "shared" "Shared sample survives"
        Expect.equal (output (arc.Process.get(outside.Id.Value))).Id shared.Id "Shared output survives"
        arc.History.undo()
        Expect.equal (Seq.item 0 arc.Model.HasParts).Id child.Id "Child restored"
        Expect.equal (Seq.item 0 (arc.Dataset.get(child.Id.Value)).HasParts).Id nested.Id "Nested restored"
        Expect.equal (Seq.item 0 (arc.Dataset.get(nested.Id.Value)).Processes).Id proc.Id "Process membership restored"
        Expect.equal (input (arc.Process.get(proc.Id.Value))).Id shared.Id "Input restored"
        arc.Sample.delete(shared)
        Expect.isNone (arc.Process.get(proc.Id.Value)).Input "Sample deletion clears input"
        Expect.isNone (arc.Process.get(outside.Id.Value)).Output "Sample deletion clears shared output"
        arc.History.undo()
        Expect.equal (input (arc.Process.get(proc.Id.Value))).Id shared.Id "Input link restored"
        Expect.equal (output (arc.Process.get(outside.Id.Value))).Id shared.Id "Output link restored"
        arc.Process.setOutput(outside, None) |> ignore
        Expect.isNone (arc.Process.get(outside.Id.Value)).Output "Output clear"
        arc.History.undo()
        Expect.equal (output (arc.Process.get(outside.Id.Value))).Id shared.Id "Output clear undone"
        arc.Process.delete(outside)
        Expect.equal (Seq.length arc.Model.Processes) 0 "Deletion removes membership"
        arc.History.undo()
        Expect.equal (Seq.item 0 arc.Model.Processes).Id outside.Id "Membership restored"

    testCase "YAML reload preserves supported fields duplicates strings and shared samples" <| fun _ ->
        let path = folder()
        let sample = Sample("leaf: # 1", id = "sample-id", additionalTypes = ["custom"; "custom"])
        let first = ARCBaseModel.Process("first", input = EntityReference.Sample sample, id = "p1")
        let second = ARCBaseModel.Process("second", output = EntityReference.Sample sample, id = "p2")
        let child = Dataset(["administrative"], ["child"], title = "", processes = [second])
        let root = Dataset(["process-provenance"; "semantic-designation"], ["id"; "id"],
                           title = "original", description = "line1\nline2", license = "license: 1",
                           datePublished = "2026-10-02", dateCreated = "", dateModified = "today",
                           hasParts = [child], processes = [first], id = "root-id", additionalTypes = ["custom"; "custom"])
        let arc = ARC.create(path, root)
        arc.save()
        arc.close()
        use reloaded = ARC.openFolder(path, "yml")
        let model = reloaded.Model
        Expect.equal model.Id (Some "root-id") "Supplied ID preserved"
        Expect.equal (Seq.toList model.Identifiers) ["id"; "id"] "Identifier duplicates preserved"
        Expect.equal (Seq.toList model.AdditionalTypes) ["custom"; "custom"] "Classification duplicates preserved"
        Expect.equal model.Description root.Description "Multiline description preserved"
        Expect.equal model.License root.License "Quoted text preserved"
        Expect.equal model.DatePublished root.DatePublished "Publication text preserved"
        Expect.equal model.DateCreated (Some "") "Empty text is not absent"
        Expect.equal model.DateModified root.DateModified "Modification text preserved"
        Expect.equal (Seq.item 0 model.HasParts).Title (Some "") "Empty title preserved"
        Expect.equal (input (Seq.item 0 model.Processes)).Id (output (Seq.item 0 (Seq.item 0 model.HasParts).Processes)).Id "Shared sample keeps one ID"
        Expect.equal (reloaded.Sample.list()).Count 1 "Shared sample resolves once"
        Expect.isFalse reloaded.History.CanUndo "Explicit YAML creates fresh history"
        Expect.isFalse reloaded.IsDirty "Reloaded graph matches its source"
        Expect.equal (archives path).Length 1 "Previous session archived"

    testCase "scalar edits clearing and invalid identifiers are reversible" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Dataset.setTitle(arc.Model, Some("")) |> ignore
        Expect.equal arc.Model.Title (Some "") "Empty title retained"
        arc.Dataset.setTitle(arc.Model, None) |> ignore
        Expect.isNone arc.Model.Title "Title cleared"
        arc.History.undo()
        Expect.equal arc.Model.Title (Some "") "Empty title restored"
        arc.Dataset.setDescription(arc.Model, Some("description")) |> ignore
        arc.Dataset.setDescription(arc.Model, None) |> ignore
        arc.History.undo()
        Expect.equal arc.Model.Description (Some "description") "Description restored"
        arc.Dataset.setIdentifiers(arc.Model, ["one"; "one"; "two"]) |> ignore
        Expect.equal (Seq.toList arc.Model.Identifiers) ["one"; "one"; "two"] "No deduplication"
        Expect.throws (fun () -> arc.Dataset.setIdentifiers(arc.Model, []) |> ignore) "Required identifiers remain required"
        Expect.equal (Seq.toList arc.Model.Identifiers) ["one"; "one"; "two"] "Failed command leaves state unchanged"
        let proc = arc.Process.create("name")
        arc.Process.setName(proc, "renamed") |> ignore
        arc.History.undo()
        Expect.equal (arc.Process.get(proc.Id.Value)).Name "name" "Process name restored"

    testCase "ID collisions and references of the wrong type fail without history" <| fun _ ->
        let _, created = create()
        use arc = created
        let proc = arc.Process.create("local")
        let foreign = Sample("foreign", id = proc.Id.Value)
        let child = arc.Dataset.create("child")
        arc.Dataset.addPart(arc.Model, child) |> ignore
        let commands = count "history" arc.DatabasePath
        Expect.throws (fun () -> arc.Process.setInput(proc, Some(EntityReference.Sample foreign)) |> ignore) "A sample reference to a process ID is rejected"
        Expect.throws (fun () -> arc.Sample.add(foreign) |> ignore) "Cross-kind ID collision rejected"
        Expect.throws (fun () -> arc.Dataset.delete(arc.Model)) "Root cannot be deleted"
        Expect.throws (fun () -> arc.Sample.setName(Sample("anonymous"), "oops") |> ignore) "A subject without an ID is rejected"
        Expect.throws (fun () -> arc.Sample.setName(Sample("missing", id = "unknown"), "oops") |> ignore) "A subject that is not stored is rejected"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed commands create no history"
        Expect.equal (Seq.item 0 arc.Model.HasParts).Id child.Id "Graph unchanged"

    testCase "invalid graph is rejected before assigning IDs" <| fun _ ->
        let _, created = create()
        use arc = created
        let sample = Sample("pending")
        let bad = ARCBaseModel.Process("bad", input = EntityReference.Sample sample, executesRecipe = Recipe("recipe"))
        bad.State <- { bad.State with Name = Unchecked.defaultof<string> }
        Expect.throws (fun () -> arc.Process.add(bad) |> ignore) "Missing required process name rejected"
        Expect.isNone bad.Id "No process ID assigned on failure"
        Expect.isNone sample.Id "No sample ID assigned on failure"
        Expect.equal (arc.Process.list()).Count 0 "Registry unchanged"

    testCase "SQL failure rolls back stored state and history" <| fun _ ->
        let _, created = create()
        use arc = created
        let sample = arc.Sample.create("original")
        let commands = count "history" arc.DatabasePath
        withSql arc.DatabasePath (fun sql ->
            sql.ExecuteScript("CREATE TRIGGER reject_sample BEFORE INSERT ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected failure'); END; CREATE TRIGGER reject_sample_update BEFORE UPDATE ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected failure'); END;"))
        Expect.throws (fun () -> arc.Sample.setName(sample, "reject") |> ignore) "SQL failure rejects command"
        Expect.equal (arc.Sample.get(sample.Id.Value)).Name "original" "Stored state rolls back"
        let pending = Sample("reject")
        Expect.throws (fun () -> arc.Sample.add(pending) |> ignore) "Registration failure"
        Expect.isNone pending.Id "Supplied input stays unchanged"
        Expect.equal (arc.Sample.list()).Count 1 "Failed registration rolls back registry"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed writes do not journal commands"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT name FROM sample").[0].Get(0).AsText()) "original" "SQL rolled back")
        arc.History.undo()
        Expect.equal (arc.Sample.list()).Count 0 "Previous history is usable"

    testCase "returned values stay unchanged after edits undo redo and closure" <| fun _ ->
        let _, arc = create()
        let before = arc.Model
        let sample = arc.Sample.create("first")
        let edited = arc.Dataset.setTitle(before, Some("managed"))
        let renamed = arc.Sample.setName(sample, "second")
        arc.History.undo()
        arc.History.undo()
        arc.History.redo()
        Expect.isNone before.Title "Earlier root value is unchanged"
        Expect.equal edited.Title (Some "managed") "Edit result is unchanged by undo"
        Expect.equal sample.Name "first" "Created value is unchanged"
        Expect.equal renamed.Name "second" "Renamed value is unchanged by undo"
        let operations = arc.Sample
        arc.close()
        arc.close()
        Expect.equal edited.Title (Some "managed") "Values survive closure"
        Expect.throws (fun () -> operations.create("closed") |> ignore) "Cached service cannot outlive session"
        Expect.throws (fun () -> operations.upsert(Sample("closed")) |> ignore) "Upsert cannot outlive session"
        Expect.throws (fun () -> arc.Model |> ignore) "Closed model access rejected"

    testCase "a no-op setter keeps revision history and the redo branch" <| fun _ ->
        let _, created = create()
        use arc = created
        let sample = arc.Sample.create("same")
        arc.Sample.setName(sample, "changed") |> ignore
        arc.History.undo()
        let commands = count "history" arc.DatabasePath
        let revision = withSql arc.DatabasePath (fun sql -> sql.Query("SELECT revision FROM arc").[0].Get(0).AsInteger())
        let result = arc.Sample.setName(sample, "same")
        Expect.equal result.Name "same" "No-op setter returns the stored value"
        arc.Dataset.setTitle(arc.Model, None) |> ignore
        Expect.isTrue arc.History.CanRedo "Redo branch survives no-op commands"
        Expect.equal (count "history" arc.DatabasePath) commands "No history row added"
        Expect.equal (withSql arc.DatabasePath (fun sql -> sql.Query("SELECT revision FROM arc").[0].Get(0).AsInteger())) revision "Revision unchanged"

    testCase "a setter on an older value preserves current unrelated fields" <| fun _ ->
        let _, created = create()
        use arc = created
        let older = arc.Dataset.setTitle(arc.Model, Some("title"))
        arc.Dataset.setDescription(older, Some("description")) |> ignore
        let result = arc.Dataset.setLicense(older, Some("license"))
        Expect.equal result.Title (Some "title") "Title kept"
        Expect.equal result.Description (Some "description") "Description set through another value is kept"
        Expect.equal result.License (Some "license") "License applied"
        Expect.isNone older.Description "The older value is unchanged"

    testCase "add rejects an existing ID and inserts new nested entities" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Sample.upsert(Sample("stored", id = "sample-1")) |> ignore
        let commands = count "history" arc.DatabasePath
        Expect.throws (fun () -> arc.Sample.add(Sample("again", id = "sample-1")) |> ignore) "Existing ID rejected"
        let proc = ARCBaseModel.Process("p", input = EntityReference.Sample(Sample("nested")))
        let stored = arc.Process.add(proc)
        Expect.isSome (input stored).Id "New nested entity inserted with an ID"
        Expect.equal (arc.Sample.list()).Count 2 "Nested insertion is stored"
        Expect.equal (count "history" arc.DatabasePath) (commands + 1L) "Failed add left no history"

    testCase "non-recursive upsert links stored nested entities and recursive upsert replaces them" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Sample.upsert(Sample("stored", id = "sample-1")) |> ignore
        let nested = Sample("changed", id = "sample-1")
        let proc = ARCBaseModel.Process("p", id = "process-1", input = EntityReference.Sample nested)
        arc.Process.upsert(proc) |> ignore
        Expect.equal (arc.Sample.get("sample-1")).Name "stored" "Stored nested entity is only linked"
        Expect.equal (input (arc.Process.get("process-1"))).Name "stored" "The link resolves to the stored entity"
        arc.Process.upsert(proc, true) |> ignore
        Expect.equal (arc.Sample.get("sample-1")).Name "changed" "Recursive upsert writes the nested entity"
        let again = arc.Process.upsert(ARCBaseModel.Process("p2", id = "process-1"), recursive = false)
        Expect.equal again.Name "p2" "The root is always replaced"
        Expect.isNone again.Input "Replacement drops the old input"

    testCase "distinct objects with one ID must carry equal definitions" <| fun _ ->
        let _, created = create()
        use arc = created
        let commands = count "history" arc.DatabasePath
        let first = Sample("one", id = "shared")
        let second = Sample("two", id = "shared")
        let proc = ARCBaseModel.Process("p", input = EntityReference.Sample first, output = EntityReference.Sample second)
        Expect.throws (fun () -> arc.Process.add(proc) |> ignore) "Conflicting definitions rejected"
        let equal = ARCBaseModel.Process("p", input = EntityReference.Sample(Sample("one", id = "shared")), output = EntityReference.Sample(Sample("one", id = "shared")))
        let stored = arc.Process.add(equal)
        Expect.equal (input stored).Id (output stored).Id "Equal definitions share one row"
        Expect.equal (arc.Sample.list()).Count 1 "Stored once"
        Expect.equal (count "history" arc.DatabasePath) (commands + 1L) "Only the valid command is journaled"

    testCase "automatic selection reloads external YAML only for a clean session" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, Some("original")) |> ignore
        arc.save()
        arc.close()
        externalTitle path "external"
        use resumed = ARC.openFolder(path, "auto")
        Expect.equal resumed.Model.Title (Some "external") "Clean session reloads external YAML"
        Expect.isFalse resumed.History.CanUndo "Reload starts fresh history"
        Expect.equal (archives path).Length 1 "Displaced session archived"
        Expect.equal (archivedHistory path (archives path).[0]) 1L "Archive preserves old history"

    testCase "external changes conflict with pending edits and explicit SQL preserves them" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, Some("original")) |> ignore
        arc.save()
        arc.Dataset.setTitle(arc.Model, Some("working")) |> ignore
        arc.close()
        externalTitle path "external"
        Expect.throws (fun () -> ARC.openFolder(path, "auto") |> ignore) "Pending edits cannot be displaced"
        Expect.equal (archives path).Length 0 "Conflict does not archive"
        use resumed = ARC.openFolder(path, "sql")
        Expect.equal resumed.Model.Title (Some "working") "Explicit SQL resumes working state"
        resumed.History.undo()
        Expect.equal resumed.Model.Title (Some "original") "Undo history retained"
        Expect.isTrue resumed.IsDirty "Chosen SQL graph still needs filesystem publication"
        let yaml = Path.combineNative path ARCtrl.Helper.Layout.ARCFileName
        Path.appendFileText yaml "\n# another external edit\n"
        Expect.throws (fun () -> resumed.save()) "Further external edits reject save"

    testCase "a second connection sees committed edits and its writes apply to the refreshed state" <| fun _ ->
        let path, first = create()
        use first = first
        use second = ARC.openFolder(path, "auto")
        first.Dataset.setTitle(first.Model, Some("first writer")) |> ignore
        Expect.equal second.Model.Title (Some "first writer") "The next read refreshes the second connection"
        second.Dataset.setDescription(second.Model, Some("second writer")) |> ignore
        Expect.equal first.Model.Title (Some "first writer") "The first edit survives the second write"
        Expect.equal first.Model.Description (Some "second writer") "The first connection sees the second write"
        let stale = second.Model
        first.Dataset.setLicense(first.Model, Some("license")) |> ignore
        let result = second.Dataset.setTitle(stale, Some("third writer"))
        Expect.equal result.License (Some "license") "A write from an older value starts from the refreshed state"
        Expect.isTrue second.History.CanUndo "The second connection recorded history"
        first.save()
        Expect.isFalse first.IsDirty "Save publishes the refreshed state"

    testCase "initial invalid graph leaves no broken session database" <| fun _ ->
        let path = folder()
        let root = Dataset(["process-provenance"], ["example"], dataFiles = [Data("file", "file.txt")])
        let file = Seq.item 0 root.DataFiles
        file.State <- { file.State with Path = Unchecked.defaultof<string> }
        Expect.throws (fun () -> ARC.create(path, root) |> ignore) "Invalid initial graph rejected"
        Expect.isFalse (Path.pathExists(Path.combineNative (Path.combineNative path ARCtrl.Helper.Layout.ARCConfigFolderName) "arc.sqlite")) "Failed creation removes its own database"
        Expect.isNone root.Id "Failed initial graph leaves root ID optional"

    testCase "missing sources and invalid source preferences do not create state" <| fun _ ->
        let path = folder()
        for source in ["auto"; "sql"; "yml"; "invalid"] do
            Expect.throws (fun () -> ARC.openFolder(path, source) |> ignore) "Missing or invalid source rejected"
        Expect.isFalse (Path.directoryExists(Path.combineNative path ARCtrl.Helper.Layout.ARCConfigFolderName)) "Read failures do not initialize storage"

    testCase "session-only objects conflict even when the root graph is clean" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, Some("original")) |> ignore
        let sample = arc.Sample.create("standalone")
        let id = sample.Id.Value
        arc.save()
        arc.close()
        externalTitle path "external"
        Expect.throws (fun () -> ARC.openFolder(path, "auto") |> ignore) "Standalone data must not be displaced"
        let resumed = ARC.openFolder(path, "sql")
        Expect.equal (resumed.Sample.get(id)).Name "standalone" "Explicit SQL preserves standalone object"
        Expect.isTrue resumed.IsDirty "External version is not falsely marked exported"
        resumed.save()
        Expect.isFalse resumed.IsDirty "Explicit save publishes chosen state"
        resumed.close()
        use reopened = ARC.openFolder(path, "auto")
        Expect.equal reopened.Model.Title (Some "original") "Chosen state was exported"
        Expect.equal (reopened.Sample.get(id)).Name "standalone" "Save retains session-only data"

    testCase "explicit YAML archives pending objects and histories under unique names" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, Some("original")) |> ignore
        arc.save()
        arc.Sample.create("retained in archive") |> ignore
        arc.Dataset.setTitle(arc.Model, Some("unsaved")) |> ignore
        arc.close()
        externalTitle path "external"
        let reloaded = ARC.openFolder(path, "yml")
        Expect.equal reloaded.Model.Title (Some "external") "YAML preference replaces working state"
        Expect.equal (reloaded.Sample.list()).Count 0 "Fresh registry"
        Expect.isFalse reloaded.History.CanUndo "Fresh history"
        reloaded.close()
        use second = ARC.openFolder(path, "yml")
        Expect.equal (archives path).Length 2 "Archive names are unique"
        Expect.isTrue (archivedSamples path) "Standalone data retained in archive"

    testCase "invalid YAML is validated before archiving a retained session" <| fun _ ->
        let path, arc = create()
        arc.save()
        arc.close()
        Path.writeFileText (Path.combineNative path ARCtrl.Helper.Layout.ARCFileName) "type: Dataset\nunknown: unsupported\n"
        Expect.throws (fun () -> ARC.openFolder(path, "yml") |> ignore) "Unsupported YAML rejected"
        Expect.equal (archives path).Length 0 "Original SQL not displaced"
        use retained = ARC.openFolder(path, "sql")
        Expect.equal (Seq.toList retained.Model.Identifiers) ["example"] "SQL remains intact"
        retained.save()

    testCase "failed saves do not advance checkpoints or discard pending edits" <| fun _ ->
        let path, created = create()
        use arc = created
        arc.Dataset.setTitle(arc.Model, Some("original")) |> ignore
        arc.save()
        let yaml = Path.combineNative path ARCtrl.Helper.Layout.ARCFileName
        let baseline = Path.readFileText(yaml)
        arc.Dataset.setTitle(arc.Model, Some("edited")) |> ignore
        withSql arc.DatabasePath (fun sql ->
            sql.ExecuteScript("CREATE TRIGGER fail_save BEFORE UPDATE OF baseline ON arc BEGIN SELECT RAISE(ABORT,'injected save failure'); END;"))
        Expect.throws (fun () -> arc.save()) "Injected SQL checkpoint failure"
        Expect.isTrue arc.IsDirty "Failed save leaves edits pending"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT baseline FROM arc").[0].Get(0).AsText()) baseline "Checkpoint is not advanced"
            sql.Execute("DROP TRIGGER fail_save"))
        Expect.equal (temporaryFiles path).Length 0 "Temporary output cleaned up"
        Expect.throws (fun () -> arc.save()) "Published file with failed SQL commit requires source reselection"
        arc.close()
        use resumed = ARC.openFolder(path, "sql")
        resumed.save()
        Expect.isFalse resumed.IsDirty "Explicit source choice recovers failed save"

    testCase "filesystem write failure leaves SQL checkpoint unchanged" <| fun _ ->
        let path, created = create()
        use arc = created
        Path.createDirectory(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName) |> ignore
        Expect.throws (fun () -> arc.save()) "Cannot replace a directory with YAML"
        Expect.isTrue arc.IsDirty "Graph remains unsaved"
        withSql arc.DatabasePath (fun sql ->
            Expect.isTrue (sql.Query("SELECT baseline FROM arc").[0].Get(0).IsNull) "No checkpoint established")
        Expect.equal (temporaryFiles path).Length 0 "Temporary output removed"

    testCase "sample upsert inserts with supplied or assigned IDs as one reversible command" <| fun _ ->
        let _, created = create()
        use arc = created
        let supplied = Sample("supplied", id = "sample-1", additionalTypes = ["custom"; "custom"])
        let assigned = Sample("assigned")
        let first = arc.Sample.upsert(supplied)
        let second = arc.Sample.upsert(assigned)
        Expect.equal first.Id (Some "sample-1") "Supplied ID retained"
        Expect.isNone assigned.Id "Supplied input keeps its absent ID"
        Expect.isSome second.Id "Missing ID assigned to the stored copy"
        Expect.equal (arc.Sample.get("sample-1")).Name "supplied" "Supplied sample resolves"
        Expect.equal (arc.Sample.get(second.Id.Value)).Name "assigned" "Assigned sample resolves"
        Expect.equal (count "sample" arc.DatabasePath) 2L "Both insertions written to SQL"
        withSql arc.DatabasePath (fun sql ->
            let rows = sql.Query("SELECT kind FROM history ORDER BY sequence")
            Expect.equal (rows |> Seq.map (fun row -> row.Get(0).AsText()) |> Seq.toList)
                ["Sample.set"; "Sample.set"] "One upsert command per insertion")
        let id = second.Id.Value
        arc.History.undo()
        Expect.equal (arc.Sample.list()).Count 1 "Undo removes the inserted registration"
        arc.History.redo()
        Expect.equal (arc.Sample.get(id)).Name "assigned" "Redo restores the same ID"
        arc.History.undo()
        arc.History.undo()
        Expect.equal (count "sample" arc.DatabasePath) 0L "SQL insertion undone"
        arc.History.redo()
        Expect.equal (Seq.toList (arc.Sample.get("sample-1")).AdditionalTypes) ["custom"; "custom"] "Duplicates restored"

    testCase "sample upsert replaces values while keeping shared references" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Sample.upsert(Sample("original", id = "sample-1", additionalTypes = ["old"; "old"])) |> ignore
        let canonical = arc.Sample.get("sample-1")
        let proc = arc.Process.create("measurement")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInput(proc, Some(EntityReference.Sample canonical)) |> ignore
        arc.Process.setOutput(proc, Some(EntityReference.Sample canonical)) |> ignore
        let replacement = Sample("updated", id = "sample-1", additionalTypes = ["new"; "new"; "other"])
        let commands = count "history" arc.DatabasePath
        arc.Sample.upsert(replacement) |> ignore
        Expect.equal (count "history" arc.DatabasePath) (commands + 1L) "Replacement is one command"
        Expect.equal (arc.Sample.list()).Count 1 "No duplicate registered object"
        let current = arc.Process.get(proc.Id.Value)
        Expect.equal (input current).Id canonical.Id "Input still points to the entity"
        Expect.equal (output current).Id canonical.Id "Output still points to the entity"
        Expect.equal (input current).Name "updated" "Linked entity shows the new values"
        Expect.equal canonical.Name "original" "Previously returned value is unchanged"
        Expect.equal (Seq.toList canonical.AdditionalTypes) ["old"; "old"] "Previously returned collection is unchanged"
        Expect.equal (Seq.toList (arc.Sample.get("sample-1")).AdditionalTypes) ["new"; "new"; "other"] "Types replaced, not merged"
        replacement.State <- { replacement.State with Name = "later caller edit" }
        Expect.equal (arc.Sample.get("sample-1")).Name "updated" "Later edits of the supplied object do not reach the store"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT name FROM sample").[0].Get(0).AsText()) "updated" "SQL value replaced")
        arc.History.undo()
        Expect.equal (arc.Sample.get("sample-1")).Name "original" "Prior scalar restored"
        Expect.equal (Seq.toList (arc.Sample.get("sample-1")).AdditionalTypes) ["old"; "old"] "Prior collection restored"
        arc.History.redo()
        Expect.equal (arc.Sample.get("sample-1")).Name "updated" "Recorded values, not later caller edits, replayed"
        arc.Sample.upsert(Sample("", id = "sample-1")) |> ignore
        Expect.equal (arc.Sample.get("sample-1")).Name "" "Empty name preserved"
        Expect.equal (Seq.length (arc.Sample.get("sample-1")).AdditionalTypes) 0 "Empty collection clears earlier values"
        arc.History.undo()
        Expect.equal (Seq.toList (arc.Sample.get("sample-1")).AdditionalTypes) ["new"; "new"; "other"] "Clearing is reversible"

    testCase "sample upsert history and pending edits recover across reopening" <| fun _ ->
        let path, created = create()
        use arc = created
        let sample = arc.Sample.upsert(Sample("original", id = "sample-1"))
        let proc = arc.Process.create("measurement")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInput(proc, Some(EntityReference.Sample sample)) |> ignore
        arc.save()
        arc.Sample.upsert(Sample("updated", id = "sample-1", additionalTypes = ["custom"; "custom"])) |> ignore
        Expect.isTrue arc.IsDirty "Linked sample replacement awaits filesystem export"
        arc.close()
        use resumed = ARC.openFolder(path, "auto")
        Expect.equal (resumed.Sample.get("sample-1")).Name "updated" "SQL working state recovered"
        Expect.equal (input (Seq.item 0 resumed.Model.Processes)).Name "updated" "Link resolves to the replacement"
        resumed.History.undo()
        Expect.equal (resumed.Sample.get("sample-1")).Name "original" "Recovered history undoes replacement"
        Expect.isFalse resumed.IsDirty "Undo returns to saved root"
        resumed.close()
        use reopened = ARC.openFolder(path, "auto")
        Expect.isTrue reopened.History.CanRedo "Redo survives another restart"
        reopened.History.redo()
        let redone = reopened.Sample.get("sample-1")
        Expect.equal redone.Name "updated" "Recovered replacement redone"
        Expect.equal (Seq.toList redone.AdditionalTypes) ["custom"; "custom"] "SQL snapshots retain duplicate values"
        Expect.equal (input (Seq.item 0 reopened.Model.Processes)).Id redone.Id "Link follows the entity"

    testCase "sample upsert can insert a deleted ID again" <| fun _ ->
        let _, created = create()
        use arc = created
        let canonical = arc.Sample.upsert(Sample("original", id = "sample-1"))
        arc.Sample.delete(canonical)
        arc.Sample.upsert(Sample("replacement", id = "sample-1")) |> ignore
        Expect.equal (arc.Sample.get("sample-1")).Name "replacement" "Deleted ID is inserted again"
        arc.History.undo()
        Expect.equal (count "sample" arc.DatabasePath) 0L "Reactivation undone"
        arc.History.undo()
        Expect.equal (arc.Sample.get("sample-1")).Name "original" "Original deletion can still be undone"
        arc.History.redo()
        arc.History.redo()
        Expect.equal (arc.Sample.get("sample-1")).Name "replacement" "Reactivation redone"
        Expect.equal canonical.Name "original" "The earlier value is unchanged"

    testCase "sample upsert rejects invalid inputs and cross-type IDs" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Sample.upsert(Sample("original", id = "sample-1")) |> ignore
        let proc = arc.Process.create("measurement")
        let commands = count "history" arc.DatabasePath
        Expect.throws (fun () -> arc.Sample.upsert(Sample("collision", id = arc.Model.Id.Value)) |> ignore) "Dataset ID collision rejected"
        Expect.throws (fun () -> arc.Sample.upsert(Sample("collision", id = proc.Id.Value)) |> ignore) "Process ID collision rejected"
        Expect.throws (fun () -> arc.Sample.upsert(Unchecked.defaultof<Sample>) |> ignore) "Null input rejected"
        let invalid = Sample("invalid")
        invalid.State <- { invalid.State with Name = Unchecked.defaultof<string> }
        Expect.throws (fun () -> arc.Sample.upsert(invalid) |> ignore) "Null name rejected"
        Expect.isNone invalid.Id "Validation failure does not assign an ID"
        let property = Annotation("property")
        property.State <- { property.State with Name = Unchecked.defaultof<string> }
        let invalid = Sample("invalid", id = "sample-1", additionalProperties = [property])
        Expect.throws (fun () -> arc.Sample.upsert(invalid) |> ignore) "Invalid nested properties are rejected"
        let invalidTypes = Sample("invalid", id = "sample-1", additionalTypes = [Unchecked.defaultof<string>])
        Expect.throws (fun () -> arc.Sample.upsert(invalidTypes) |> ignore) "Null collection values rejected"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed upserts create no history"
        Expect.equal (arc.Sample.get("sample-1")).Name "original" "Stored values unchanged"

    testCase "sample upsert SQL failure rolls back updates insertions and assigned IDs" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Sample.upsert(Sample("original", id = "sample-1", additionalTypes = ["old"])) |> ignore
        let commands = count "history" arc.DatabasePath
        withSql arc.DatabasePath (fun sql ->
            sql.ExecuteScript("CREATE TRIGGER reject_upsert BEFORE INSERT ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected failure'); END; CREATE TRIGGER reject_upsert_update BEFORE UPDATE ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected failure'); END;"))
        Expect.throws (fun () -> arc.Sample.upsert(Sample("reject", id = "sample-1", additionalTypes = ["new"])) |> ignore) "Replacement SQL failure"
        Expect.equal (arc.Sample.get("sample-1")).Name "original" "Failed update leaves stored values unchanged"
        Expect.equal (Seq.toList (arc.Sample.get("sample-1")).AdditionalTypes) ["old"] "Failed update keeps collection values"
        let pending = Sample("reject")
        Expect.throws (fun () -> arc.Sample.upsert(pending) |> ignore) "Insertion SQL failure"
        Expect.isNone pending.Id "Supplied input stays unchanged"
        Expect.equal (arc.Sample.list()).Count 1 "Failed insertion leaves registry unchanged"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed SQL creates no upsert history"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT name FROM sample").[0].Get(0).AsText()) "original" "SQL transaction rolled back")
        arc.History.undo()
        Expect.equal (arc.Sample.list()).Count 0 "Existing history remains usable"

    testCase "sample upsert branches history without dirtying a saved standalone root" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.save()
        let sample = arc.Sample.upsert(Sample("original"))
        let id = sample.Id.Value
        arc.History.undo()
        Expect.isTrue arc.History.CanRedo "Insertion can be redone"
        arc.Sample.upsert(Sample("new branch", id = id)) |> ignore
        Expect.isFalse arc.History.CanRedo "New upsert clears the redo branch"
        Expect.equal (arc.Sample.get(id)).Name "new branch" "The ID is reused on the new branch"
        Expect.isFalse arc.IsDirty "Standalone upsert does not change the saved root graph"
        Expect.isTrue arc.HasSessionOnlyObjects "Standalone state is tracked separately"
]
