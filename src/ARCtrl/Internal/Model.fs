namespace ARCtrl.Internal

open ARCBaseModel

module internal Model =
    let required name value = if isNull (box value) then nullArg name else value
    let entityId value = value |> Option.defaultWith (fun () -> invalidOp "Entity has no session ID; register it first.")
    let noValues name (values: seq<'T>) = if not (Seq.isEmpty values) then invalidOp ("Unsupported prototype property: " + name)
    let sampleRow id (sample: Sample) : SampleRow =
        noValues "Sample.AdditionalProperties" sample.AdditionalProperties
        { Id = id; Types = List.ofSeq sample.AdditionalTypes; Name = required "name" sample.Name }
    let processRow id endpoint (proc: ARCBaseModel.Process) : ProcessRow =
        if proc.ExecutesRecipe.IsSome then invalidOp "Recipes are outside the prototype."
        noValues "Process.ParameterValues" proc.ParameterValues
        { Id = id; Types = List.ofSeq proc.AdditionalTypes; Name = required "name" proc.Name
          Input = proc.Input |> Option.map endpoint; Output = proc.Output |> Option.map endpoint }
    let datasetRow id child proc (dataset: Dataset) : DatasetRow =
        noValues "Dataset.DataFiles" dataset.DataFiles
        noValues "Dataset.Agents" dataset.Agents
        noValues "Dataset.Citations" dataset.Citations
        noValues "Dataset.Descriptors" dataset.Descriptors
        noValues "Dataset.AdditionalProperties" dataset.AdditionalProperties
        { Id = id; Types = List.ofSeq dataset.AdditionalTypes; Profiles = List.ofSeq dataset.ConformsTo; Identifiers = List.ofSeq dataset.Identifiers
          Title = dataset.Title; Description = dataset.Description; License = dataset.License; Published = dataset.DatePublished
          Created = dataset.DateCreated; Modified = dataset.DateModified
          Parts = dataset.HasParts |> Seq.map child |> List.ofSeq; Processes = dataset.Processes |> Seq.map proc |> List.ofSeq }
    let validate state =
        let ids = [yield! state.Datasets |> List.map (fun row -> row.Id); yield! state.Processes |> List.map (fun row -> row.Id); yield! state.Samples |> List.map (fun row -> row.Id)]
        if ids.Length <> (List.distinct ids).Length then invalidOp "Different entities cannot share a session ID."
        let datasetIds = state.Datasets |> List.map (fun row -> row.Id)
        let processIds = state.Processes |> List.map (fun row -> row.Id)
        let sampleIds = state.Samples |> List.map (fun row -> row.Id)
        if not (List.contains state.Root datasetIds) then invalidOp "Root Dataset is missing."
        let parents = state.Datasets |> List.collect (fun row -> row.Parts)
        let owners = state.Datasets |> List.collect (fun row -> row.Processes)
        if (List.distinct parents).Length <> parents.Length || (List.distinct owners).Length <> owners.Length then
            invalidOp "The prototype permits only one membership per Dataset or Process."
        if List.contains state.Root parents then invalidOp "The root Dataset cannot be nested."
        for row in state.Datasets do
            Dataset(row.Profiles, row.Identifiers) |> ignore
            for value in row.Types @ row.Identifiers @ row.Profiles do required "text" value |> ignore
            for value in [row.Title; row.Description; row.License; row.Published; row.Created; row.Modified] |> List.choose id do required "text" value |> ignore
            for child in row.Parts do if not (List.contains child datasetIds) then invalidOp "Unknown child Dataset."
            for proc in row.Processes do if not (List.contains proc processIds) then invalidOp "Unknown Process."
        for row in state.Processes do
            required "name" row.Name |> ignore
            for value in row.Types do required "text" value |> ignore
            for endpoint in [row.Input; row.Output] |> List.choose id do
                if not (List.contains endpoint sampleIds) then invalidOp "Unknown sample endpoint."
        for row in state.Samples do
            required "name" row.Name |> ignore
            for value in row.Types do required "text" value |> ignore
        let rec visit path id =
            if List.contains id path then invalidOp "Dataset nesting cannot contain cycles."
            let row = state.Datasets |> List.find (fun row -> row.Id = id)
            for child in row.Parts do visit (id :: path) child
        for row in state.Datasets do visit [] row.Id
    let reachable state =
        let rec datasets id =
            let row = state.Datasets |> List.find (fun row -> row.Id = id)
            row :: (row.Parts |> List.collect datasets)
        let ds = datasets state.Root
        let ps = ds |> List.collect (fun row -> row.Processes)
        let ss = state.Processes |> List.filter (fun row -> List.contains row.Id ps)
                 |> List.collect (fun row -> [row.Input; row.Output] |> List.choose id) |> List.distinct
        ds.Length + ps.Length + ss.Length
    let sessionOnly state = reachable state < state.Datasets.Length + state.Processes.Length + state.Samples.Length
