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
        let identities = System.Collections.Generic.Dictionary<obj,string>(HashIdentity.Reference)
        let ids = System.Collections.Generic.HashSet<string>()
        let rows = ResizeArray<EntityRow>()
        let register value =
            Model.required "entity" value |> ignore
            assertAvailable value
            match identities.TryGetValue value with
            | true,id -> id
            | _ ->
                let id=Model.id value |> Option.defaultWith Identifier.newId
                if not (ids.Add id) then invalidOp ("Session ID collision: "+id)
                identities.Add(value,id)
                objects.Add(id,value)
                id
        let rootId=register (box root)
        let mutable index=0
        while index<objects.Count do
            let id,value=objects[index]
            index<-index+1
            rows.Add(Model.capture id register value)
        let state = { Root=rootId; Entities=List.ofSeq rows }
        Model.validate state
        state,List.ofSeq objects

    /// Prepare management IDs in a snapshot, without modifying the input graph.
    let snapshot root = capture ignore root |> fst

    /// Initialize graph and workspace metadata in one transaction without mutating inputs.
    let createWithMetadata (repository: Repository) root folder baseline =
        repository.Access(fun () ->
            let arcId = Identifier.newId()
            let state,_ = capture ignore root
            repository.Connection.WithTransaction(fun () ->
                Store.addArc repository.Connection arcId state folder baseline
                GraphContext(repository,arcId,[])))

    let create repository root = createWithMetadata repository root None None
