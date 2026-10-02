namespace ARCtrl.Internal

open System
open System.Collections.Generic
open ARCBaseModel
open PolyglotSQLite
open ARCtrl

type internal Session(folder: string, database: Database) =
    let db = database.Connection
    let metadata = Store.metadata db
    let mutable state = metadata.GetByName("state").AsText() |> Codec.decodeState
    let mutable cursor = int (metadata.GetByName("cursor").AsInteger())
    let mutable revision = metadata.GetByName("revision").AsInteger()
    let mutable baseline = Store.optText metadata "baseline"
    let mutable savedGraph = Store.optText metadata "saved_graph"
    let mutable closed = false
    let datasets = Dictionary<string, Dataset>()
    let processes = Dictionary<string, ARCBaseModel.Process>()
    let samples = Dictionary<string, Sample>()
    let rootPath = Files.combine folder "arc.yml"
    let replace (target: ResizeArray<'T>) values = target.Clear(); target.AddRange(values)
    let active kind id =
        match kind with
        | "Dataset" -> state.Datasets |> List.exists (fun row -> row.Id = id)
        | "Process" -> state.Processes |> List.exists (fun row -> row.Id = id)
        | _ -> state.Samples |> List.exists (fun row -> row.Id = id)
    let owned kind id entity =
        let matches =
            match kind with
            | "Dataset" -> datasets.ContainsKey(id) && obj.ReferenceEquals(datasets[id], entity)
            | "Process" -> processes.ContainsKey(id) && obj.ReferenceEquals(processes[id], entity)
            | _ -> samples.ContainsKey(id) && obj.ReferenceEquals(samples[id], entity)
        if not (active kind id && matches) then invalidOp (kind + " is not registered in this session.")
        id
    let datasetId (value: Dataset) = Model.required "dataset" value |> ignore; owned "Dataset" (Model.entityId value.Id) value
    let processId (value: ARCBaseModel.Process) = Model.required "process" value |> ignore; owned "Process" (Model.entityId value.Id) value
    let sampleId (value: Sample) = Model.required "sample" value |> ignore; owned "Sample" (Model.entityId value.Id) value
    let endpoint = function
        | EntityReference.Sample value -> sampleId value
        | EntityReference.Data _ -> invalidOp "Data endpoints are outside the prototype."
    let restore next =
        for row in next.Datasets do
            if not (datasets.ContainsKey row.Id) then datasets.Add(row.Id, Dataset(row.Profiles, row.Identifiers))
            let value = datasets[row.Id]
            value.Id <- Some row.Id
            replace value.AdditionalTypes row.Types; replace value.ConformsTo row.Profiles; replace value.Identifiers row.Identifiers
            value.Title <- row.Title; value.Description <- row.Description; value.License <- row.License
            value.DatePublished <- row.Published; value.DateCreated <- row.Created; value.DateModified <- row.Modified
        for row in next.Samples do
            if not (samples.ContainsKey row.Id) then samples.Add(row.Id, Sample(row.Name))
            let value = samples[row.Id]
            value.Id <- Some row.Id; value.Name <- row.Name; replace value.AdditionalTypes row.Types
        for row in next.Processes do
            if not (processes.ContainsKey row.Id) then processes.Add(row.Id, ARCBaseModel.Process(row.Name))
            let value = processes[row.Id]
            value.Id <- Some row.Id; value.Name <- row.Name; replace value.AdditionalTypes row.Types
            value.Input <- row.Input |> Option.map (fun id -> EntityReference.Sample samples[id])
            value.Output <- row.Output |> Option.map (fun id -> EntityReference.Sample samples[id])
        for row in next.Datasets do
            replace datasets[row.Id].HasParts (row.Parts |> List.map (fun id -> datasets[id]))
            replace datasets[row.Id].Processes (row.Processes |> List.map (fun id -> processes[id]))
    let ensureOpen () = if closed then invalidOp "The ARC session is closed."
    let check () =
        ensureOpen()
        let captured =
            { Root = state.Root
              Datasets = state.Datasets |> List.map (fun row -> let value = datasets[row.Id] in Model.datasetRow (datasetId value) datasetId processId value)
              Processes = state.Processes |> List.map (fun row -> let value = processes[row.Id] in Model.processRow (processId value) endpoint value)
              Samples = state.Samples |> List.map (fun row -> let value = samples[row.Id] in Model.sampleRow (sampleId value) value) }
        if captured <> state then invalidOp "Direct model mutation detected. Use the session operations or reopen the session."
    let assertRevision () =
        if (Store.metadata db).GetByName("revision").AsInteger() <> revision then invalidOp "Session changed through another connection; reopen it."
    let update next nextCursor kind action operationId =
        Model.validate next
        let previous = state
        try
            db.WithTransaction(fun () ->
                assertRevision()
                Store.mirror db next
                if action = "apply" then
                    Store.execute db "DELETE FROM history WHERE sequence > $cursor" ["cursor", SqlValue.Integer(int64 cursor)]
                    Store.execute db "INSERT INTO history VALUES($sequence,$id,$kind,$before,$after)"
                        ["sequence", SqlValue.Integer(int64 nextCursor); "id", Store.text operationId; "kind", Store.text kind
                         "before", Store.text (Codec.encodeState previous); "after", Store.text (Codec.encodeState next)]
                Store.execute db "UPDATE session SET state=$state,cursor=$cursor,revision=$revision WHERE singleton=1"
                    ["state", Store.text (Codec.encodeState next); "cursor", SqlValue.Integer(int64 nextCursor); "revision", SqlValue.Integer(revision + 1L)]
                Store.execute db "INSERT INTO journal(operation_id,kind,action,revision) VALUES($id,$kind,$action,$revision)"
                    ["id", Store.text operationId; "kind", Store.text kind; "action", Store.text action; "revision", SqlValue.Integer(revision + 1L)]
                restore next)
            state <- next; cursor <- nextCursor; revision <- revision + 1L
        with _ -> restore previous; reraise()
    do Model.validate state; restore state

    member _.Model = ensureOpen(); datasets[state.Root]
    member _.Folder = folder
    member _.DatabasePath = Files.combine (Files.combine folder ".arc") "testing.sqlite"
    member _.IsDirty = check(); savedGraph <> Some(Codec.encodeGraph state)
    member _.HasSessionOnlyObjects = check(); Model.sessionOnly state
    member _.CanUndo = ensureOpen(); cursor > 0
    member _.CanRedo = ensureOpen(); db.Query("SELECT sequence FROM history WHERE sequence=" + string (cursor + 1)).Count > 0
    member _.Check() = check()
    member _.Execute(kind, transform) =
        check()
        let next = transform state
        let operationId = Files.newId()
        update next (cursor + 1) kind "apply" operationId
        AppliedOperation(operationId, kind)
    member _.DatasetId(value) = ensureOpen(); datasetId value
    member _.ProcessId(value) = ensureOpen(); processId value
    member _.SampleId(value) = ensureOpen(); sampleId value
    member _.Datasets() = ensureOpen(); state.Datasets |> Seq.map (fun row -> datasets[row.Id]) |> ResizeArray
    member _.Processes() = ensureOpen(); state.Processes |> Seq.map (fun row -> processes[row.Id]) |> ResizeArray
    member _.Samples() = ensureOpen(); state.Samples |> Seq.map (fun row -> samples[row.Id]) |> ResizeArray
    member _.GetDataset(id) =
        ensureOpen()
        if not (active "Dataset" id) then invalidArg "id" "Unknown Dataset."
        datasets[id]
    member _.GetProcess(id) =
        ensureOpen()
        if not (active "Process" id) then invalidArg "id" "Unknown Process."
        processes[id]
    member _.GetSample(id) =
        ensureOpen()
        if not (active "Sample" id) then invalidArg "id" "Unknown Sample."
        samples[id]
    member private _.RegisterAs(entity: obj, kind: string, operationKind: string) =
        check()
        let pending = ResizeArray<string * obj>()
        let mutable next = state
        let reserve kind supplied value =
            match pending |> Seq.tryFind (fun (_, existing) -> obj.ReferenceEquals(value, existing)) with
            | Some(id, _) -> id, false
            | None ->
                let id = supplied |> Option.defaultWith Files.newId
                let existing =
                    if datasets.ContainsKey(id) then Some(box datasets[id])
                    elif processes.ContainsKey(id) then Some(box processes[id])
                    elif samples.ContainsKey(id) then Some(box samples[id]) else None
                match existing with
                | Some existing when obj.ReferenceEquals(existing, value) && active kind id -> id, false
                | Some _ -> invalidOp ("Session ID collision: " + id)
                | None ->
                    if pending |> Seq.exists (fun (existingId, _) -> existingId = id) then invalidOp ("Session ID collision: " + id)
                    pending.Add(id, value)
                    id, true
        let rec sample (value: Sample) =
            Model.required "sample" value |> ignore
            let id, isNew = reserve "Sample" value.Id value
            if isNew then next <- { next with Samples = next.Samples @ [Model.sampleRow id value] }
            id
        and proc (value: ARCBaseModel.Process) =
            Model.required "process" value |> ignore
            let id, isNew = reserve "Process" value.Id value
            if isNew then
                let row = Model.processRow id (function EntityReference.Sample s -> sample s | EntityReference.Data _ -> invalidOp "Data endpoints are outside the prototype.") value
                next <- { next with Processes = next.Processes @ [row] }
            id
        and dataset (value: Dataset) =
            Model.required "dataset" value |> ignore
            let id, isNew = reserve "Dataset" value.Id value
            if isNew then
                let row = Model.datasetRow id dataset proc value
                next <- { next with Datasets = next.Datasets @ [row] }
            id
        match entity with
        | :? Dataset as value -> dataset value |> ignore
        | :? ARCBaseModel.Process as value -> proc value |> ignore
        | :? Sample as value -> sample value |> ignore
        | _ -> invalidArg "entity" "Only Dataset, Process, and Sample are supported."
        Model.validate next
        if pending.Count > 0 then
            for id, value in pending do
                match value with
                | :? Dataset as value -> datasets.Add(id, value)
                | :? ARCBaseModel.Process as value -> processes.Add(id, value)
                | :? Sample as value -> samples.Add(id, value)
                | _ -> ()
            let originalIds = pending |> Seq.map (fun (id, value) ->
                id, value, (match value with :? Dataset as d -> d.Id | :? ARCBaseModel.Process as p -> p.Id | :? Sample as s -> s.Id | _ -> None)) |> Seq.toList
            try update next (cursor + 1) operationKind "apply" (Files.newId())
            with _ ->
                for id, value, originalId in originalIds do
                    match value with
                    | :? Dataset as d -> datasets.Remove(id) |> ignore; d.Id <- originalId
                    | :? ARCBaseModel.Process as p -> processes.Remove(id) |> ignore; p.Id <- originalId
                    | :? Sample as s -> samples.Remove(id) |> ignore; s.Id <- originalId
                    | _ -> ()
                reraise()
    member this.Register(entity: obj, kind: string) =
        this.RegisterAs(entity, kind, kind + ".register")
    member this.SetSample(value: Sample) =
        check()
        Model.required "sample" value |> ignore
        match value.Id with
        | Some id when samples.ContainsKey(id) ->
            // Keep the canonical instance, including a tombstone retained for undo.
            let canonical = samples[id]
            let previous = Model.sampleRow id canonical
            let replacement = Model.sampleRow id value
            let rows =
                if active "Sample" id then
                    state.Samples |> List.map (fun row -> if row.Id = id then replacement else row)
                else state.Samples @ [replacement]
            try
                update { state with Samples = rows } (cursor + 1) "Sample.set" "apply" (Files.newId())
            with _ ->
                // Normal rollback restores active objects; also repair an inactive instance.
                canonical.Id <- Some previous.Id
                canonical.Name <- previous.Name
                replace canonical.AdditionalTypes previous.Types
                reraise()
        | _ -> this.RegisterAs(value, "Sample", "Sample.set")
    member _.InitializeRoot(value: Dataset) =
        let previousRoot = state.Root
        let next = { state with Root = datasetId value; Datasets = state.Datasets |> List.filter (fun row -> row.Id <> previousRoot) }
        Model.validate next
        db.WithTransaction(fun () ->
            Store.mirror db next
            Store.execute db "UPDATE session SET state=$state,cursor=0,revision=0 WHERE singleton=1" ["state", Store.text (Codec.encodeState next)]
            db.Execute("DELETE FROM history")
            db.Execute("DELETE FROM journal"))
        datasets.Remove(previousRoot) |> ignore
        state <- next; cursor <- 0; revision <- 0L
    member _.History(redo: bool) =
        check()
        let sequence = if redo then cursor + 1 else cursor
        let rows = db.Query("SELECT * FROM history WHERE sequence=" + string sequence)
        if sequence <= 0 || rows.Count = 0 then invalidOp (if redo then "Nothing to redo." else "Nothing to undo.")
        let row = rows[0]
        let next = row.GetByName(if redo then "after_state" else "before_state").AsText() |> Codec.decodeState
        update next (if redo then cursor + 1 else cursor - 1) (row.GetByName("kind").AsText())
            (if redo then "redo" else "undo") (row.GetByName("operation_id").AsText())
    member _.Save() =
        check()
        if Files.readOptional rootPath <> baseline then invalidOp "Persistent YAML changed externally. Reopen with an explicit source preference."
        let graph = Codec.encodeGraph state
        let temporary = Files.combine folder (".arc-" + Files.newId() + ".tmp")
        try
            Files.write temporary graph
            db.WithTransaction(fun () ->
                assertRevision()
                if Files.readOptional rootPath <> baseline then invalidOp "Persistent YAML changed during save."
                Files.replace temporary rootPath
                Store.execute db "UPDATE session SET baseline=$graph,saved_graph=$graph,revision=$revision WHERE singleton=1"
                    ["graph", Store.text graph; "revision", SqlValue.Integer(revision + 1L)])
            baseline <- Some graph; savedGraph <- Some graph; revision <- revision + 1L
        finally
            if Files.exists temporary then Files.remove temporary
    member _.AcceptPersistentBaseline(current: string option) =
        // An explicit SQL choice accepts the observed disk version for overwrite,
        // but does not pretend the retained working graph has been exported there.
        if current <> baseline then
            db.WithTransaction(fun () ->
                assertRevision()
                Store.execute db "UPDATE session SET baseline=$baseline,saved_graph=NULL,revision=$revision WHERE singleton=1"
                    ["baseline", Store.optional current; "revision", SqlValue.Integer(revision + 1L)])
            baseline <- current; savedGraph <- None; revision <- revision + 1L
    member _.Close() = if not closed then database.Close(); closed <- true
