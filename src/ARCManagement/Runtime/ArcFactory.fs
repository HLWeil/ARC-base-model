namespace ARCManagement.Internal

open ARCManagement.Helper
open ARCBaseModel

/// Graph adoption and hydration without workspace paths or filesystem policy.
module internal ArcFactory =
    let load (repository: Repository) arcId =
        try GraphContext(repository,arcId,[])
        with _ -> repository.Forget(arcId); reraise()

    let private capture assertAvailable (root: Dataset) =
        Model.required "rootDataset" root |> ignore
        let objects = ResizeArray<string * obj>()
        let rows = ResizeArray<EntityRow>()
        let rec visit value =
            Model.required "entity" value |> ignore
            assertAvailable value
            match objects |> Seq.tryFind (fun (_,existing) -> obj.ReferenceEquals(existing,value)) with
            | Some(id,_) -> id
            | None ->
                let id = Model.id value |> Option.defaultWith Identifier.newId
                if objects |> Seq.exists (fun (existing,_) -> existing = id) then invalidOp ("Session ID collision: " + id)
                objects.Add(id,value)
                rows.Add(Model.capture id visit value)
                id
        let rootId = visit root
        let state = { Root=rootId; Entities=List.ofSeq rows }
        Model.validate state
        state,List.ofSeq objects

    /// Prepare management IDs in a snapshot, without modifying the input graph.
    let snapshot root = capture ignore root |> fst

    /// Initialize optional persisted metadata in the same transaction as adoption.
    let createWithMetadata (repository: Repository) root folder baseline =
        repository.Access(fun () ->
            let arcId = Identifier.newId()
            let state,objects = capture (fun value -> repository.AssertAvailable(arcId,value)) root
            let originalIds = objects |> List.map (fun (_,value) -> value,Model.id value)
            try
                repository.Connection.WithTransaction(fun () ->
                    Store.addArc repository.Connection arcId state folder baseline
                    GraphContext(repository,arcId,objects))
            with _ ->
                for value,id in originalIds do
                    repository.Release(arcId,value)
                    Model.setId value id
                reraise())

    let create repository root = createWithMetadata repository root None None
