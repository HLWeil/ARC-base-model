namespace ARCtrl.Internal

open YAMLicious.YAMLiciousTypes

module internal Codec =
    let text value = YAMLElement.Value(YAMLContent.create(value))
    let mapping pairs =
        pairs |> List.map (fun (key, value) -> YAMLElement.Mapping(YAMLContent.create(key), value)) |> YAMLElement.Object
    let sequence values = YAMLElement.Sequence(values)
    let optional key value = value |> Option.map (fun value -> key, text value) |> Option.toList
    let collection key values = if List.isEmpty values then [] else [key, sequence values]
    let strings key values = collection key (List.map text values)
    let write element = YAMLicious.Writer.write element None
    let fields element =
        match element with
        | YAMLElement.Object values ->
            let pairs = values |> List.map (function
                | YAMLElement.Mapping(key, value) -> key.Value, value
                | _ -> invalidOp "Expected a YAML object.")
            if pairs |> List.map fst |> List.distinct |> List.length <> pairs.Length then
                invalidOp "Duplicate YAML fields are not supported."
            pairs
        | _ -> invalidOp "Expected a YAML object."
    let field key element = fields element |> List.tryPick (fun (name, value) -> if name = key then Some value else None)
    let scalar element =
        match element with
        | YAMLElement.Value value -> value.Value
        | YAMLElement.Object [YAMLElement.Value value] -> value.Value
        | _ -> invalidOp "Expected a YAML string value."
    let required key element = field key element |> Option.defaultWith (fun () -> invalidOp ("Missing YAML field: " + key))
    let value key element = required key element |> scalar
    let opt key element = field key element |> Option.map scalar
    let items key element =
        match field key element with
        | None -> []
        | Some(YAMLElement.Sequence values) -> values
        | Some(YAMLElement.Object [YAMLElement.Sequence values]) -> values
        | Some _ -> invalidOp ("Expected a sequence for " + key)
    let stringItems key element = items key element |> List.map scalar
    let check kind allowed element =
        if value "type" element <> kind then invalidOp ("Expected type " + kind)
        for key, _ in fields element do
            if not (List.contains key allowed) then invalidOp ("Unsupported " + kind + " property: " + key)
    let id element = opt "id" element |> Option.defaultWith Files.newId
    let datasetFields (row: DatasetRow) =
        [ "type", text "Dataset"; "id", text row.Id ]
        @ strings "additionalTypes" row.Types @ strings "conformsTo" row.Profiles @ strings "identifiers" row.Identifiers
        @ optional "title" row.Title @ optional "description" row.Description @ optional "license" row.License
        @ optional "datePublished" row.Published @ optional "dateCreated" row.Created @ optional "dateModified" row.Modified
    let sampleElement (row: SampleRow) =
        mapping (["type", text "Sample"; "id", text row.Id; "name", text row.Name] @ strings "additionalTypes" row.Types)
    let processFields (row: ProcessRow) =
        ["type", text "Process"; "id", text row.Id; "name", text row.Name] @ strings "additionalTypes" row.Types
    let readDataset element : DatasetRow =
        check "Dataset" ["type"; "id"; "additionalTypes"; "conformsTo"; "identifiers"; "title"; "description"; "license";
                         "datePublished"; "dateCreated"; "dateModified"; "hasParts"; "processes"] element
        { Id = id element; Types = stringItems "additionalTypes" element; Profiles = stringItems "conformsTo" element
          Identifiers = stringItems "identifiers" element; Title = opt "title" element; Description = opt "description" element
          License = opt "license" element; Published = opt "datePublished" element; Created = opt "dateCreated" element
          Modified = opt "dateModified" element; Parts = []; Processes = [] }
    let readSample element : SampleRow =
        check "Sample" ["type"; "id"; "name"; "additionalTypes"] element
        { Id = id element; Types = stringItems "additionalTypes" element; Name = value "name" element }
    let readProcess element : ProcessRow =
        check "Process" ["type"; "id"; "name"; "additionalTypes"; "input"; "output"] element
        { Id = id element; Types = stringItems "additionalTypes" element; Name = value "name" element; Input = None; Output = None }
    let encodeState state =
        mapping (["root", text state.Root]
                 @ collection "datasets" (state.Datasets |> List.map (fun row -> mapping (datasetFields row @ strings "hasParts" row.Parts @ strings "processes" row.Processes)))
                 @ collection "processes" (state.Processes |> List.map (fun row -> mapping (processFields row @ optional "input" row.Input @ optional "output" row.Output)))
                 @ collection "samples" (state.Samples |> List.map sampleElement)) |> write
    let decodeState source =
        let element = YAMLicious.Reader.read source
        { Root = value "root" element
          Datasets = items "datasets" element |> List.map (fun e -> { readDataset e with Parts = stringItems "hasParts" e; Processes = stringItems "processes" e })
          Processes = items "processes" element |> List.map (fun e -> { readProcess e with Input = opt "input" e; Output = opt "output" e })
          Samples = items "samples" element |> List.map readSample }
    let rootElement state =
        let sample id = state.Samples |> List.find (fun row -> row.Id = id) |> sampleElement
        let proc id =
            let row = state.Processes |> List.find (fun row -> row.Id = id)
            mapping (processFields row
                     @ (row.Input |> Option.map (fun id -> "input", sample id) |> Option.toList)
                     @ (row.Output |> Option.map (fun id -> "output", sample id) |> Option.toList))
        let rec dataset id =
            let row = state.Datasets |> List.find (fun row -> row.Id = id)
            mapping (datasetFields row @ collection "hasParts" (List.map dataset row.Parts)
                     @ collection "processes" (List.map proc row.Processes))
        dataset state.Root
    let encodeGraph state = rootElement state |> write
    let decodeGraph source =
        let datasets = ResizeArray<DatasetRow>()
        let processes = ResizeArray<ProcessRow>()
        let samples = ResizeArray<SampleRow>()
        let endpoint element =
            match element with
            | YAMLElement.Value _ -> scalar element
            | YAMLElement.Object [YAMLElement.Value _] -> scalar element
            | _ ->
                let row = readSample element
                match samples |> Seq.tryFind (fun existing -> existing.Id = row.Id) with
                | Some existing when existing <> row -> invalidOp ("Conflicting sample definitions: " + row.Id)
                | Some _ -> ()
                | None -> samples.Add(row)
                row.Id
        let proc element =
            let row = { readProcess element with Input = field "input" element |> Option.map endpoint; Output = field "output" element |> Option.map endpoint }
            processes.Add(row)
            row.Id
        let rec dataset element =
            let row = { readDataset element with Parts = items "hasParts" element |> List.map dataset; Processes = items "processes" element |> List.map proc }
            datasets.Add(row)
            row.Id
        let root = YAMLicious.Reader.read source |> dataset
        { Root = root; Datasets = List.ofSeq datasets; Processes = List.ofSeq processes; Samples = List.ofSeq samples }
