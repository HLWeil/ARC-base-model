module ManagementPrototype.Tests.Behavior

open System
open System.IO
open ARCBaseModel
open ARCtrl
open Fable.Pyxpecto

let private folder () =
    let root = Path.GetFullPath("build/out/management-prototype/tests")
    let path = Path.Combine(root, Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(path) |> ignore
    path

let private create () =
    let path = folder()
    path, ARC.create(path, Dataset(["process-provenance"], ["example"]))

let private same actual expected = Expect.isTrue (Object.ReferenceEquals(actual, expected)) "Shared object identity"
let private input (proc: ARCBaseModel.Process) =
    match proc.Input with
    | Some(EntityReference.Sample sample) -> sample
    | _ -> failwith "Expected sample input"

let private output (proc: ARCBaseModel.Process) =
    match proc.Output with
    | Some(EntityReference.Sample sample) -> sample
    | _ -> failwith "Expected sample output"

let private withSql path action =
    let options = Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
    options.DataSource <- path
    options.Pooling <- false
    use handle = new Microsoft.Data.Sqlite.SqliteConnection(options.ToString())
    handle.Open()
    use sql = PolyglotSQLite.Sqlite.WrapConnection(handle)
    action sql

let private count table path =
    withSql path (fun sql -> sql.Query("SELECT count(*) FROM " + table).[0].Get(0).AsInteger())

let private archives path = Directory.GetFiles(Path.Combine(path, ".arc"), "testing-*.sqlite")

let private externalTitle path title =
    let yaml = Path.Combine(path, "arc.yml")
    File.WriteAllText(yaml, File.ReadAllText(yaml).Replace("title: original", "title: " + title))

let tests = testList "Management prototype" [
    testCase "create adopts supplied graph and retains references" <| fun _ ->
        let sample = Sample("leaf")
        let proc = ARCBaseModel.Process("measurement", input = EntityReference.Sample sample)
        let root = Dataset(["process-provenance"], ["example"], processes = [proc])
        use arc = ARC.create(folder(), root)
        same arc.Model root
        same (arc.Process.get(proc.Id.Value)) proc
        same (arc.Sample.get(sample.Id.Value)) sample
        Expect.isFalse arc.History.CanUndo "Initialization is not a user operation"

    testCase "endpoint editing is reversible and SQL mirrors the graph" <| fun _ ->
        let _, created = create()
        use arc = created
        let sampleA = arc.Sample.create("A")
        let sampleB = arc.Sample.create("B")
        let proc = arc.Process.create("measurement")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInputSample(proc, sampleA) |> ignore
        arc.Process.setInputSample(proc, sampleB) |> ignore
        same (input proc) sampleB
        arc.History.undo()
        same (input proc) sampleA
        arc.History.redo()
        same (input proc) sampleB
        use sql = PolyglotSQLite.Sqlite.OpenFile(arc.DatabasePath)
        let result = sql.Query("SELECT input_sample_id FROM process")
        Expect.equal (result[0].Get(0).AsText()) sampleB.Id.Value "SQL follows endpoint changes"
        arc.Process.clearInput(proc) |> ignore
        Expect.isNone proc.Input "Cleared endpoint"
        arc.History.undo()
        same (input proc) sampleB

    testCase "saved root graph excludes standalone objects and SQL resumes them" <| fun _ ->
        let path, arc = create()
        let standalone = arc.Sample.create("standalone")
        let proc = arc.Process.create("measurement")
        let sample = arc.Sample.create("leaf")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInputSample(proc, sample) |> ignore
        arc.save()
        let yaml = File.ReadAllText(Path.Combine(path, "arc.yml"))
        Expect.isFalse (yaml.Contains("standalone")) "Only the root graph is exported"
        Expect.isFalse arc.IsDirty "Exported graph is saved"
        Expect.isTrue arc.HasSessionOnlyObjects "Standalone registry is tracked separately"
        arc.Sample.setName(standalone, "unsaved standalone") |> ignore
        let id = standalone.Id.Value
        arc.close()
        use reopened = ARC.openFolder(path, "auto")
        let restored = reopened.Sample.get(id)
        Expect.equal restored.Name "unsaved standalone" "SQL working state resumes"
        reopened.History.undo()
        Expect.equal restored.Name "standalone" "History survives reopening"
        reopened.History.redo()
        Expect.equal restored.Name "unsaved standalone" "Redo survives reopening"

    testCase "create and close retain unsaved working state without writing YAML" <| fun _ ->
        let path, arc = create()
        let proc = arc.Process.create("unsaved")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        let id = proc.Id.Value
        arc.close()
        Expect.isFalse (File.Exists(Path.Combine(path, "arc.yml"))) "Close is not save"
        use resumed = ARC.openFolder(path, "auto")
        same (resumed.Process.get(id)) resumed.Model.Processes[0]
        Expect.isTrue resumed.IsDirty "Root graph still awaits export"
        resumed.History.undo()
        Expect.equal resumed.Model.Processes.Count 0 "Membership undo survives reopening"
        resumed.History.undo()
        Expect.equal (resumed.Process.list()).Count 0 "Creation undo survives reopening"
        resumed.History.redo()
        Expect.equal (resumed.Process.get(id)).Name "unsaved" "Redo uses the same stable ID"

    testCase "standalone datasets and processes resume without being exported" <| fun _ ->
        let path, arc = create()
        let ds = arc.Dataset.create("standalone dataset")
        let proc = arc.Process.create("standalone process")
        let sample = arc.Sample.create("standalone sample")
        arc.Process.setInputSample(proc, sample) |> ignore
        arc.Dataset.addProcess(ds, proc) |> ignore
        arc.save()
        let yaml = File.ReadAllText(Path.Combine(path, "arc.yml"))
        Expect.isFalse (yaml.Contains("standalone")) "Entire standalone subgraph stays in SQL"
        let dsId, procId, sampleId = ds.Id.Value, proc.Id.Value, sample.Id.Value
        arc.close()
        use resumed = ARC.openFolder(path, "auto")
        let restored = resumed.Dataset.get(dsId)
        same restored.Processes[0] (resumed.Process.get(procId))
        same (input restored.Processes[0]) (resumed.Sample.get(sampleId))

    testCase "saving preserves undo redo and starting a new branch clears redo" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, "first") |> ignore
        arc.save()
        arc.Dataset.setTitle(arc.Model, "second") |> ignore
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
        resumed.Dataset.setTitle(resumed.Model, "new branch") |> ignore
        Expect.isFalse resumed.History.CanRedo "New command replaces abandoned redo"
        Expect.equal (count "history" resumed.DatabasePath) 2L "Two commands on active branch"
        Expect.equal (count "journal" resumed.DatabasePath) 6L "Audit retains undo redo and abandoned branch"

    testCase "ordered moves and detach are reversible" <| fun _ ->
        let _, created = create()
        use arc = created
        let a = arc.Dataset.create("a")
        let b = arc.Dataset.create("b")
        let nested = arc.Dataset.create("nested")
        arc.Dataset.addPart(arc.Model, a) |> ignore
        arc.Dataset.addPart(arc.Model, b) |> ignore
        arc.Dataset.addPart(a, nested) |> ignore
        arc.Dataset.movePart(a, b) |> ignore
        same arc.Model.HasParts[0] b
        same b.HasParts[0] a
        arc.History.undo()
        same arc.Model.HasParts[0] a
        same arc.Model.HasParts[1] b
        same a.HasParts[0] nested
        let p1 = arc.Process.create("one")
        let p2 = arc.Process.create("two")
        arc.Dataset.addProcess(a, p1) |> ignore
        arc.Dataset.addProcess(a, p2) |> ignore
        arc.Dataset.moveProcess(p1, b) |> ignore
        same a.Processes[0] p2
        same b.Processes[0] p1
        arc.History.undo()
        same a.Processes[0] p1
        same a.Processes[1] p2
        arc.Dataset.removeProcess(a, p1) |> ignore
        same (arc.Process.get(p1.Id.Value)) p1
        arc.History.undo()
        same a.Processes[0] p1
        arc.Dataset.removePart(a, nested) |> ignore
        same (arc.Dataset.get(nested.Id.Value)) nested
        arc.History.undo()
        same a.HasParts[0] nested

    testCase "dataset cascade and sample deletion restore identity and shared endpoints" <| fun _ ->
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
        arc.Process.setInputSample(proc, shared) |> ignore
        arc.Process.setOutputSample(outside, shared) |> ignore
        arc.Dataset.delete(child) |> ignore
        Expect.throws (fun () -> arc.Dataset.get(nested.Id.Value) |> ignore) "Descendant is removed"
        Expect.throws (fun () -> arc.Process.get(proc.Id.Value) |> ignore) "Nested process is removed"
        same (arc.Sample.get(shared.Id.Value)) shared
        same (output outside) shared
        arc.History.undo()
        same arc.Model.HasParts[0] child
        same child.HasParts[0] nested
        same nested.Processes[0] proc
        same (input proc) shared
        arc.Sample.delete(shared) |> ignore
        Expect.isNone proc.Input "Sample deletion clears input"
        Expect.isNone outside.Output "Sample deletion clears shared output"
        arc.History.undo()
        same (input proc) shared
        same (output outside) shared
        arc.Process.clearOutput(outside) |> ignore
        Expect.isNone outside.Output "Output clear"
        arc.History.undo()
        same (output outside) shared
        arc.Process.delete(outside) |> ignore
        Expect.equal arc.Model.Processes.Count 0 "Deletion removes membership"
        arc.History.undo()
        same arc.Model.Processes[0] outside

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
        Expect.equal model.HasParts[0].Title (Some "") "Empty title preserved"
        same (input model.Processes[0]) (output model.HasParts[0].Processes[0])
        Expect.equal (reloaded.Sample.list()).Count 1 "Shared sample resolves once"
        Expect.isFalse reloaded.History.CanUndo "Explicit YAML creates fresh history"
        Expect.isFalse reloaded.IsDirty "Reloaded graph matches its source"
        Expect.equal (archives path).Length 1 "Previous session archived"

    testCase "scalar edits clearing and invalid identifiers are reversible" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.Dataset.setTitle(arc.Model, "") |> ignore
        Expect.equal arc.Model.Title (Some "") "Empty title retained"
        arc.Dataset.clearTitle(arc.Model) |> ignore
        Expect.isNone arc.Model.Title "Title cleared"
        arc.History.undo()
        Expect.equal arc.Model.Title (Some "") "Empty title restored"
        arc.Dataset.setDescription(arc.Model, "description") |> ignore
        arc.Dataset.clearDescription(arc.Model) |> ignore
        arc.History.undo()
        Expect.equal arc.Model.Description (Some "description") "Description restored"
        arc.Dataset.setIdentifiers(arc.Model, ["one"; "one"; "two"]) |> ignore
        Expect.equal (Seq.toList arc.Model.Identifiers) ["one"; "one"; "two"] "No deduplication"
        Expect.throws (fun () -> arc.Dataset.setIdentifiers(arc.Model, []) |> ignore) "Required identifiers remain required"
        Expect.equal (Seq.toList arc.Model.Identifiers) ["one"; "one"; "two"] "Failed command leaves state unchanged"
        let proc = arc.Process.create("name")
        arc.Process.setName(proc, "renamed") |> ignore
        arc.History.undo()
        Expect.equal proc.Name "name" "Process name restored"

    testCase "ownership membership cycles and ID collisions fail without history" <| fun _ ->
        let _, created = create()
        use arc = created
        let proc = arc.Process.create("local")
        let foreign = Sample("foreign", id = proc.Id.Value)
        let child = arc.Dataset.create("child")
        arc.Dataset.addPart(arc.Model, child) |> ignore
        let commands = count "history" arc.DatabasePath
        Expect.throws (fun () -> arc.Process.setInputSample(proc, foreign) |> ignore) "Foreign object rejected"
        Expect.throws (fun () -> arc.Sample.register(foreign) |> ignore) "Cross-kind ID collision rejected"
        Expect.throws (fun () -> arc.Dataset.addPart(child, arc.Model) |> ignore) "Root cannot be nested"
        Expect.throws (fun () -> arc.Dataset.addPart(child, child) |> ignore) "Cycle rejected"
        Expect.throws (fun () -> arc.Dataset.addPart(arc.Model, child) |> ignore) "Duplicate membership rejected"
        Expect.throws (fun () -> arc.Dataset.delete(arc.Model) |> ignore) "Root cannot be deleted"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed commands create no history"
        let fake = ARCBaseModel.Process("fake", id = proc.Id.Value)
        Expect.throws (fun () -> arc.Process.setName(fake, "oops") |> ignore) "Same ID does not confer ownership"
        same arc.Model.HasParts[0] child

    testCase "unsupported graph and cycles are rejected before assigning IDs" <| fun _ ->
        let _, created = create()
        use arc = created
        let sample = Sample("pending")
        let bad = ARCBaseModel.Process("bad", input = EntityReference.Sample sample, executesRecipe = Recipe("recipe"))
        Expect.throws (fun () -> arc.Process.register(bad) |> ignore) "Unsupported recipe rejected"
        Expect.isNone bad.Id "No process ID assigned on failure"
        Expect.isNone sample.Id "No sample ID assigned on failure"
        let cycle = Dataset(["process-provenance"], ["cycle"])
        cycle.HasParts.Add(cycle)
        Expect.throws (fun () -> arc.Dataset.register(cycle) |> ignore) "Cyclic graph rejected"
        Expect.isNone cycle.Id "No Dataset ID assigned on failure"
        Expect.equal (arc.Process.list()).Count 0 "Registry unchanged"

    testCase "SQL failure rolls back model registry IDs and history" <| fun _ ->
        let _, created = create()
        use arc = created
        let sample = arc.Sample.create("original")
        let commands = count "history" arc.DatabasePath
        withSql arc.DatabasePath (fun sql ->
            sql.ExecuteScript("CREATE TRIGGER reject_sample BEFORE INSERT ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected failure'); END;"))
        Expect.throws (fun () -> arc.Sample.setName(sample, "reject") |> ignore) "SQL failure rejects command"
        Expect.equal sample.Name "original" "Live model rolls back"
        let pending = Sample("reject")
        Expect.throws (fun () -> arc.Sample.register(pending) |> ignore) "Registration failure"
        Expect.isNone pending.Id "Failed registration restores optional ID"
        Expect.equal (arc.Sample.list()).Count 1 "Failed registration rolls back registry"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed writes do not journal commands"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT name FROM sample").[0].Get(0).AsText()) "original" "SQL rolled back")
        arc.History.undo()
        Expect.equal (arc.Sample.list()).Count 0 "Previous history is usable"

    testCase "direct mutation and closed sessions reject commands" <| fun _ ->
        let _, arc = create()
        arc.Model.Title <- Some "bypass"
        Expect.throws (fun () -> arc.save()) "Direct mutation detected on save"
        Expect.throws (fun () -> arc.Sample.create("x") |> ignore) "Direct mutation detected on commands"
        arc.Model.Title <- None
        arc.Dataset.setTitle(arc.Model, "managed") |> ignore
        let operations = arc.Sample
        arc.close()
        arc.close()
        Expect.throws (fun () -> operations.create("closed") |> ignore) "Cached service cannot outlive session"
        Expect.throws (fun () -> operations.set(Sample("closed"))) "Upsert cannot outlive session"
        Expect.throws (fun () -> arc.Model |> ignore) "Closed model access rejected"

    testCase "automatic selection reloads external YAML only for a clean session" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, "original") |> ignore
        arc.save()
        arc.close()
        externalTitle path "external"
        use resumed = ARC.openFolder(path, "auto")
        Expect.equal resumed.Model.Title (Some "external") "Clean session reloads external YAML"
        Expect.isFalse resumed.History.CanUndo "Reload starts fresh history"
        Expect.equal (archives path).Length 1 "Displaced session archived"
        Expect.equal (count "history" (archives path).[0]) 1L "Archive preserves old history"

    testCase "external changes conflict with pending edits and explicit SQL preserves them" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, "original") |> ignore
        arc.save()
        arc.Dataset.setTitle(arc.Model, "working") |> ignore
        arc.close()
        externalTitle path "external"
        Expect.throws (fun () -> ARC.openFolder(path, "auto") |> ignore) "Pending edits cannot be displaced"
        Expect.equal (archives path).Length 0 "Conflict does not archive"
        use resumed = ARC.openFolder(path, "sql")
        Expect.equal resumed.Model.Title (Some "working") "Explicit SQL resumes working state"
        resumed.History.undo()
        Expect.equal resumed.Model.Title (Some "original") "Undo history retained"
        Expect.isTrue resumed.IsDirty "Chosen SQL graph still needs filesystem publication"
        File.AppendAllText(Path.Combine(path, "arc.yml"), "\n# another external edit\n")
        Expect.throws (fun () -> resumed.save()) "Further external edits reject save"

    testCase "two open sessions cannot silently overwrite each other's SQL revision" <| fun _ ->
        let path, first = create()
        use first = first
        use second = ARC.openFolder(path, "auto")
        first.Dataset.setTitle(first.Model, "first writer") |> ignore
        Expect.throws (fun () -> second.Dataset.setTitle(second.Model, "stale writer") |> ignore) "Stale SQL revision rejected"
        Expect.isNone second.Model.Title "Failed stale command leaves projection unchanged"
        Expect.throws (fun () -> second.save()) "Stale save rejected"
        first.save()

    testCase "initial unsupported graph leaves no broken session database" <| fun _ ->
        let path = folder()
        let root = Dataset(["process-provenance"], ["example"], dataFiles = [Data("file", "file.txt")])
        Expect.throws (fun () -> ARC.create(path, root) |> ignore) "Unsupported initial graph rejected"
        Expect.isFalse (File.Exists(Path.Combine(path, ".arc", "testing.sqlite"))) "Failed creation removes its own database"
        Expect.isNone root.Id "Failed initial graph leaves root ID optional"

    testCase "missing sources and invalid source preferences do not create state" <| fun _ ->
        let path = folder()
        for source in ["auto"; "sql"; "yml"; "invalid"] do
            Expect.throws (fun () -> ARC.openFolder(path, source) |> ignore) "Missing or invalid source rejected"
        Expect.isFalse (Directory.Exists(Path.Combine(path, ".arc"))) "Read failures do not initialize storage"

    testCase "session-only objects conflict even when the root graph is clean" <| fun _ ->
        let path, arc = create()
        arc.Dataset.setTitle(arc.Model, "original") |> ignore
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
        arc.Dataset.setTitle(arc.Model, "original") |> ignore
        arc.save()
        arc.Sample.create("retained in archive") |> ignore
        arc.Dataset.setTitle(arc.Model, "unsaved") |> ignore
        arc.close()
        externalTitle path "external"
        let reloaded = ARC.openFolder(path, "yml")
        Expect.equal reloaded.Model.Title (Some "external") "YAML preference replaces working state"
        Expect.equal (reloaded.Sample.list()).Count 0 "Fresh registry"
        Expect.isFalse reloaded.History.CanUndo "Fresh history"
        reloaded.close()
        use second = ARC.openFolder(path, "yml")
        Expect.equal (archives path).Length 2 "Archive names are unique"
        Expect.isTrue ((archives path) |> Array.exists (fun archive -> count "sample" archive = 1L)) "Standalone data retained in archive"

    testCase "invalid YAML is validated before archiving a retained session" <| fun _ ->
        let path, arc = create()
        arc.save()
        arc.close()
        File.WriteAllText(Path.Combine(path, "arc.yml"), "type: Dataset\nunknown: unsupported\n")
        Expect.throws (fun () -> ARC.openFolder(path, "yml") |> ignore) "Unsupported YAML rejected"
        Expect.equal (archives path).Length 0 "Original SQL not displaced"
        use retained = ARC.openFolder(path, "sql")
        Expect.equal (Seq.toList retained.Model.Identifiers) ["example"] "SQL remains intact"
        retained.save()

    testCase "failed saves do not advance checkpoints or discard pending edits" <| fun _ ->
        let path, created = create()
        use arc = created
        arc.Dataset.setTitle(arc.Model, "original") |> ignore
        arc.save()
        let yaml = Path.Combine(path, "arc.yml")
        let baseline = File.ReadAllText(yaml)
        arc.Dataset.setTitle(arc.Model, "edited") |> ignore
        withSql arc.DatabasePath (fun sql ->
            sql.ExecuteScript("CREATE TRIGGER fail_save BEFORE UPDATE OF baseline ON session BEGIN SELECT RAISE(ABORT,'injected save failure'); END;"))
        Expect.throws (fun () -> arc.save()) "Injected SQL checkpoint failure"
        Expect.isTrue arc.IsDirty "Failed save leaves edits pending"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT baseline FROM session").[0].Get(0).AsText()) baseline "Checkpoint is not advanced"
            sql.Execute("DROP TRIGGER fail_save"))
        Expect.equal (Directory.GetFiles(path, ".arc-*.tmp")).Length 0 "Temporary output cleaned up"
        Expect.throws (fun () -> arc.save()) "Published file with failed SQL commit requires source reselection"
        arc.close()
        use resumed = ARC.openFolder(path, "sql")
        resumed.save()
        Expect.isFalse resumed.IsDirty "Explicit source choice recovers failed save"

    testCase "filesystem write failure leaves SQL checkpoint unchanged" <| fun _ ->
        let path, created = create()
        use arc = created
        Directory.CreateDirectory(Path.Combine(path, "arc.yml")) |> ignore
        Expect.throws (fun () -> arc.save()) "Cannot replace a directory with YAML"
        Expect.isTrue arc.IsDirty "Graph remains unsaved"
        withSql arc.DatabasePath (fun sql ->
            Expect.isTrue (sql.Query("SELECT baseline FROM session").[0].Get(0).IsNull) "No checkpoint established")
        Expect.equal (Directory.GetFiles(path, ".arc-*.tmp")).Length 0 "Temporary output removed"

    testCase "sample set inserts with supplied or assigned IDs as one reversible command" <| fun _ ->
        let _, created = create()
        use arc = created
        let supplied = Sample("supplied", id = "sample-1", additionalTypes = ["custom"; "custom"])
        let assigned = Sample("assigned")
        let result: unit = arc.Sample.set(supplied)
        arc.Sample.set(assigned)
        Expect.equal result () "Upsert returns unit"
        Expect.equal supplied.Id (Some "sample-1") "Supplied ID retained"
        Expect.isSome assigned.Id "Missing ID assigned by Layer 2"
        same (arc.Sample.get(supplied.Id.Value)) supplied
        same (arc.Sample.get(assigned.Id.Value)) assigned
        Expect.equal (count "sample" arc.DatabasePath) 2L "Both insertions written to SQL"
        withSql arc.DatabasePath (fun sql ->
            let rows = sql.Query("SELECT kind FROM history ORDER BY sequence")
            Expect.equal (rows |> Seq.map (fun row -> row.Get(0).AsText()) |> Seq.toList)
                ["Sample.set"; "Sample.set"] "One upsert command per insertion")
        let id = assigned.Id.Value
        arc.History.undo()
        Expect.equal (arc.Sample.list()).Count 1 "Undo removes the inserted registration"
        arc.History.redo()
        same (arc.Sample.get(id)) assigned
        arc.History.undo()
        arc.History.undo()
        Expect.equal (count "sample" arc.DatabasePath) 0L "SQL insertion undone"
        arc.History.redo()
        same (arc.Sample.get("sample-1")) supplied
        Expect.equal (Seq.toList supplied.AdditionalTypes) ["custom"; "custom"] "Duplicates restored"

    testCase "sample set replaces values while retaining shared references and collection identity" <| fun _ ->
        let _, created = create()
        use arc = created
        let canonical = Sample("original", id = "sample-1", additionalTypes = ["old"; "old"])
        arc.Sample.set(canonical)
        let proc = arc.Process.create("measurement")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInputSample(proc, canonical) |> ignore
        arc.Process.setOutputSample(proc, canonical) |> ignore
        let types = canonical.AdditionalTypes
        let replacement = Sample("updated", id = "sample-1", additionalTypes = ["new"; "new"; "other"])
        let commands = count "history" arc.DatabasePath
        arc.Sample.set(replacement)
        Expect.equal (count "history" arc.DatabasePath) (commands + 1L) "Replacement is one command"
        Expect.equal (arc.Sample.list()).Count 1 "No duplicate registered object"
        same (arc.Sample.get("sample-1")) canonical
        same (input proc) canonical
        same (output proc) canonical
        same canonical.AdditionalTypes types
        Expect.equal canonical.Name "updated" "Registered values replaced"
        Expect.equal (Seq.toList canonical.AdditionalTypes) ["new"; "new"; "other"] "Types replaced, not merged"
        replacement.Name <- "later caller edit"
        replacement.AdditionalTypes.Add("later")
        Expect.equal canonical.Name "updated" "Caller object was not installed over the canonical instance"
        Expect.equal canonical.AdditionalTypes.Count 3 "Caller collection was copied"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT name FROM sample").[0].Get(0).AsText()) "updated" "SQL value replaced")
        arc.History.undo()
        Expect.equal canonical.Name "original" "Prior scalar restored"
        Expect.equal (Seq.toList canonical.AdditionalTypes) ["old"; "old"] "Prior collection restored"
        same (input proc) canonical
        arc.History.redo()
        Expect.equal canonical.Name "updated" "Recorded values, not later caller edits, replayed"
        arc.Sample.set(Sample("", id = "sample-1"))
        Expect.equal canonical.Name "" "Empty name preserved"
        Expect.equal canonical.AdditionalTypes.Count 0 "Empty collection clears earlier values"
        arc.History.undo()
        Expect.equal (Seq.toList canonical.AdditionalTypes) ["new"; "new"; "other"] "Clearing is reversible"

    testCase "sample set history and pending edits recover across reopening" <| fun _ ->
        let path, created = create()
        use arc = created
        let sample = Sample("original", id = "sample-1")
        arc.Sample.set(sample)
        let proc = arc.Process.create("measurement")
        arc.Dataset.addProcess(arc.Model, proc) |> ignore
        arc.Process.setInputSample(proc, sample) |> ignore
        arc.save()
        arc.Sample.set(Sample("updated", id = "sample-1", additionalTypes = ["custom"; "custom"]))
        Expect.isTrue arc.IsDirty "Linked sample replacement awaits filesystem export"
        arc.close()
        use resumed = ARC.openFolder(path, "auto")
        let restored = resumed.Sample.get("sample-1")
        Expect.equal restored.Name "updated" "SQL working state recovered"
        same (input resumed.Model.Processes[0]) restored
        resumed.History.undo()
        Expect.equal restored.Name "original" "Recovered history undoes replacement"
        Expect.isFalse resumed.IsDirty "Undo returns to saved root"
        resumed.close()
        use reopened = ARC.openFolder(path, "auto")
        Expect.isTrue reopened.History.CanRedo "Redo survives another restart"
        reopened.History.redo()
        let redone = reopened.Sample.get("sample-1")
        Expect.equal redone.Name "updated" "Recovered replacement redone"
        Expect.equal (Seq.toList redone.AdditionalTypes) ["custom"; "custom"] "SQL snapshots retain duplicate values"
        same (input reopened.Model.Processes[0]) redone

    testCase "sample set can reactivate a deleted ID without replacing its retained instance" <| fun _ ->
        let _, created = create()
        use arc = created
        let canonical = Sample("original", id = "sample-1")
        arc.Sample.set(canonical)
        arc.Sample.delete(canonical) |> ignore
        arc.Sample.set(Sample("replacement", id = "sample-1"))
        same (arc.Sample.get("sample-1")) canonical
        Expect.equal canonical.Name "replacement" "Deleted ID is inserted again"
        arc.History.undo()
        Expect.equal (count "sample" arc.DatabasePath) 0L "Reactivation undone"
        arc.History.undo()
        same (arc.Sample.get("sample-1")) canonical
        Expect.equal canonical.Name "original" "Original deletion can still be undone"
        arc.History.redo()
        arc.History.redo()
        same (arc.Sample.get("sample-1")) canonical
        Expect.equal canonical.Name "replacement" "Reactivation redo retains identity"

    testCase "sample set rejects invalid inputs cross-type IDs and direct live mutations" <| fun _ ->
        let _, created = create()
        use arc = created
        let canonical = Sample("original", id = "sample-1")
        arc.Sample.set(canonical)
        let proc = arc.Process.create("measurement")
        let commands = count "history" arc.DatabasePath
        Expect.throws (fun () -> arc.Sample.set(Sample("collision", id = arc.Model.Id.Value))) "Dataset ID collision rejected"
        Expect.throws (fun () -> arc.Sample.set(Sample("collision", id = proc.Id.Value))) "Process ID collision rejected"
        Expect.throws (fun () -> arc.Sample.set(Unchecked.defaultof<Sample>)) "Null input rejected"
        let invalid = Sample("invalid")
        invalid.Name <- Unchecked.defaultof<string>
        Expect.throws (fun () -> arc.Sample.set(invalid)) "Null name rejected"
        Expect.isNone invalid.Id "Validation failure does not assign an ID"
        let unsupported = Sample("unsupported", id = "sample-1", additionalProperties = [Annotation("property")])
        Expect.throws (fun () -> arc.Sample.set(unsupported)) "Unsupported properties are not silently dropped"
        let invalidTypes = Sample("invalid", id = "sample-1")
        invalidTypes.AdditionalTypes.Add(Unchecked.defaultof<string>)
        Expect.throws (fun () -> arc.Sample.set(invalidTypes)) "Null collection values rejected"
        canonical.Name <- "bypassed"
        Expect.throws (fun () -> arc.Sample.set(canonical)) "Upsert does not commit direct live edits"
        canonical.Name <- "original"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed upserts create no history"
        same (arc.Sample.get("sample-1")) canonical
        Expect.equal canonical.Name "original" "Registered values unchanged"
        Expect.throws (fun () -> arc.Sample.register(Sample("duplicate", id = "sample-1")) |> ignore) "Registration still rejects duplicates"

    testCase "sample set SQL failure rolls back updates insertions and assigned IDs" <| fun _ ->
        let _, created = create()
        use arc = created
        let canonical = Sample("original", id = "sample-1", additionalTypes = ["old"])
        arc.Sample.set(canonical)
        let commands = count "history" arc.DatabasePath
        withSql arc.DatabasePath (fun sql ->
            sql.ExecuteScript("CREATE TRIGGER reject_upsert BEFORE INSERT ON sample WHEN NEW.name='reject' BEGIN SELECT RAISE(ABORT,'injected failure'); END;"))
        Expect.throws (fun () -> arc.Sample.set(Sample("reject", id = "sample-1", additionalTypes = ["new"]))) "Replacement SQL failure"
        Expect.equal canonical.Name "original" "Failed update leaves canonical values unchanged"
        Expect.equal (Seq.toList canonical.AdditionalTypes) ["old"] "Failed update restores collection values"
        let pending = Sample("reject")
        Expect.throws (fun () -> arc.Sample.set(pending)) "Insertion SQL failure"
        Expect.isNone pending.Id "Failed insertion restores missing ID"
        Expect.equal (arc.Sample.list()).Count 1 "Failed insertion leaves registry unchanged"
        Expect.equal (count "history" arc.DatabasePath) commands "Failed SQL creates no upsert history"
        withSql arc.DatabasePath (fun sql ->
            Expect.equal (sql.Query("SELECT name FROM sample").[0].Get(0).AsText()) "original" "SQL transaction rolled back")
        arc.History.undo()
        Expect.equal (arc.Sample.list()).Count 0 "Existing history remains usable"

    testCase "sample set branches history without dirtying a saved standalone root" <| fun _ ->
        let _, created = create()
        use arc = created
        arc.save()
        let sample = Sample("original")
        arc.Sample.set(sample)
        arc.History.undo()
        Expect.isTrue arc.History.CanRedo "Insertion can be redone"
        arc.Sample.set(Sample("new branch", id = sample.Id.Value))
        Expect.isFalse arc.History.CanRedo "New upsert clears the redo branch"
        same (arc.Sample.get(sample.Id.Value)) sample
        Expect.equal sample.Name "new branch" "Retained identity reused on the new branch"
        Expect.isFalse arc.IsDirty "Standalone upsert does not change the saved root graph"
        Expect.isTrue arc.HasSessionOnlyObjects "Standalone state is tracked separately"
]
