namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type DatasetOperations internal (session: Session) =
    member _.create(identifier: string) =
        let value = Dataset(["process-provenance"], [Model.required "identifier" identifier])
        session.Register(value, "Dataset"); value
    member _.register(value: Dataset) = session.Register(value, "Dataset"); value
    member _.get(id: string) = session.GetDataset(id)
    member _.list() = session.Datasets()
    member _.setIdentifiers(value: Dataset, identifiers: seq<string>) =
        let id = session.DatasetId(value)
        let identifiers = List.ofSeq identifiers
        session.Execute("Dataset.setIdentifiers", fun state -> { state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = id then {row with Identifiers = identifiers} else row) })
    member _.setTitle(value: Dataset, title: string) =
        let id = session.DatasetId(value)
        session.Execute("Dataset.setTitle", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = id then {row with Title = Some(Model.required "title" title)} else row)})
    member _.clearTitle(value: Dataset) =
        let id = session.DatasetId(value)
        session.Execute("Dataset.clearTitle", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = id then {row with Title = None} else row)})
    member _.setDescription(value: Dataset, description: string) =
        let id = session.DatasetId(value)
        session.Execute("Dataset.setDescription", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = id then {row with Description = Some(Model.required "description" description)} else row)})
    member _.clearDescription(value: Dataset) =
        let id = session.DatasetId(value)
        session.Execute("Dataset.clearDescription", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = id then {row with Description = None} else row)})
    member _.addPart(parent: Dataset, child: Dataset) =
        let parentId, childId = session.DatasetId(parent), session.DatasetId(child)
        session.Execute("Dataset.addPart", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = parentId then {row with Parts = row.Parts @ [childId]} else row)})
    member _.removePart(parent: Dataset, child: Dataset) =
        let parentId, childId = session.DatasetId(parent), session.DatasetId(child)
        if not (parent.HasParts |> Seq.exists (fun value -> obj.ReferenceEquals(value, child))) then invalidOp "Child is not attached to this Dataset."
        session.Execute("Dataset.removePart", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = parentId then {row with Parts = row.Parts |> List.filter ((<>) childId)} else row)})
    member _.movePart(child: Dataset, destination: Dataset) =
        let childId, destinationId = session.DatasetId(child), session.DatasetId(destination)
        session.Execute("Dataset.movePart", fun state ->
            let nextDatasets = state.Datasets |> List.map (fun row ->
                let parts = row.Parts |> List.filter ((<>) childId)
                {row with Parts = if row.Id = destinationId then parts @ [childId] else parts})
            {state with Datasets = nextDatasets})
    member _.addProcess(parent: Dataset, proc: ARCBaseModel.Process) =
        let parentId, processId = session.DatasetId(parent), session.ProcessId(proc)
        session.Execute("Dataset.addProcess", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = parentId then {row with Processes = row.Processes @ [processId]} else row)})
    member _.removeProcess(parent: Dataset, proc: ARCBaseModel.Process) =
        let parentId, processId = session.DatasetId(parent), session.ProcessId(proc)
        if not (parent.Processes |> Seq.exists (fun value -> obj.ReferenceEquals(value, proc))) then invalidOp "Process is not attached to this Dataset."
        session.Execute("Dataset.removeProcess", fun state -> {state with Datasets = state.Datasets |> List.map (fun row -> if row.Id = parentId then {row with Processes = row.Processes |> List.filter ((<>) processId)} else row)})
    member _.moveProcess(proc: ARCBaseModel.Process, destination: Dataset) =
        let processId, destinationId = session.ProcessId(proc), session.DatasetId(destination)
        session.Execute("Dataset.moveProcess", fun state ->
            let nextDatasets = state.Datasets |> List.map (fun row ->
                let processes = row.Processes |> List.filter ((<>) processId)
                {row with Processes = if row.Id = destinationId then processes @ [processId] else processes})
            {state with Datasets = nextDatasets})
    member _.delete(value: Dataset) =
        let id = session.DatasetId(value)
        session.Execute("Dataset.delete", fun state ->
            if state.Root = id then invalidOp "The root Dataset cannot be deleted."
            let rec descendants id =
                let row = state.Datasets |> List.find (fun row -> row.Id = id)
                id :: (row.Parts |> List.collect descendants)
            let deleted = descendants id
            let deletedProcesses = state.Datasets |> List.filter (fun row -> List.contains row.Id deleted) |> List.collect (fun row -> row.Processes)
            {state with Datasets = state.Datasets |> List.filter (fun row -> not (List.contains row.Id deleted)) |> List.map (fun row -> {row with Parts = row.Parts |> List.filter (fun child -> not (List.contains child deleted))})
                        Processes = state.Processes |> List.filter (fun row -> not (List.contains row.Id deletedProcesses))})
