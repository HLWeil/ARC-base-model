namespace ARCtrl.Internal

open ARCtrl.Helper

open ARCBaseModel
open ARCSession.Internal

module internal ArcFactory =
    let load (repository: Repository) arcId =
        try Session(repository,arcId,[])
        with _ -> repository.Forget(arcId); reraise()
    let create (repository: Repository) (root: Dataset) =
        Model.required "rootDataset" root |> ignore
        repository.Access(fun () ->
            let arcId = Identifier.newId()
            let objects = ResizeArray<string * obj>()
            let rows = ResizeArray<EntityRow>()
            let rec capture value =
                Model.required "entity" value |> ignore
                repository.AssertAvailable(arcId,value)
                match objects |> Seq.tryFind (fun (_,existing) -> obj.ReferenceEquals(existing,value)) with
                | Some(id,_) -> id
                | None ->
                    let id = Model.id value |> Option.defaultWith Identifier.newId
                    if objects |> Seq.exists (fun (existing,_) -> existing = id) then invalidOp ("Session ID collision: " + id)
                    objects.Add(id,value)
                    rows.Add(Model.capture id capture value)
                    id
            let state = {Root=capture root; Entities=[]}
            let state = {state with Entities=List.ofSeq rows}
            Model.validate state
            let originalIds = objects |> Seq.map (fun (_,value) -> value,Model.id value) |> Seq.toList
            try
                repository.Connection.WithTransaction(fun () ->
                    Store.addArc repository.Connection arcId state None None
                    Session(repository,arcId,List.ofSeq objects))
            with _ ->
                for value,id in originalIds do
                    repository.Release(arcId,value)
                    Model.setId value id
                reraise())
    let import (repository: Repository) folder =
        let folder = Path.fullPath(Model.required "folder" folder)
        let current = Path.readFileText(Path.combineNative folder Path.ARCFileName)
        let state = Codec.decodeGraph current
        Model.validate state
        repository.Access(fun () ->
            let arcId = Identifier.newId()
            repository.Connection.WithTransaction(fun () ->
                Store.addArc repository.Connection arcId state (Some folder) (Some current)
                load repository arcId))
