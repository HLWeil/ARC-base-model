namespace ARCtrl.Internal

open ARCtrl.Helper

open YAMLicious.YAMLiciousTypes
open System.Globalization
open Fable.Core

module internal Codec =
    let text value = YAMLElement.Value(YAMLContent.create(value))
    let mapping pairs =
        pairs |> List.map (fun (key, value) -> YAMLElement.Mapping(YAMLContent.create(key), value)) |> YAMLElement.Object
    let extensionMappings pairs =
        let keyContent (key: string) =
            let mutable numeric = 0.0
            let implicitScalar =
                List.contains (key.ToLowerInvariant()) ["true"; "false"; "null"; "~"; "yes"; "no"; "on"; "off"; ".nan"; ".inf"; "-.inf"; "+.inf"] ||
                System.Double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, &numeric)
            // '$' introduces YAMLicious array syntax; '['/'{' also interfere with its key parser.
            if YAMLicious.Writer.StyleVerifier.isPlainSafe key && not (key.StartsWith("$") || key.Contains("[") || key.Contains("{")) && not implicitScalar then
                YAMLContent.create(key)
            else YAMLContent.create(key, style = ScalarStyle.DoubleQuoted)
        pairs |> List.map (fun (key, value) -> YAMLElement.Mapping(keyContent key, value))
    let extensionMapping pairs = extensionMappings pairs |> YAMLElement.Object
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
    let id element = opt "id" element |> Option.defaultWith Identifier.newId
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
    let number (v: float) = YAMLElement.Value(YAMLContent.create(numericText v, style = ScalarStyle.Plain))
    let rec extensionElement cell =
        let scalar kind v = mapping ["storage", text kind; "value", v]
        match cell with
        | ExtensionText v -> scalar "text" (text v)
        | ExtensionNumber v -> scalar "number" (number v)
        | ExtensionBool v -> scalar "bool" (text (if v then "true" else "false"))
        | ExtensionNull -> mapping ["storage", text "null"]
        | ExtensionBlob v -> scalar "blob" (text v)
        | ExtensionObject v -> scalar "object" (text v)
        | ExtensionCollection vs -> mapping ["storage", text "collection"; "values", sequence(List.map extensionElement vs)]
    let rec readExtension e =
        match value "storage" e with
        | "text" -> ExtensionText(value "value" e)
        | "number" -> ExtensionNumber(System.Double.Parse(value "value" e, CultureInfo.InvariantCulture))
        | "bool" ->
            match value "value" e with
            | "true" -> ExtensionBool true
            | "false" -> ExtensionBool false
            | _ -> invalidOp "Invalid stored Boolean."
        | "null" -> ExtensionNull
        | "blob" -> ExtensionBlob(value "value" e)
        | "object" -> ExtensionObject(value "value" e)
        | "collection" -> ExtensionCollection(items "values" e |> List.map readExtension)
        | _ -> invalidOp "Unknown extension storage type."
    let cellElement cell =
        match cell with
        | Text v -> mapping ["storage", text "text"; "value", text v]
        | Number v -> mapping ["storage", text "number"; "value", number v]
        | Texts vs -> mapping ["storage", text "texts"; "values", sequence (List.map text vs)]
        | Links vs -> mapping ["storage", text "links"; "values", sequence (List.map text vs)]
    let rowElement row =
        mapping (["id", text row.Id; "type", text row.Kind
                  "extensions", row.Extensions |> Map.toList |> List.map (fun (key,cell) -> key, extensionElement cell) |> extensionMapping
                  "properties", row.Properties |> Map.toList |> List.map (fun (key,cell) -> key, cellElement cell) |> mapping] @ optional "suppliedId" row.SuppliedId)
    let readRow element =
        let properties = required "properties" element |> fields |> List.map (fun (key,e) ->
            key, match value "storage" e with
                 | "text" -> Text(value "value" e)
                 | "number" -> Number(System.Double.Parse(value "value" e, CultureInfo.InvariantCulture))
                 | "texts" -> Texts(stringItems "values" e)
                 | "links" -> Links(stringItems "values" e)
                 | _ -> invalidOp "Unknown stored property type.") |> Map.ofList
        { Id = value "id" element; Kind = value "type" element; SuppliedId = opt "suppliedId" element; Properties = properties; Extensions = field "extensions" element |> Option.map (fields >> List.map (fun (key,e) -> key, readExtension e) >> Map.ofList) |> Option.defaultValue Map.empty }
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
                        Some(key, YAMLElement.Value(YAMLContent.create(v, style = ScalarStyle.DoubleQuoted)))
                    | Text v -> Some(key,text v)
                    | Number v -> Some(key,number v)
                    | Texts [] | Links [] -> None
                    | Texts vs -> Some(key,sequence(List.map text vs))
                    | Links vs ->
                        let _,shape,_,_ = Model.specifications row.Kind |> List.find (fun (name,_,_,_) -> name = key)
                        Some(key,if shape = "links" then sequence(List.map entity vs) else entity (List.exactlyOne vs)))
                let baseFields = match mapping (["type", text row.Kind; "id", text row.Id] @ properties) with YAMLElement.Object fields -> fields | _ -> invalidOp "Expected mapping."
                YAMLElement.Object(baseFields @ extensionMappings (row.Extensions |> Map.toList |> List.map (fun (key,cell) -> key, extension cell)))
        and extension = function
            | ExtensionObject id -> entity id
            | ExtensionCollection values -> sequence(List.map extension values)
            | ExtensionText v -> YAMLElement.Value(YAMLContent.create(v, style = ScalarStyle.DoubleQuoted))
            | ExtensionNumber v -> number v
            | ExtensionBool v -> YAMLElement.Value(YAMLContent.create((if v then "true" else "false"), style = ScalarStyle.Plain))
            | ExtensionNull -> YAMLElement.Value(YAMLContent.create("null", style = ScalarStyle.Plain))
            | ExtensionBlob v -> YAMLElement.Value(YAMLContent.create(v, tag = "tag:yaml.org,2002:binary", style = ScalarStyle.Plain))
        entity state.Root
    let encodeGraph state = rootElement state |> write
    let decodeGraph source =
        let rows = System.Collections.Generic.Dictionary<string,EntityRow>()
        let rec entity element =
            match element with
            | YAMLElement.Value _ | YAMLElement.Object [YAMLElement.Value _] -> scalar element
            | _ when (field "$ref" element |> Option.isSome) && (field "type" element |> Option.isNone) ->
                if (fields element).Length <> 1 then invalidOp "Reference objects contain only $ref."
                value "$ref" element
            | _ ->
                let kind = value "type" element
                let suppliedId = id element
                let specs = Model.specifications kind
                let properties = fields element |> List.choose (fun (key,e) ->
                    if key = "id" || key = "type" || not (specs |> List.exists (fun (name,_,_,_) -> name = key)) then None
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
                let extensions = fields element |> List.choose (fun (key,e) ->
                    if key = "id" || key = "type" || specs |> List.exists (fun (name,_,_,_) -> name = key) then None
                    else Some(key, extension e)) |> Map.ofList
                let row = { Id = suppliedId; Kind = kind; SuppliedId = opt "id" element; Properties = properties; Extensions = extensions }
                match rows.TryGetValue suppliedId with
                | true, previous when previous <> row -> invalidOp ("Conflicting entity definitions: " + suppliedId)
                | true, _ -> ()
                | _ -> rows.Add(suppliedId,row)
                suppliedId
        and extension e =
            match e with
            | YAMLElement.Sequence values | YAMLElement.Object [YAMLElement.Sequence values] -> ExtensionCollection(List.map extension values)
            | YAMLElement.Value content | YAMLElement.Object [YAMLElement.Value content] ->
                let quoted = content.Style = Some ScalarStyle.DoubleQuoted || content.Style = Some ScalarStyle.SingleQuoted
                let mutable numeric = 0.0
                if content.Tag = Some "!!binary" || content.Tag = Some "tag:yaml.org,2002:binary" then ExtensionBlob content.Value
                elif content.Tag = Some "!!str" || quoted then ExtensionText content.Value
                elif content.Tag = Some "!!null" || content.Value = "null" || content.Value = "~" then ExtensionNull
                elif content.Tag = Some "!!bool" || content.Value = "true" || content.Value = "false" then ExtensionBool(content.Value = "true")
                elif System.Double.TryParse(content.Value, NumberStyles.Float, CultureInfo.InvariantCulture, &numeric) then ExtensionNumber numeric
                else ExtensionText content.Value
            | _ -> ExtensionObject(entity e)
        let root = YAMLicious.Reader.read source |> entity
        { Root = root; Entities = rows.Values |> Seq.toList }
