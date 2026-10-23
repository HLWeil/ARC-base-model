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
    let private hex4 (code: int) =
        let digits = "0123456789abcdef"
        string digits.[(code >>> 12) &&& 15] + string digits.[(code >>> 8) &&& 15] + string digits.[(code >>> 4) &&& 15] + string digits.[code &&& 15]
    /// Characters that need an escape in a double-quoted scalar: controls, line and paragraph separators, byte order mark.
    let private needsEscape (c: char) =
        let code = int c
        code < 0x20 || (code >= 0x7F && code <= 0x9F) || code = 0x2028 || code = 0x2029 || code = 0xFEFF
    /// The text between the quotes of a double-quoted scalar. YAMLicious escapes only some control characters, so the
    /// escapes are written here and the result goes out verbatim.
    let private escapeQuoted (value: string) =
        let builder = System.Text.StringBuilder()
        for c in value do
            if c = '\\' then builder.Append("\\\\") |> ignore
            elif c = '"' then builder.Append("\\\"") |> ignore
            elif c = '\n' then builder.Append("\\n") |> ignore
            elif c = '\r' then builder.Append("\\r") |> ignore
            elif c = '\t' then builder.Append("\\t") |> ignore
            elif needsEscape c then builder.Append("\\u").Append(hex4 (int c)) |> ignore
            else builder.Append(c) |> ignore
        builder.ToString()
    let private isSpace (c: char) =
        let code = int c
        c = ' ' || (code >= 0x09 && code <= 0x0D) || code = 0x85 || code = 0xA0 || code = 0x1680 || (code >= 0x2000 && code <= 0x200A)
        || code = 0x2028 || code = 0x2029 || code = 0x202F || code = 0x205F || code = 0x3000
    /// True when a plain scalar with this text would be misread or altered by a YAML reader. The checks use plain
    /// character tests, so every runtime decides alike. Line feeds alone do not count, because multi-line text
    /// goes out as a block scalar.
    let needsQuotes (value: string) =
        if implicitScalar value then true
        elif "-?:,[]{}#&*!|>'\"%@`".IndexOf(value.[0]) >= 0 then true
        elif isSpace value.[0] || isSpace value.[value.Length - 1] then true
        elif value.StartsWith("---") || value.StartsWith("...") then true
        elif value.Contains(": ") || value.Contains(" #") then true
        else value |> Seq.exists (fun c -> c <> '\n' && (c = '\t' || c = '\r' || needsEscape c))
    /// Text that stays a string for every YAML reader: quoted when a plain scalar would change meaning or text.
    let scalarText (value: string) =
        if needsQuotes value then YAMLElement.Value(YAMLContent.create("\"" + escapeQuoted value + "\"", style = ScalarStyle.Plain))
        else text value
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
    /// Rewrites the shortest round-trip text of a finite number into one form for every runtime. The runtimes agree
    /// on the digits and differ on the layout (3 or 3.0, E+21 or e+21, 1E-07 or 1e-7). Plain notation covers decimal
    /// exponents from -7 up to 20. Outside that range the text is d.ddde+N or d.ddde-N. Negative zero becomes 0.
    /// Text that is not a plain decimal number (NaN, Infinity) comes back unchanged.
    let canonicalNumber (text: string) : string =
        let negative = text.StartsWith("-")
        let body = if negative || text.StartsWith("+") then text.Substring(1) else text
        let lower, upper = body.IndexOf('e'), body.IndexOf('E')
        let marker = if lower >= 0 then lower else upper
        let mantissa = if marker >= 0 then body.Substring(0, marker) else body
        let exponentText = if marker >= 0 then body.Substring(marker + 1) else "0"
        let dot = mantissa.IndexOf('.')
        let whole = if dot >= 0 then mantissa.Substring(0, dot) else mantissa
        let fraction = if dot >= 0 then mantissa.Substring(dot + 1) else ""
        let isDigits (value: string) = value |> Seq.forall (fun c -> c >= '0' && c <= '9')
        let exponentDigits = if exponentText.StartsWith("-") || exponentText.StartsWith("+") then exponentText.Substring(1) else exponentText
        if whole.Length + fraction.Length = 0 || not (isDigits whole) || not (isDigits fraction) || exponentDigits.Length = 0 || not (isDigits exponentDigits) then text
        else
            let all = whole + fraction
            let significant = all.TrimStart('0')
            let digits = significant.TrimEnd('0')
            if digits.Length = 0 then "0"
            else
                // The value is 0.<all> * 10^(whole.Length + exponent), so the leading digit has this decimal exponent.
                let leading = whole.Length + int exponentText - (all.Length - significant.Length) - 1
                let sign = if negative then "-" else ""
                if leading >= -7 && leading < 21 then
                    if leading >= 0 then
                        let integer = if digits.Length <= leading + 1 then digits + String.replicate (leading + 1 - digits.Length) "0" else digits.Substring(0, leading + 1)
                        let rest = if digits.Length > leading + 1 then "." + digits.Substring(leading + 1) else ""
                        sign + integer + rest
                    else sign + "0." + String.replicate (-leading - 1) "0" + digits
                else
                    let tail = if digits.Length > 1 then "." + digits.Substring(1) else ""
                    sign + digits.Substring(0, 1) + tail + "e" + (if leading < 0 then "-" else "+") + string (abs leading)
    /// The canonical text that the codecs write for a number.
    let numberText (v: float) : string = canonicalNumber (numericText v)
    let number (v: float) = YAMLElement.Value(YAMLContent.create(numberText v, style = ScalarStyle.Plain))
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
