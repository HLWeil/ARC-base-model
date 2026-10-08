namespace ARCtrl.Internal

open ARCtrl.Helper

open System
open System.Collections.Generic
open ARCBaseModel
open PolyglotSQLite
open ARCtrl
open ARCSession.Internal

type internal Session(repository: Repository, arcId: string, supplied: (string * obj) list) =
    let db = repository.Connection
    let metadata = Store.metadata db arcId
    let mutable state = metadata.GetByName("state").AsText() |> Codec.decodeState
    let mutable cursor = int (metadata.GetByName("cursor").AsInteger())
    let mutable revision = metadata.GetByName("revision").AsInteger()
    let mutable baseline = Store.optText metadata "baseline"
    let mutable savedGraph = Store.optText metadata "saved_graph"
    let mutable closed = false
    // Retained instances also serve as tombstones so undo preserves identity.
    let entities = Dictionary<string,obj>()
    let mutable folder = Store.optText metadata "folder"
    let active id = state.Entities |> List.exists (fun row -> row.Id = id)
    let ensureOpen () =
        repository.EnsureOpen()
        if closed then invalidOp "The ARC context is closed."
    let owned entity =
        Model.required "entity" entity |> ignore
        let id = Model.id entity |> Model.entityId
        if not (active id && entities.ContainsKey(id) && obj.ReferenceEquals(entities[id], entity)) then
            invalidOp (Model.kind entity + " is not registered in this ARC.")
        id
    let execute sql values = Store.execute db sql (("arc",Store.text arcId)::values)
    let query sql values = db.Query(sql, Store.parameters (("arc",Store.text arcId)::values))
    let restore next =
        for row in next.Entities do
            if not (entities.ContainsKey row.Id) then
                let value = Model.create row
                repository.Claim(arcId,value)
                entities.Add(row.Id,value)
        for row in next.Entities do Model.restore (fun id -> entities[id]) row entities[row.Id]
    let check () =
        ensureOpen()
        let captured = { state with Entities = state.Entities |> List.map (fun row -> {Model.capture (owned entities[row.Id]) owned entities[row.Id] with SuppliedId = row.SuppliedId}) }
        if captured <> state then invalidOp "Direct model mutation detected. Use ARC operations or close and reopen this ARC."
    let assertRevision () =
        if (Store.metadata db arcId).GetByName("revision").AsInteger() <> revision then
            invalidOp "ARC changed through another connection. Close this ARC and reopen it with session.openArc or ARC.openFolder."
    let update next nextCursor kind action operationId =
        Model.validate next
        let previous = state
        try
            db.WithTransaction(fun () ->
                assertRevision()
                Store.mirror db arcId next
                if action = "apply" then
                    execute "DELETE FROM history WHERE arc_id=$arc AND sequence > $cursor" ["cursor", SqlValue.Integer(int64 cursor)]
                    execute "INSERT INTO history VALUES($arc,$sequence,$id,$kind,$before,$after)"
                        ["sequence", SqlValue.Integer(int64 nextCursor); "id", Store.text operationId; "kind", Store.text kind
                         "before", Store.text (Codec.encodeState previous); "after", Store.text (Codec.encodeState next)]
                execute "UPDATE session SET state=$state,root_id=$root,cursor=$cursor,revision=$revision WHERE arc_id=$arc"
                    ["root",Store.text next.Root; "state", Store.text (Codec.encodeState next); "cursor", SqlValue.Integer(int64 nextCursor); "revision", SqlValue.Integer(revision + 1L)]
                execute "INSERT INTO journal(arc_id,sequence,operation_id,kind,action,revision) VALUES($arc,$revision,$id,$kind,$action,$revision)"
                    ["id", Store.text operationId; "kind", Store.text kind; "action", Store.text action; "revision", SqlValue.Integer(revision + 1L)]
                restore next
                if action = "undo" && (kind.EndsWith(".register") || kind.EndsWith(".set")) then
                    for row in previous.Entities do
                        if not (next.Entities |> List.exists (fun nextRow -> nextRow.Id = row.Id)) then
                            Model.setId entities[row.Id] row.SuppliedId)
            state <- next; cursor <- nextCursor; revision <- revision + 1L
        with _ -> restore previous; reraise()
    do
        Model.validate state
        for id,value in supplied do
            repository.Claim(arcId,value)
            entities.Add(id,value)
        restore state

    member _.Model = ensureOpen(); unbox<Dataset> entities[state.Root]
    member _.ArcId = arcId
    member _.Repository = repository
    member _.IsClosed = closed
    member _.Folder = ensureOpen(); folder
    member _.DatabasePath = repository.DatabasePath
    member _.State = ensureOpen(); state
    member _.Baseline = baseline
    member _.SavedGraph = savedGraph
    member _.Revision = revision
    member _.IsDirty = check(); savedGraph <> Some(Codec.encodeGraph state)
    member _.HasSessionOnlyObjects = check(); Model.sessionOnly state
    member _.CanUndo = ensureOpen(); cursor > 0
    member _.CanRedo = ensureOpen(); (query "SELECT sequence FROM history WHERE arc_id=$arc AND sequence=$sequence" ["sequence",SqlValue.Integer(int64 (cursor + 1))]).Count > 0
    member _.Check() = check()
    member _.Id(value: obj) = ensureOpen(); owned value
    member _.List<'T>(kind: string) =
        ensureOpen()
        state.Entities |> Seq.filter (fun row -> row.Kind = kind) |> Seq.map (fun row -> unbox<'T> entities[row.Id]) |> ResizeArray
    member _.Get<'T>(kind: string, id: string) =
        ensureOpen()
        if not (state.Entities |> List.exists (fun row -> row.Id = id && row.Kind = kind)) then invalidArg "id" ("Unknown " + kind)
        unbox<'T> entities[id]
    member _.Execute(kind, transform) = repository.Access(fun () ->
        check()
        let next = transform state
        let operationId = Identifier.newId()
        update next (cursor + 1) kind "apply" operationId
        AppliedOperation(operationId, kind))
    member this.Change(entity: obj, key: string, value: Cell option, kind: string) =
        let id = this.Id(entity)
        this.Execute(kind, fun state ->
            let rows = state.Entities |> List.map (fun row ->
                if row.Id <> id then row
                else { row with Properties = match value with Some v -> Map.add key v row.Properties | None -> Map.remove key row.Properties })
            { state with Entities = rows })
    member _.GetEntity(id: string) =
        ensureOpen()
        if not (active id) then invalidArg "id" "Unknown entity."
        unbox<EntityObject> entities[id]
    member _.ListEntities() =
        ensureOpen()
        state.Entities |> Seq.map (fun row -> unbox<EntityObject> entities[row.Id]) |> ResizeArray
    member this.Extension(entity: EntityObject, key: string, value: Entity option, addOnly: bool) =
        let id = this.Id(entity)
        let captured = value |> Option.map (Model.captureExtension owned)
        this.Execute(entity.Type + "." + (if value.IsNone then "removeProperty" elif addOnly then "addProperty" else "setProperty"), fun state ->
            let rows = state.Entities |> List.map (fun row ->
                if row.Id <> id then row
                else
                    if addOnly && Map.containsKey key row.Extensions then invalidArg "key" "Duplicate extension property."
                    {row with Extensions = match captured with Some v -> Map.add key v row.Extensions | None -> Map.remove key row.Extensions})
            {state with Entities = rows})
    member this.Collection(entity: obj, key: string, target: obj, append: bool, kind: string) =
        let id, targetId = this.Id(entity), this.Id(target)
        this.Execute(kind, fun state ->
            let rows = state.Entities |> List.map (fun row ->
                if row.Id <> id then row else
                    let values = Model.links key row
                    if not append && not (List.contains targetId values) then invalidOp "Target is not attached."
                    let rec remove = function [] -> [] | head :: tail when head = targetId -> tail | head :: tail -> head :: remove tail
                    {row with Properties = Map.add key (Links(if append then values @ [targetId] else remove values)) row.Properties})
            {state with Entities = rows})
    member this.Move(entity: obj, destination: Dataset, key: string, kind: string) =
        let id, destinationId = this.Id(entity), this.Id(destination)
        this.Execute(kind,fun state ->
            let occurrences = state.Entities |> List.filter (fun row -> row.Kind = "Dataset") |> List.collect (Model.links key) |> List.filter ((=) id)
            if occurrences.Length > 1 then invalidOp "Move requires a single membership. Use collection setters to edit shared or repeated memberships."
            let rows = state.Entities |> List.map (fun row ->
                if row.Kind <> "Dataset" then row else
                    let values = Model.links key row |> List.filter ((<>) id)
                    {row with Properties = Map.add key (Links(if row.Id = destinationId then values @ [id] else values)) row.Properties})
            {state with Entities = rows})
    member this.Delete(entity: obj) =
        let id = this.Id(entity)
        this.Execute(Model.kind entity + ".delete",fun state ->
            if id = state.Root then invalidOp "The root Dataset cannot be deleted."
            // Cascade Dataset containment only when it is exclusively owned by deleted nodes.
            let rec cascade removed =
                let candidates = state.Entities |> List.filter (fun row -> Set.contains row.Id removed && row.Kind = "Dataset")
                                 |> List.collect (fun row -> Model.links "hasParts" row @ Model.links "processes" row)
                let next = candidates |> List.fold (fun deleted candidate ->
                    let shared = state.Entities |> List.exists (fun row ->
                        not (Set.contains row.Id deleted) &&
                        ((row.Properties |> Map.exists (fun _ cell -> match cell with Links ids -> List.contains candidate ids | _ -> false)) ||
                         (row.Extensions |> Map.exists (fun _ cell -> Model.extensionReferences cell |> List.contains candidate))))
                    if shared then deleted else Set.add candidate deleted) removed
                if next = removed then removed else cascade next
            let removed = cascade (Set.singleton id)
            let rows = state.Entities |> List.filter (fun row -> not (Set.contains row.Id removed)) |> List.map (fun row ->
                if row.Extensions |> Map.exists (fun _ cell -> Model.extensionReferences cell |> List.exists (fun id -> Set.contains id removed)) then
                    invalidOp "Remove extension references before deleting their targets."
                let props = row.Properties |> Map.toList |> List.choose (fun (key,cell) ->
                    match cell with
                    | Links ids ->
                        let remaining = ids |> List.filter (fun id -> not (Set.contains id removed))
                        let _,shape,_,mandatory = Model.specifications row.Kind |> List.find (fun (name,_,_,_) -> name = key)
                        if remaining = [] && shape <> "links" then
                            if mandatory then invalidOp ("Cannot delete required target of " + row.Kind + "." + key)
                            None
                        else Some(key,Links remaining)
                    | _ -> Some(key,cell)) |> Map.ofList
                {row with Properties = props})
            {state with Entities = rows})
    member private _.Adopt(entity: obj, kind: string, replaceExisting: bool, operationKind: string) = repository.Access(fun () ->
        check()
        Model.required "entity" entity |> ignore
        if Model.kind entity <> kind then invalidArg "entity" "Wrong entity type."
        let pending = ResizeArray<string * obj>()
        let replacements = Dictionary<string,EntityRow>()
        let mutable planned = state.Entities
        let rec register (value: obj) =
            Model.required "entity" value |> ignore
            repository.AssertAvailable(arcId,value)
            match pending |> Seq.tryFind (fun (_,existing) -> obj.ReferenceEquals(value,existing)) with
            | Some(id,_) -> id
            | None ->
                let retained = entities |> Seq.tryFind (fun item -> obj.ReferenceEquals(item.Value,value))
                let id =
                    match retained, Model.id value with
                    | Some item, None -> item.Key
                    | Some item, Some supplied when item.Key <> supplied -> invalidOp "A retained instance cannot change its session identity. Use a detached input."
                    | _, Some supplied -> supplied
                    | _ -> Identifier.newId()
                let isRoot = obj.ReferenceEquals(entity,value)
                if entities.ContainsKey id then
                    let canonical = entities[id]
                    if Model.kind canonical <> Model.kind value then invalidOp ("Session ID collision: " + id)
                    if (isRoot && replaceExisting) || (not (active id) && obj.ReferenceEquals(canonical,value)) then
                        // Reserve first to permit self-references in detached graphs.
                        pending.Add(id,value)
                        let row = Model.capture id register value
                        let suppliedId = state.Entities |> List.tryFind (fun row -> row.Id = id) |> Option.map (fun row -> row.SuppliedId) |> Option.defaultWith (fun () -> Model.id canonical)
                        let row = {row with SuppliedId = suppliedId}
                        replacements[id] <- Model.capture id (fun reference -> entities |> Seq.find (fun item -> obj.ReferenceEquals(item.Value,reference)) |> fun item -> item.Key) canonical
                        planned <- planned |> List.filter (fun row -> row.Id <> id)
                        planned <- planned @ [row]
                        id
                    elif active id && obj.ReferenceEquals(canonical,value) then id
                    else invalidOp ("Session ID collision: " + id)
                else
                    if pending |> Seq.exists (fun (existing,_) -> existing = id) then invalidOp ("Session ID collision: " + id)
                    pending.Add(id,value)
                    let row = Model.capture id register value
                    planned <- planned @ [row]
                    id
        register entity |> ignore
        let next = {state with Entities = planned}
        Model.validate next
        let additions = pending |> Seq.filter (fun (id,_) -> not (entities.ContainsKey id)) |> Seq.map (fun (id,value) -> id,value,Model.id value) |> Seq.toList
        for id,value,_ in additions do
            repository.Claim(arcId,value)
            entities.Add(id,value)
        try
            if next <> state then update next (cursor + 1) operationKind "apply" (Identifier.newId())
        with _ ->
            for KeyValue(id,row) in replacements do
                Model.restore (fun id -> entities[id]) row entities[id]
                if not (active id) then Model.setId entities[id] row.SuppliedId
            for id,value,original in additions do
                entities.Remove(id) |> ignore
                repository.Release(arcId,value)
                Model.setId value original
            reraise())
    member this.Register(entity: obj, kind: string) = this.Adopt(entity,kind,false,kind + ".register")
    member this.Set(entity: obj, kind: string) = this.Adopt(entity,kind,true,kind + ".set")
    member _.History(redo: bool) = repository.Access(fun () ->
        check()
        let sequence = if redo then cursor + 1 else cursor
        let rows = query "SELECT * FROM history WHERE arc_id=$arc AND sequence=$sequence" ["sequence",SqlValue.Integer(int64 sequence)]
        if sequence <= 0 || rows.Count = 0 then invalidOp (if redo then "Nothing to redo." else "Nothing to undo.")
        let row = rows[0]
        let next = row.GetByName(if redo then "after_state" else "before_state").AsText() |> Codec.decodeState
        update next (if redo then cursor + 1 else cursor - 1) (row.GetByName("kind").AsText())
            (if redo then "redo" else "undo") (row.GetByName("operation_id").AsText()))
    member _.AssertRevision() = ensureOpen(); assertRevision()
    member _.SetBinding(path: string) =
        check()
        assertRevision()
        execute "UPDATE session SET folder=$folder,baseline=NULL,saved_graph=NULL,revision=$revision WHERE arc_id=$arc"
            ["folder",Store.text path; "revision",SqlValue.Integer(revision + 1L)]
        folder <- Some path; baseline <- None; savedGraph <- None; revision <- revision + 1L
    member _.SetCheckpoint(graph: string) =
        execute "UPDATE session SET baseline=$graph,saved_graph=$graph,revision=$revision WHERE arc_id=$arc"
            ["graph",Store.text graph; "revision",SqlValue.Integer(revision + 1L)]
    member _.AcceptCheckpoint(graph: string) =
        baseline <- Some graph; savedGraph <- Some graph; revision <- revision + 1L
    member _.AcceptPersistentBaseline(current: string option) =
        check()
        if current <> baseline then
            db.WithTransaction(fun () ->
                assertRevision()
                execute "UPDATE session SET baseline=$baseline,saved_graph=NULL,revision=$revision WHERE arc_id=$arc"
                    ["baseline",Store.optional current; "revision",SqlValue.Integer(revision + 1L)])
            baseline <- current; savedGraph <- None; revision <- revision + 1L
    member _.Reload(next: State, current: string) =
        check()
        Model.validate next
        db.WithTransaction(fun () ->
            assertRevision()
            Store.archive db arcId |> ignore
            Store.mirror db arcId next
            execute "DELETE FROM history WHERE arc_id=$arc" []
            execute "DELETE FROM journal WHERE arc_id=$arc" []
            execute "UPDATE session SET state=$state,root_id=$root,baseline=$baseline,saved_graph=$saved,cursor=0,revision=$revision WHERE arc_id=$arc"
                ["state",Store.text (Codec.encodeState next); "root",Store.text next.Root; "baseline",Store.text current
                 "saved",Store.text (Codec.encodeGraph next); "revision",SqlValue.Integer(revision + 1L)])
        // New instances isolate displaced state from the replacement projection.
        for value in entities.Values do repository.Release(arcId,value)
        entities.Clear()
        state <- next; cursor <- 0; revision <- revision + 1L
        baseline <- Some current; savedGraph <- Some(Codec.encodeGraph next)
        restore next
    member _.Close() =
        if not closed then
            closed <- true
            repository.Forget(arcId)
            entities.Clear()
