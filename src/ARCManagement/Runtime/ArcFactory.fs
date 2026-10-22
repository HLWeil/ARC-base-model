namespace ARCManagement.Internal

open ARCManagement.Helper
open ARCBaseModel

/// Graph adoption without workspace paths or filesystem policy.
module internal ArcFactory =
    let load (repository: Repository) arcId =
        try GraphContext(repository,arcId)
        with _ -> repository.Forget(arcId); reraise()

    /// Prepare management IDs in a snapshot, without modifying the input graph.
    let snapshot (root: Dataset) =
        Model.required "rootDataset" root |> ignore
        let state, rootId = Adoption.adopt { Root = ""; Entities = [] } (fun kind -> kind = "Dataset") (box root) true true
        let state = { state with Root = rootId }
        Model.validate state
        state

    /// Initialize optional persisted metadata in the same transaction as adoption.
    let createWithMetadata (repository: Repository) root folder baseline =
        repository.Access(fun () ->
            let arcId = Identifier.newId()
            let state = snapshot root
            repository.Connection.WithTransaction(fun () ->
                Store.addArc repository.Connection arcId state folder baseline
                GraphContext(repository,arcId)))

    let create repository root = createWithMetadata repository root None None
