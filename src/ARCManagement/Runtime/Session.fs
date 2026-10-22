namespace ARCManagement.Internal

open ARCManagement.Helper

open System
open System.Collections.Generic
open ARCBaseModel
open PolyglotSQLite
open ARCManagement
open ARCManagement.Internal

/// Everything one revision of an ARC entry holds. SQL is authoritative and this value is replaced as a whole.
type internal Stored =
    { State: State
      Ordinals: Map<string, int64>
      Cursor: int
      Revision: int64
      Folder: string option
      Baseline: string option
      SavedGraph: string option }

/// Caches the stored rows of one ARC entry and hydrates fresh immutable model values from them.
type internal GraphContext(repository: Repository, arcId: string) =
    let db = repository.Connection
    let arcParameters values = Store.parameters (("arc", Store.text arcId) :: values)
    let execute sql values = Store.execute db sql (("arc", Store.text arcId) :: values)
    let query sql values = db.Query(sql, arcParameters values)
    let readStored () =
        let metadata = Store.metadata db arcId
        let state, ordinals = Store.load db arcId
        { State = state
          Ordinals = ordinals
          Cursor = int (metadata.GetByName("cursor").AsInteger())
          Revision = metadata.GetByName("revision").AsInteger()
          Folder = Store.optText metadata "folder"
          Baseline = Store.optText metadata "baseline"
          SavedGraph = Store.optText metadata "saved_graph" }
    let mutable stored = readStored ()
    let index = Dictionary<string, EntityRow>()
    // Hydrated values of the current revision. Any publication of a new revision clears it.
    let cache = Dictionary<string, obj>()
    let mutable closed = false
    let publish (next: Stored) =
        stored <- next
        index.Clear()
        for row in next.State.Entities do index[row.Id] <- row
        cache.Clear()
    let references (row: EntityRow) =
        let linked = row.Properties |> Map.toList |> List.collect (fun (_, cell) -> match cell with Links ids -> ids | _ -> [])
        linked @ (row.Extensions |> Map.toList |> List.collect (snd >> Model.extensionReferences))
    let hydrate (id: string) : obj =
        match cache.TryGetValue id with
        | true, value -> value
        | _ ->
            let fresh = ResizeArray<EntityRow>()
            let pending = ResizeArray<string>()
            pending.Add id
            while pending.Count > 0 do
                let next = pending[pending.Count - 1]
                pending.RemoveAt(pending.Count - 1)
                if not (cache.ContainsKey next) then
                    let row = index[next]
                    cache[next] <- Model.create row
                    fresh.Add row
                    for target in references row do pending.Add target
            // Restoring touches only objects allocated above, which no caller has seen yet.
            for row in fresh do Model.restore (fun target -> cache[target]) row cache[row.Id]
            cache[id]
    let ensureOpen () =
        repository.EnsureOpen()
        if closed then invalidOp "The ARC context is closed."
    // Picks up commits of other connections. Call inside the repository gate.
    let sync () =
        ensureOpen()
        let rows = query "SELECT revision FROM arc WHERE arc_id=$arc" []
        if rows.Count <> 1 then invalidOp "Unknown ARC entry."
        if rows[0].Get(0).AsInteger() <> stored.Revision then
            let scope = db.BeginTransaction()
            try
                let fresh = readStored ()
                scope.Commit()
                publish fresh
            finally scope.Close()
    let read (action: unit -> 'T) : 'T = repository.Access(fun () -> sync (); action ())
    // Runs one command in a write transaction. The command receives the current stored revision and returns the next one,
    // or None when it changed nothing. It performs its own SQL writes.
    let run (command: Stored -> Stored option) : unit =
        repository.Access(fun () ->
            ensureOpen()
            let scope = db.BeginTransaction()
            try
                let rows = query "UPDATE arc SET revision = revision + 1 WHERE arc_id=$arc RETURNING revision" []
                if rows.Count <> 1 then invalidOp "Unknown ARC entry."
                let previous = rows[0].Get(0).AsInteger() - 1L
                // Reloaded data stays published even when the command fails, because another connection committed it.
                if previous <> stored.Revision then publish { readStored () with Revision = previous }
                match command stored with
                | None -> scope.Rollback()
                | Some next ->
                    scope.Commit()
                    publish { next with Revision = previous + 1L }
            finally scope.Close())
    let requireRow (state: State) (id: string) (kind: string) =
        if not (state.Entities |> List.exists (fun row -> row.Id = id && row.Kind = kind)) then
            invalidOp (kind + " is not registered in this ARC.")
    let sortByOrdinal (state: State) (ordinals: Map<string, int64>) =
        { state with Entities = state.Entities |> List.sortBy (fun row -> Map.find row.Id ordinals) }
    // The preset map carries recorded list positions for entities that an undo or redo restores.
    let applyState (previous: Stored) (next: State) (preset: Map<string, int64>) kind (action: string) operationId =
        Model.validate next
        let updated = Store.write db arcId preset previous.State next
        let nextCursor = if action = "undo" then previous.Cursor - 1 else previous.Cursor + 1
        if action = "apply" then
            execute "DELETE FROM history WHERE arc_id=$arc AND sequence > $cursor" ["cursor", SqlValue.Integer(int64 previous.Cursor)]
            let ordinalOf id = match Map.tryFind id updated with Some ordinal -> ordinal | None -> Map.find id preset
            execute "INSERT INTO history(arc_id,sequence,operation_id,kind,changes) VALUES($arc,$sequence,$id,$kind,$changes)"
                ["sequence", SqlValue.Integer(int64 nextCursor); "id", Store.text operationId; "kind", Store.text kind
                 "changes", Store.text (Codec.encodeChanges (Store.changes previous.State next ordinalOf))] |> ignore
        execute "UPDATE arc SET root_id=$root,cursor=$cursor WHERE arc_id=$arc"
            ["root", Store.text next.Root; "cursor", SqlValue.Integer(int64 nextCursor)] |> ignore
        { previous with State = sortByOrdinal next updated; Ordinals = updated; Cursor = nextCursor }
    do Model.validate stored.State
    do publish stored

    member _.ArcId = arcId
    member _.Repository = repository
    member _.IsClosed = closed
    member _.DatabasePath = repository.DatabasePath
    member _.Model = read (fun () -> unbox<Dataset> (hydrate stored.State.Root))
    member _.State = read (fun () -> stored.State)
    member _.Folder = read (fun () -> stored.Folder)
    member _.Baseline = read (fun () -> stored.Baseline)
    member _.Revision = read (fun () -> stored.Revision)
    member _.IsDirty = read (fun () -> stored.SavedGraph <> Some(Codec.encodeGraph stored.State))
    member _.HasSessionOnlyObjects = read (fun () -> Model.sessionOnly stored.State)
    member _.CanUndo = read (fun () -> stored.Cursor > 0)
    member _.CanRedo =
        read (fun () -> (query "SELECT sequence FROM history WHERE arc_id=$arc AND sequence=$sequence" ["sequence", SqlValue.Integer(int64 (stored.Cursor + 1))]).Count > 0)
    member _.ToYaml() = read (fun () -> Codec.encodeGraph stored.State)
    member _.Id(value: obj) = Model.required "entity" value |> ignore; Model.id value |> Model.entityId
    member _.List<'T>(kind: string) =
        read (fun () ->
            stored.State.Entities |> Seq.filter (fun row -> row.Kind = kind) |> Seq.map (fun row -> unbox<'T> (hydrate row.Id)) |> ResizeArray)
    member _.Get<'T>(kind: string, id: string) =
        read (fun () ->
            if not (index.ContainsKey id && index[id].Kind = kind) then invalidArg "id" ("Unknown " + kind)
            unbox<'T> (hydrate id))
    member _.GetEntity(id: string) =
        read (fun () ->
            if not (index.ContainsKey id) then invalidArg "id" "Unknown entity."
            unbox<EntityObject> (hydrate id))
    member _.ListEntities() =
        read (fun () -> stored.State.Entities |> Seq.map (fun row -> unbox<EntityObject> (hydrate row.Id)) |> ResizeArray)
    /// Applies a pure state transform as one history entry. An unchanged result writes nothing.
    member _.Apply(kind: string, transform: State -> State) : unit =
        run (fun previous ->
            let next = transform previous.State
            if List.isEmpty (Store.changes previous.State next (fun _ -> 0L)) then None
            else Some(applyState previous next previous.Ordinals kind "apply" (Identifier.newId())))
    member private _.Hydrated<'T>(id: string) : 'T =
        repository.Access(fun () ->
            ensureOpen()
            unbox<'T> (hydrate id))
    member this.Change<'T>(entity: obj, key: string, value: Cell option, kind: string) : 'T =
        let id, expected = this.Id(entity), Model.kind entity
        this.Apply(kind, fun state ->
            requireRow state id expected
            let rows = state.Entities |> List.map (fun row ->
                if row.Id <> id then row
                else { row with Properties = match value with Some v -> Map.add key v row.Properties | None -> Map.remove key row.Properties })
            { state with Entities = rows })
        this.Hydrated<'T>(id)
    member this.Extension<'T>(entity: EntityObject, key: string, value: Entity option, addOnly: bool) : 'T =
        let id = this.Id(entity)
        let captured = value |> Option.map (Model.captureExtension (fun target -> this.Id(target)))
        this.Apply(entity.Type + "." + (if value.IsNone then "removeProperty" elif addOnly then "addProperty" else "setProperty"), fun state ->
            requireRow state id entity.Type
            let rows = state.Entities |> List.map (fun row ->
                if row.Id <> id then row
                else
                    if addOnly && Map.containsKey key row.Extensions then invalidArg "key" "Duplicate extension property."
                    {row with Extensions = match captured with Some v -> Map.add key v row.Extensions | None -> Map.remove key row.Extensions})
            {state with Entities = rows})
        this.Hydrated<'T>(id)
    member this.Collection<'T>(entity: obj, key: string, target: obj, append: bool, kind: string) : 'T =
        let id, targetId, expected = this.Id(entity), this.Id(target), Model.kind entity
        this.Apply(kind, fun state ->
            requireRow state id expected
            let rows = state.Entities |> List.map (fun row ->
                if row.Id <> id then row else
                    let values = Model.links key row
                    if not append && not (List.contains targetId values) then invalidOp "Target is not attached."
                    let rec remove = function [] -> [] | head :: tail when head = targetId -> tail | head :: tail -> head :: remove tail
                    {row with Properties = Map.add key (Links(if append then values @ [targetId] else remove values)) row.Properties})
            {state with Entities = rows})
        this.Hydrated<'T>(id)
    member this.Delete(entity: obj) : unit =
        let id, expected = this.Id(entity), Model.kind entity
        this.Apply(expected + ".delete", fun state ->
            requireRow state id expected
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
                        let property = (Schema.tryProperty row.Kind key).Value
                        if remaining = [] && (match property.Shape with Schema.Shape.Links _ -> false | _ -> true) then
                            if property.Required then invalidOp ("Cannot delete required target of " + row.Kind + "." + key)
                            None
                        else Some(key,Links remaining)
                    | _ -> Some(key,cell)) |> Map.ofList
                {row with Properties = props})
            {state with Entities = rows})
    member private this.Adopt<'T>(value: obj, kind: string option, strict: bool, recursive: bool) : 'T =
        Model.required "entity" value |> ignore
        let accepts = match kind with Some expected -> (fun actual -> actual = expected) | None -> (fun actual -> not (Schema.isCore actual))
        let rootId = ref ""
        this.Apply(Model.kind value + (if strict then ".register" else ".set"), fun state ->
            let next, id = Adoption.adopt state accepts value strict recursive
            rootId.Value <- id
            next)
        this.Hydrated<'T>(rootId.Value)
    member this.Add<'T>(value: obj, kind: string option) : 'T = this.Adopt<'T>(value, kind, true, false)
    member this.Upsert<'T>(value: obj, kind: string option, recursive: bool) : 'T = this.Adopt<'T>(value, kind, false, recursive)
    member _.History(redo: bool) : unit =
        run (fun previous ->
            let sequence = if redo then previous.Cursor + 1 else previous.Cursor
            let rows = query "SELECT * FROM history WHERE arc_id=$arc AND sequence=$sequence" ["sequence", SqlValue.Integer(int64 sequence)]
            if sequence <= 0 || rows.Count = 0 then invalidOp (if redo then "Nothing to redo." else "Nothing to undo.")
            let row = rows[0]
            let records = row.GetByName("changes").AsText() |> Codec.decodeChanges
            let next, preset = Store.applyChanges previous.State previous.Ordinals records redo
            Some(applyState previous next preset (row.GetByName("kind").AsText()) (if redo then "redo" else "undo") (row.GetByName("operation_id").AsText())))
    /// Binds the entry to a folder. The check runs against the freshly read binding inside the write transaction.
    member _.SetBinding(path: string, check: unit -> unit) : unit =
        run (fun previous ->
            if previous.Folder = Some path then None
            else
                check ()
                execute "UPDATE arc SET folder=$folder,baseline=NULL,saved_graph=NULL WHERE arc_id=$arc" ["folder", Store.text path] |> ignore
                Some { previous with Folder = Some path; Baseline = None; SavedGraph = None })
    /// Records a checkpoint. The publication callback writes the exported graph and returns its text.
    member _.Checkpoint(publication: Stored -> string) : unit =
        run (fun previous ->
            let graph = publication previous
            execute "UPDATE arc SET baseline=$graph,saved_graph=$graph WHERE arc_id=$arc" ["graph", Store.text graph] |> ignore
            Some { previous with Baseline = Some graph; SavedGraph = Some graph })
    member _.AcceptPersistentBaseline(current: string option) : unit =
        run (fun previous ->
            if current = previous.Baseline then None
            else
                execute "UPDATE arc SET baseline=$baseline,saved_graph=NULL WHERE arc_id=$arc" ["baseline", Store.optional current] |> ignore
                Some { previous with Baseline = current; SavedGraph = None })
    member _.Reload(next: State, current: string) : unit =
        run (fun previous ->
            Model.validate next
            Store.archive db arcId previous.State |> ignore
            execute "DELETE FROM history WHERE arc_id=$arc" [] |> ignore
            let updated = Store.write db arcId previous.Ordinals previous.State next
            let sorted = sortByOrdinal next updated
            let graph = Codec.encodeGraph sorted
            execute "UPDATE arc SET root_id=$root,baseline=$baseline,saved_graph=$saved,cursor=0 WHERE arc_id=$arc"
                ["root", Store.text next.Root; "baseline", Store.text current; "saved", Store.text graph] |> ignore
            Some { previous with State = sorted; Ordinals = updated; Cursor = 0; Baseline = Some current; SavedGraph = Some graph })
    member _.Close() =
        if not closed then
            closed <- true
            repository.Forget(arcId)
            cache.Clear()
