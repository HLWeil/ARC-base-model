namespace ARCManagement.Internal

open ARCManagement.Helper

open YAMLicious.YAMLiciousTypes
open System.Globalization
open Fable.Core

module internal Codec =
    let text value = YAMLElement.Value(YAMLContent.create(value))
    let mapping pairs =
        pairs |> List.map (fun (key, value) -> YAMLElement.Mapping(YAMLContent.create(key), value)) |> YAMLElement.Object
    /// True when a plain scalar with this text could be typed implicitly by a YAML 1.1 or 1.2 reader. The check uses
    /// plain character tests, so every runtime writes the same text. It over-quotes anything that starts like a
    /// number or a date.
    let implicitScalar (value: string) =
        if value.Length = 0 then true
        elif List.contains (value.ToLowerInvariant()) ["null"; "~"; "true"; "false"; "yes"; "no"; "on"; "off"; "y"; "n"; ".inf"; "-.inf"; "+.inf"; ".nan"] then true
        else
            let start = if value.[0] = '+' || value.[0] = '-' then 1 else 0
            let isDigit (index: int) = index < value.Length && value.[index] >= '0' && value.[index] <= '9'
            isDigit start || (start < value.Length && value.[start] = '.' && isDigit (start + 1))
    /// Text that stays a string for every YAML reader: quoted when a plain scalar would be typed implicitly.
    let scalarText (value: string) =
        if implicitScalar value then YAMLElement.Value(YAMLContent.create(value, style = ScalarStyle.DoubleQuoted)) else text value
    let extensionMappings pairs =
        let keyContent (key: string) =
            // '$' introduces YAMLicious array syntax; '['/'{' also interfere with its key parser.
            if YAMLicious.Writer.StyleVerifier.isPlainSafe key && not (key.StartsWith("$") || key.Contains("[") || key.Contains("{")) && not (implicitScalar key) then
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
                  "properties", row.Properties |> Map.toList |> List.map (fun (key,cell) -> key, cellElement cell) |> mapping])
    let readRow element =
        let properties = required "properties" element |> fields |> List.map (fun (key,e) ->
            key, match value "storage" e with
                 | "text" -> Text(value "value" e)
                 | "number" -> Number(System.Double.Parse(value "value" e, CultureInfo.InvariantCulture))
                 | "texts" -> Texts(stringItems "values" e)
                 | "links" -> Links(stringItems "values" e)
                 | _ -> invalidOp "Unknown stored property type.") |> Map.ofList
        // Rows written before the supplied-ID field was removed carry a "suppliedId" key, which is ignored here.
        { Id = value "id" element; Kind = value "type" element; Properties = properties; Extensions = field "extensions" element |> Option.map (fields >> List.map (fun (key,e) -> key, readExtension e) >> Map.ofList) |> Option.defaultValue Map.empty }
    let encodeState state = mapping ["root", text state.Root; "entities", sequence (List.map rowElement state.Entities)] |> write
    let decodeState source =
        let element = YAMLicious.Reader.read source
        { Root = value "root" element; Entities = items "entities" element |> List.map readRow }
    let encodeChanges (changes: Change list) =
        if List.isEmpty changes then ""
        else
            let record (change: Change) =
                mapping (["id", text change.EntityId; "ordinal", text (change.Ordinal.ToString())]
                         @ (change.Before |> Option.map (fun row -> "before", rowElement row) |> Option.toList)
                         @ (change.After |> Option.map (fun row -> "after", rowElement row) |> Option.toList))
            mapping ["changes", sequence (List.map record changes)] |> write
    let decodeChanges source : Change list =
        if source = "" then []
        else
            items "changes" (YAMLicious.Reader.read source) |> List.map (fun element ->
                { EntityId = value "id" element
                  Ordinal = System.Int64.Parse(value "ordinal" element)
                  Before = field "before" element |> Option.map readRow
                  After = field "after" element |> Option.map readRow })
