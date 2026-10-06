namespace ARCtrl.Internal

open YAMLicious.YAMLiciousTypes
open System.Globalization
open Fable.Core

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
#if FABLE_COMPILER_PYTHON
    [<Emit("str(float($0))")>]
    let private numericText (_value: float): string = nativeOnly
#else
#if FABLE_COMPILER
    [<Emit("String($0)")>]
    let private numericText (_value: float): string = nativeOnly
#else
    let private numericText (value: float) = value.ToString("R", CultureInfo.InvariantCulture)
#endif
#endif
    let number (v: float) = YAMLElement.Value(YAMLContent.create(numericText v, tag = "!!float"))
    let cellElement cell =
        match cell with
        | Text v -> mapping ["storage", text "text"; "value", text v]
        | Number v -> mapping ["storage", text "number"; "value", number v]
        | Texts vs -> mapping ["storage", text "texts"; "values", sequence (List.map text vs)]
        | Links vs -> mapping ["storage", text "links"; "values", sequence (List.map text vs)]
    let rowElement row =
        mapping (["id", text row.Id; "type", text row.Kind
                  "properties", row.Properties |> Map.toList |> List.map (fun (key,cell) -> key, cellElement cell) |> mapping] @ optional "suppliedId" row.SuppliedId)
    let readRow element =
        let properties = required "properties" element |> fields |> List.map (fun (key,e) ->
            key, match value "storage" e with
                 | "text" -> Text(value "value" e)
                 | "number" -> Number(System.Double.Parse(value "value" e, CultureInfo.InvariantCulture))
                 | "texts" -> Texts(stringItems "values" e)
                 | "links" -> Links(stringItems "values" e)
                 | _ -> invalidOp "Unknown stored property type.") |> Map.ofList
        { Id = value "id" element; Kind = value "type" element; SuppliedId = opt "suppliedId" element; Properties = properties }
    let encodeState state = mapping ["root", text state.Root; "entities", sequence (List.map rowElement state.Entities)] |> write
    let decodeState source =
        let element = YAMLicious.Reader.read source
        { Root = value "root" element; Entities = items "entities" element |> List.map readRow }
    let rootElement state =
        let rows = state.Entities |> List.map (fun row -> row.Id, row) |> Map.ofList
        let emitted = System.Collections.Generic.HashSet<string>()
        let rec entity id =
            if not (emitted.Add id) then mapping ["$ref", text id]
            else
                let row = rows[id]
                let properties = row.Properties |> Map.toList |> List.choose (fun (key,cell) ->
                    match cell with
                    | Text v when row.Kind = "Annotation" && key = "value" ->
                        Some(key, YAMLElement.Value(YAMLContent.create(v, tag = "!!str")))
                    | Text v -> Some(key,text v)
                    | Number v -> Some(key,number v)
                    | Texts [] | Links [] -> None
                    | Texts vs -> Some(key,sequence(List.map text vs))
                    | Links vs ->
                        let _,shape,_,_ = Model.specifications row.Kind |> List.find (fun (name,_,_,_) -> name = key)
                        Some(key,if shape = "links" then sequence(List.map entity vs) else entity (List.exactlyOne vs)))
                mapping (["type", text row.Kind; "id", text row.Id] @ properties)
        entity state.Root
    let encodeGraph state = rootElement state |> write
    let decodeGraph source =
        let rows = System.Collections.Generic.Dictionary<string,EntityRow>()
        let rec entity element =
            match element with
            | YAMLElement.Value _ | YAMLElement.Object [YAMLElement.Value _] -> scalar element
            | _ when field "$ref" element |> Option.isSome ->
                if (fields element).Length <> 1 then invalidOp "Reference objects contain only $ref."
                value "$ref" element
            | _ ->
                let kind = value "type" element
                let suppliedId = id element
                let specs = Model.specifications kind
                let properties = fields element |> List.choose (fun (key,e) ->
                    if key = "id" || key = "type" then None
                    else
                        let _,shape,targets,_ = specs |> List.tryFind (fun (name,_,_,_) -> name = key) |> Option.defaultWith (fun () -> invalidOp ("Unknown property: " + key))
                        let values () = match e with YAMLElement.Sequence vs | YAMLElement.Object [YAMLElement.Sequence vs] -> vs | _ -> invalidOp ("Expected collection: " + key)
                        let cell =
                            match shape with
                            | "strings" -> Texts(values() |> List.map scalar)
                            | "links" -> Links(values() |> List.map entity)
                            | "link" -> Links [entity e]
                            | "text" -> Text(scalar e)
                            | _ when kind = "Annotation" && key = "value" ->
                                let content = match e with YAMLElement.Value v | YAMLElement.Object [YAMLElement.Value v] -> v | _ -> invalidOp "Expected Annotation value."
                                let mutable numeric = 0.0
                                if content.Tag <> Some "!!str" && content.Style <> Some ScalarStyle.DoubleQuoted && content.Style <> Some ScalarStyle.SingleQuoted && System.Double.TryParse(content.Value, NumberStyles.Float, CultureInfo.InvariantCulture, &numeric) then Number numeric
                                else Text content.Value
                            | _ ->
                                match e with
                                | YAMLElement.Value _ | YAMLElement.Object [YAMLElement.Value _] when targets <> "Sample|Data" -> Text(scalar e)
                                | _ -> Links [entity e]
                        Some(key,cell)) |> Map.ofList
                // All collections exist even when absent on the wire.
                let properties = specs |> List.fold (fun props (key,shape,_,_) ->
                    if Map.containsKey key props then props
                    elif shape = "strings" then Map.add key (Texts []) props
                    elif shape = "links" then Map.add key (Links []) props else props) properties
                let row = { Id = suppliedId; Kind = kind; SuppliedId = opt "id" element; Properties = properties }
                match rows.TryGetValue suppliedId with
                | true, previous when previous <> row -> invalidOp ("Conflicting entity definitions: " + suppliedId)
                | true, _ -> ()
                | _ -> rows.Add(suppliedId,row)
                suppliedId
        let root = YAMLicious.Reader.read source |> entity
        { Root = root; Entities = rows.Values |> Seq.toList }
