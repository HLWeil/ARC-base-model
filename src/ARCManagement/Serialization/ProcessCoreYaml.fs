namespace ARCManagement.Internal

open System
open System.Collections.Generic
open System.Globalization
open YAMLicious.YAMLiciousTypes

/// The external YAML codec. It writes the shape of the ProcessCore YAML codecs (type, id, ProcessCore wire names,
/// {"@id": ...} references, grouped processes) from State rows and reads that shape, plus the lenient variants
/// that ProcessCore documents use, back into rows. Wire names come from the property table in Schema.
module internal ProcessCoreYaml =
    let private indexedKinds = ["Annotation"; "Recipe"; "Agent"]
    let private sectionNames = ["annotations"; "recipes"; "labProtocols"; "agentIndex"; "samples"]

    let private quoted (value: string) = YAMLElement.Value(YAMLContent.create(value, style = ScalarStyle.DoubleQuoted))
    let private plain (value: string) = YAMLElement.Value(YAMLContent.create(value, style = ScalarStyle.Plain))
    let private blob (value: string) = YAMLElement.Value(YAMLContent.create(value, tag = "tag:yaml.org,2002:binary", style = ScalarStyle.Plain))
    let private reference (id: string) = Codec.mapping ["@id", Codec.scalarText id]
    let private primaryKey (property: Schema.Property) =
        match property.Wire with
        | Schema.Wire.Key(key, _) -> key
        | Schema.Wire.OneOrMany(singular, _) -> singular
        | Schema.Wire.FirstAndAll(first, _) -> first

    // ---- encoding ----

    /// Writes the graph below state.Root. idOf maps a row key to the ID written for it. Rows without a written ID
    /// are repeated inline at every occurrence. Indexed mode moves Annotations, Recipes and Agents that have IDs
    /// into top-level sections of the root mapping.
    let encode (indexedMode: bool) (state: State) (idOf: string -> string option) : string =
        let rows = Dictionary<string, EntityRow>()
        for row in state.Entities do rows[row.Id] <- row
        // Distinct rows that write one ID are one entity on the wire, so their definitions must agree.
        let firstKey = Dictionary<string, string>()
        let colliding = HashSet<string>()
        for row in state.Entities do
            match idOf row.Id with
            | Some id ->
                match firstKey.TryGetValue id with
                | true, first -> if first <> row.Id then colliding.Add id |> ignore
                | _ -> firstKey[id] <- row.Id
            | None -> ()
        let inProgress = HashSet<string>()
        let definitions = Dictionary<string, string>()
        let indexKey = Dictionary<string, string>()
        let indexOrder = ResizeArray<string>()
        let indexElements = Dictionary<string, YAMLElement>()

        let rec entity (key: string) : YAMLElement =
            let row = rows[key]
            let id = idOf key
            if inProgress.Contains key then
                match id with
                | Some id -> reference id
                | None -> invalidOp "Entities without IDs cannot form a cycle in YAML. Give an entity on the cycle an ID."
            else
                match id with
                | Some id when indexedMode && List.contains row.Kind indexedKinds ->
                    match indexKey.TryGetValue id with
                    | true, first ->
                        if first <> key && colliding.Contains id then define key (Some id) true |> ignore
                        reference id
                    | _ ->
                        indexKey[id] <- key
                        indexOrder.Add id
                        indexElements[id] <- define key (Some id) true
                        reference id
                | _ -> define key id false

        and define (key: string) (id: string option) (atId: bool) : YAMLElement =
            inProgress.Add key |> ignore
            let element = body key id atId
            inProgress.Remove key |> ignore
            match id with
            | Some id when colliding.Contains id ->
                let text = Codec.write element
                match definitions.TryGetValue id with
                | true, previous -> if previous <> text then invalidOp ("Conflicting definitions for entity ID " + id)
                | _ -> definitions[id] <- text
            | _ -> ()
            element

        and headPairs (kind: string) (id: string option) (atId: bool) =
            ["type", Codec.scalarText kind]
            @ (match id with Some value -> ["id", Codec.scalarText value] | None -> [])
            @ (match id with Some value when atId -> ["@id", Codec.scalarText value] | _ -> [])

        and compose (pairs: (string * YAMLElement) list) (extensions: YAMLElement list) (tail: (string * YAMLElement) list) : YAMLElement =
            let mappings = (pairs @ tail) |> List.map (fun (key, value) -> YAMLElement.Mapping(YAMLContent.create(key), value))
            YAMLElement.Object(mappings @ extensions)

        and extensionMappings (row: EntityRow) : YAMLElement list =
            let reserved = Schema.reservedWireKeys row.Kind @ (if row.Id = state.Root then sectionNames else [])
            for key in Map.toList row.Extensions |> List.map fst do
                if List.contains key reserved then
                    invalidOp ("Extension property \"" + key + "\" on " + row.Kind + " collides with a YAML key of the same entity. Rename the extension property.")
            Codec.extensionMappings (row.Extensions |> Map.toList |> List.map (fun (key, cell) -> key, extensionElement cell))

        and extensionElement (cell: ExtensionCell) : YAMLElement =
            match cell with
            | ExtensionText v -> quoted v
            | ExtensionNumber v -> Codec.number v
            | ExtensionBool v -> plain (if v then "true" else "false")
            | ExtensionNull -> plain "null"
            | ExtensionBlob v -> blob v
            | ExtensionObject target -> entity target
            | ExtensionCollection values -> Codec.sequence (List.map extensionElement values)

        and propertyPairs (row: EntityRow) (property: Schema.Property) (cell: Cell) : (string * YAMLElement) list =
            let collection (items: YAMLElement list) =
                match property.Wire, items with
                | Schema.Wire.OneOrMany(singular, _), [one] -> [singular, one]
                | Schema.Wire.OneOrMany(_, plural), _ -> [plural, Codec.sequence items]
                | _ -> [primaryKey property, Codec.sequence items]
            match cell with
            | Text v ->
                let element = if property.Shape = Schema.Shape.TextOrNumber || property.Name = "intendedUse" then quoted v else Codec.scalarText v
                [primaryKey property, element]
            | Number v -> [primaryKey property, Codec.number v]
            | Texts [] | Links [] -> []
            | Texts values ->
                match property.Wire with
                | Schema.Wire.FirstAndAll(first, all) ->
                    (first, Codec.scalarText (List.head values)) :: (if List.length values > 1 then [all, Codec.sequence (List.map Codec.scalarText values)] else [])
                | _ -> collection (List.map Codec.scalarText values)
            | Links ids ->
                match property.Shape with
                | Schema.Shape.Links _ ->
                    if row.Kind = "Dataset" && property.Name = "processes" then
                        collection (groupProcesses ids |> List.map (fun group -> match group with [one] -> entity one | _ -> processBody group))
                    else collection (List.map entity ids)
                | _ -> [primaryKey property, entity (List.exactlyOne ids)]

        and orderedProperties (row: EntityRow) =
            // additionalTypes sits right after the identity fields, as in the ProcessCore encoders.
            let properties = Schema.properties row.Kind
            (properties |> List.filter (fun p -> p.Name = "additionalTypes")) @ (properties |> List.filter (fun p -> p.Name <> "additionalTypes"))

        and processFields (row: EntityRow) (endpoints: (string * YAMLElement) list) : (string * YAMLElement) list =
            let get name =
                match Map.tryFind name row.Properties, Schema.tryProperty "Process" name with
                | Some cell, Some property -> propertyPairs row property cell
                | _ -> []
            get "additionalTypes" @ get "name" @ endpoints @ get "executesRecipe" @ get "parameterValues"

        /// Writes the endpoint arrays of a group. While one member's items are written, only that member (and the
        /// ancestors that were already in progress) counts as being written, so a sibling can appear inline.
        and endpointPairs (keys: string list) (ancestors: string list) =
            let column name (key: string) =
                let items =
                    keys |> List.choose (fun memberKey ->
                        match Map.tryFind name rows[memberKey].Properties with
                        | Some(Links [target]) ->
                            let siblings = keys |> List.filter (fun other -> other <> memberKey && not (List.contains other ancestors))
                            for other in siblings do inProgress.Remove other |> ignore
                            let element = entity target
                            for other in siblings do inProgress.Add other |> ignore
                            Some element
                        | _ -> None)
                if List.isEmpty items then [] else [key, Codec.sequence items]
            column "input" "inputs" @ column "output" "outputs"

        /// One YAML entry for one process, or for a group of processes that share their non-endpoint state.
        and processBody (keys: string list) : YAMLElement =
            let members = keys |> List.map (fun key -> rows[key])
            let first = List.head members
            let ancestors = keys |> List.filter inProgress.Contains
            for key in keys do inProgress.Add key |> ignore
            let ids = keys |> List.map idOf
            let idPairs =
                if List.forall Option.isSome ids then
                    match List.choose id ids with
                    | [one] -> ["id", Codec.scalarText one]
                    | many -> ["id", Codec.sequence (List.map Codec.scalarText many)]
                else []
            let endpoints = endpointPairs keys ancestors
            let pairs = ["type", Codec.scalarText "Process"] @ idPairs @ processFields first endpoints
            let element = compose pairs (extensionMappings first) []
            for key in keys do
                if not (List.contains key ancestors) then inProgress.Remove key |> ignore
            element

        /// Consecutive processes with endpoints, one endpoint shape and equal remaining row data share one entry.
        /// Rows reference other entities by key, so the comparison needs no encoding.
        and groupProcesses (keys: string list) : string list list =
            let signature (key: string) =
                let row = rows[key]
                let hasInput, hasOutput = Map.containsKey "input" row.Properties, Map.containsKey "output" row.Properties
                let id = idOf key
                let ungrouped = (not hasInput && not hasOutput) || inProgress.Contains key || (match id with Some value -> colliding.Contains value | None -> false)
                let shared = row.Properties |> Map.remove "input" |> Map.remove "output"
                if ungrouped then None else Some(hasInput, hasOutput, Option.isSome id, shared, row.Extensions)
            let result = ResizeArray<ResizeArray<string>>()
            let mutable previous = None
            for key in keys do
                let current = signature key
                if current.IsSome && current = previous then result[result.Count - 1].Add key
                else result.Add(ResizeArray<string>([key]))
                previous <- current
            result |> Seq.map List.ofSeq |> List.ofSeq

        and body (key: string) (id: string option) (atId: bool) : YAMLElement =
            let row = rows[key]
            if row.Kind = "Process" then
                // Single processes carry their ID in processBody. An index never holds processes.
                processBody [key]
            else
                let fields = orderedProperties row |> List.collect (fun property ->
                    match Map.tryFind property.Name row.Properties with
                    | Some cell -> propertyPairs row property cell
                    | None -> [])
                let extensions = extensionMappings row
                let tail =
                    if indexedMode && key = state.Root then
                        let section name kind =
                            let ids = indexOrder |> Seq.filter (fun id -> rows[indexKey[id]].Kind = kind) |> List.ofSeq
                            if List.isEmpty ids then [] else [name, Codec.sequence (ids |> List.map (fun id -> indexElements[id]))]
                        section "annotations" "Annotation" @ section "recipes" "Recipe" @ section "agentIndex" "Agent"
                    else []
                compose (headPairs row.Kind id atId @ fields) extensions tail

        Codec.write (entity state.Root)

    /// Managed export: every row writes its managed ID and objects repeat inline.
    let encodeState (state: State) = encode false state Some

    // ---- decoding ----

    type private Node =
        | Scalar of YAMLContent
        | Items of YAMLElement list
        | Entries of (string * YAMLElement) list

    /// Comments are YAML elements of their own. The reader ignores them, as ProcessCore does.
    let private withoutComments (items: YAMLElement list) =
        let isComment (item: YAMLElement) = match item with YAMLElement.Comment _ -> true | _ -> false
        // A comment between sequence items arrives as an item that holds nothing else.
        items |> List.filter (fun item ->
            match item with
            | YAMLElement.Object parts -> List.isEmpty parts || not (List.forall isComment parts)
            | _ -> not (isComment item))

    let private classify (element: YAMLElement) : Node =
        match element with
        | YAMLElement.Value v -> Node.Scalar v
        | YAMLElement.Sequence items -> Node.Items(withoutComments items)
        | YAMLElement.Object parts ->
            match withoutComments parts with
            | [YAMLElement.Value v] -> Node.Scalar v
            | [YAMLElement.Sequence items] -> Node.Items(withoutComments items)
            | visible -> Node.Entries(Codec.fields (YAMLElement.Object visible))
        | YAMLElement.Alias _ -> invalidOp "YAML anchors and aliases are not supported."
        | _ -> invalidOp "Unsupported YAML structure."

    let private textOf (what: string) (element: YAMLElement) : string =
        match classify element with
        | Node.Scalar v -> v.Value
        | _ -> invalidOp ("Expected a text value for " + what + ".")

    let private textItems (what: string) (element: YAMLElement) : string list =
        match classify element with
        | Node.Items items -> items |> List.map (textOf what)
        | Node.Scalar v -> [v.Value]
        | _ -> invalidOp ("Expected text for " + what + ".")

    let private find (name: string) (entries: (string * YAMLElement) list) =
        entries |> List.tryPick (fun (key, value) -> if key = name then Some value else None)

    let private digitsFrom (text: string) (start: int) =
        let mutable index = start
        while index < text.Length && text.[index] >= '0' && text.[index] <= '9' do index <- index + 1
        index

    /// YAML core-schema integers and floats. Other plain scalars stay text.
    let private isNumber (text: string) =
        let start = if text.Length > 0 && (text.[0] = '-' || text.[0] = '+') then 1 else 0
        let integerEnd = digitsFrom text start
        let hasInteger = integerEnd > start
        let fractionEnd = if integerEnd < text.Length && text.[integerEnd] = '.' then digitsFrom text (integerEnd + 1) else integerEnd
        let hasFraction = fractionEnd > integerEnd + 1
        if not (hasInteger || hasFraction) then false
        elif fractionEnd = text.Length then true
        elif text.[fractionEnd] = 'e' || text.[fractionEnd] = 'E' then
            let signed = fractionEnd + 1 < text.Length && (text.[fractionEnd + 1] = '-' || text.[fractionEnd + 1] = '+')
            let exponentStart = if signed then fractionEnd + 2 else fractionEnd + 1
            let exponentEnd = digitsFrom text exponentStart
            exponentEnd > exponentStart && exponentEnd = text.Length
        else false

    let private tryNumber (text: string) : float option =
        if isNumber text then
            let value = Double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)
            if Double.IsNaN value || Double.IsInfinity value then None else Some value
        else None

    let private isQuoted (content: YAMLContent) =
        content.Style = Some ScalarStyle.DoubleQuoted || content.Style = Some ScalarStyle.SingleQuoted
        || content.Tag = Some "!!str" || content.Tag = Some "tag:yaml.org,2002:str"

    let private scalarExtension (content: YAMLContent) : ExtensionCell =
        if content.Tag = Some "!!binary" || content.Tag = Some "tag:yaml.org,2002:binary" then ExtensionBlob content.Value
        elif isQuoted content then ExtensionText content.Value
        elif content.Value = "null" || content.Value = "~" then ExtensionNull
        elif content.Value = "true" then ExtensionBool true
        elif content.Value = "false" then ExtensionBool false
        else
            match tryNumber content.Value with
            | Some number -> ExtensionNumber number
            | None -> ExtensionText content.Value

    let private isIdKey (key: string) = key.Length > 1 && key.[0] = 'i' && key.[1] = ':'

    /// A mapping that holds only an ID points at an entity defined elsewhere in the document.
    let private referenceOf (entries: (string * YAMLElement) list) : string option =
        match entries with
        | [key, value] when key = "@id" || key = "id" ->
            match classify value with
            | Node.Scalar v -> Some v.Value
            | _ -> None
        | _ -> None

    let private suppliedId (entries: (string * YAMLElement) list) : string option =
        match find "id" entries |> Option.map (textOf "id"), find "@id" entries |> Option.map (textOf "@id") with
        | Some a, Some b -> if a <> b then invalidOp "The keys id and @id of one entity must carry the same value." else Some a
        | Some a, None -> Some a
        | None, other -> other

    /// An alias names a core kind only where the position accepts that kind. Elsewhere, such as in extensions,
    /// a type like File stays the type of a generic object.
    let private resolveType (targets: string list) (name: string) =
        let canonical = Schema.canonicalKind name
        if canonical <> name && not (List.contains canonical targets) then name else canonical

    let private typeName (targets: string list) (entries: (string * YAMLElement) list) : string option =
        match find "type" entries |> Option.map (textOf "type" >> resolveType targets), find "@type" entries |> Option.map (textOf "@type" >> resolveType targets) with
        | Some a, Some b -> if a <> b then invalidOp "The keys type and @type of one entity must carry the same value." else Some a
        | Some a, None -> Some a
        | None, other -> other

    let private inferKind (targets: string list) (entries: (string * YAMLElement) list) : string =
        match targets with
        | [one] -> one
        | [] -> "Object"
        | first :: _ -> if List.contains "Data" targets && (find "path" entries).IsSome then "Data" else first

    let private processIds (entries: (string * YAMLElement) list) (count: int) : string option list =
        let read name =
            find name entries |> Option.map (fun value ->
                match classify value with
                | Node.Items items -> items |> List.map (textOf name), true
                | _ -> [textOf name value], false)
        let chosen =
            match read "id", read "@id" with
            | Some a, Some b -> if fst a <> fst b then invalidOp "The keys id and @id of one entity must carry the same value." else Some a
            | Some a, None -> Some a
            | None, other -> other
        match chosen with
        | None -> List.replicate count None
        | Some(items, isSequence) ->
            if not isSequence then
                if count > 1 then invalidOp "A scalar id cannot name a process entry that expands to several processes. Use a sequence of IDs."
                else [Some(List.head items)]
            elif List.length items <> count then
                invalidOp ("A process entry that expands to " + string count + " processes needs " + string count + " IDs, found " + string (List.length items) + ".")
            else List.map Some items

    /// Reads a document into rows keyed by internal keys. IDs written on entities decide the keys, so equal IDs
    /// share one row. The second result maps those keys to the supplied IDs.
    let decode (source: string) : State * Map<string, string> =
        let rows = Dictionary<string, EntityRow>()
        let order = ResizeArray<string>()
        let supplied = Dictionary<string, string>()
        let anonymous = ref 0

        let rec fingerprint (row: EntityRow) : string =
            let target (key: string) = if isIdKey key then key else "(" + fingerprint rows[key] + ")"
            let sized (value: string) = string value.Length + ":" + value
            let cell (value: Cell) =
                match value with
                | Text v -> "t" + sized v
                | Number v -> "n" + string v
                | Texts vs -> "[" + String.concat "," (List.map sized vs) + "]"
                | Links ks -> "<" + String.concat "," (List.map target ks) + ">"
            let rec extension (value: ExtensionCell) =
                match value with
                | ExtensionText v -> "t" + sized v
                | ExtensionNumber v -> "n" + string v
                | ExtensionBool v -> if v then "true" else "false"
                | ExtensionNull -> "null"
                | ExtensionBlob v -> "b" + sized v
                | ExtensionObject k -> "o" + target k
                | ExtensionCollection vs -> "[" + String.concat "," (List.map extension vs) + "]"
            let properties = row.Properties |> Map.toList |> List.map (fun (key, value) -> sized key + cell value)
            let extensions = row.Extensions |> Map.toList |> List.map (fun (key, value) -> sized key + extension value)
            row.Kind + "{" + String.concat ";" properties + "|" + String.concat ";" extensions + "}"

        let define (kind: string) (id: string option) (properties: Map<string, Cell>) (extensions: Map<string, ExtensionCell>) : string =
            let key =
                match id with
                | Some value -> "i:" + value
                | None ->
                    anonymous.Value <- anonymous.Value + 1
                    "n" + string anonymous.Value
            let row = { Id = key; Kind = kind; Properties = properties; Extensions = extensions }
            match id with
            | Some value ->
                match rows.TryGetValue key with
                | true, existing ->
                    if existing.Kind <> kind || fingerprint existing <> fingerprint row then
                        invalidOp ("Conflicting definitions for entity ID " + value)
                | _ ->
                    rows[key] <- row
                    order.Add key
                    supplied[key] <- value
            | None ->
                rows[key] <- row
                order.Add key
            key

        let rec decodeItem (targets: string list) (element: YAMLElement) : string list =
            match classify element with
            | Node.Scalar v -> ["i:" + v.Value]
            | Node.Items _ -> invalidOp "Expected one entity, found a sequence."
            | Node.Entries entries ->
                match referenceOf entries with
                | Some id -> ["i:" + id]
                | None -> decodeEntries targets false entries

        and single (targets: string list) (element: YAMLElement) : string =
            match decodeItem targets element with
            | [one] -> one
            | _ -> invalidOp "A process entry with several inputs or outputs is only valid in a Dataset processes list."

        and many (targets: string list) (element: YAMLElement) : string list =
            match classify element with
            | Node.Items items -> items |> List.collect (decodeItem targets)
            | _ -> decodeItem targets element

        and decodeEntries (targets: string list) (isRoot: bool) (entries: (string * YAMLElement) list) : string list =
            let kind = match typeName targets entries with Some value -> value | None -> inferKind targets entries
            if kind = "Process" then decodeProcess entries
            else
                let properties, extensions = build kind isRoot entries []
                [define kind (suppliedId entries) properties extensions]

        and decodeProcess (entries: (string * YAMLElement) list) : string list =
            let column name =
                match find name entries with
                | None -> [||]
                | Some value ->
                    match classify value with
                    | Node.Items items -> List.toArray items
                    | _ -> [| value |]
            let inputs, outputs = column "inputs", column "outputs"
            let count = max 1 (max inputs.Length outputs.Length)
            let ids = processIds entries count |> List.toArray
            // One entry is one protocol applied to several endpoint pairs, so the expanded processes share the
            // rows decoded from the common fields, including entities that have no ID.
            let shared, extensions = build "Process" false entries []
            let keys = ResizeArray<string>()
            for index in 0 .. count - 1 do
                let endpoint (name: string) (items: YAMLElement[]) =
                    if index < items.Length then [name, Links [single ["Sample"; "Data"] items[index]]] else []
                let properties = (endpoint "input" inputs @ endpoint "output" outputs) |> List.fold (fun map (name, cell) -> Map.add name cell map) shared
                keys.Add(define "Process" ids[index] properties extensions)
            List.ofSeq keys

        and decodeCell (kind: string) (property: Schema.Property) (value: YAMLElement) : Cell =
            match property.Shape with
            | Schema.Shape.Text -> Text(textOf property.Name value)
            | Schema.Shape.Texts -> Texts(textItems property.Name value)
            | Schema.Shape.Link targets -> Links [single targets value]
            | Schema.Shape.Links targets ->
                // Data files may sit in a hasPart list. They move to dataFiles once every definition is known.
                let targets = if kind = "Dataset" && property.Name = "hasParts" then ["Dataset"; "Data"] else targets
                Links(many targets value)
            | Schema.Shape.TextOrNumber ->
                match classify value with
                | Node.Scalar content ->
                    match (if isQuoted content then None else tryNumber content.Value) with
                    | Some number -> Number number
                    | None -> Text content.Value
                | _ -> invalidOp "Expected an Annotation value."
            | Schema.Shape.TextOrLink target ->
                match classify value with
                | Node.Scalar content -> Text content.Value
                | _ -> Links [single [target] value]
            | Schema.Shape.SampleOrData -> Links [single ["Sample"; "Data"] value]

        and decodeExtension (element: YAMLElement) : ExtensionCell =
            match classify element with
            | Node.Scalar content -> scalarExtension content
            | Node.Items items -> ExtensionCollection(List.map decodeExtension items)
            | Node.Entries _ -> ExtensionObject(single [] element)

        and build (kind: string) (isRoot: bool) (entries: (string * YAMLElement) list) (endpoints: (string * Cell) list) : Map<string, Cell> * Map<string, ExtensionCell> =
            let consumed = HashSet<string>(["id"; "@id"; "type"; "@type"])
            if isRoot then for name in sectionNames do consumed.Add name |> ignore
            if kind = "Process" then
                consumed.Add "inputs" |> ignore
                consumed.Add "outputs" |> ignore
            let properties = ResizeArray<string * Cell>()
            for property in Schema.properties kind do
                let isEndpoint = (match property.Storage with Schema.Storage.Endpoint _ -> true | _ -> false)
                if not isEndpoint then
                    let present = entries |> List.filter (fun (key, _) -> List.contains key (Schema.wireNames property))
                    for key, _ in present do consumed.Add key |> ignore
                    let cell =
                        match property.Wire, present with
                        | _, [] -> None
                        | Schema.Wire.FirstAndAll(first, all), _ ->
                            let allItems = find all present |> Option.map (textItems property.Name)
                            let firstItems = find first present |> Option.map (textItems property.Name)
                            match allItems, firstItems with
                            | Some items, Some leading ->
                                if List.tryHead items <> List.tryHead leading then invalidOp "The key identifier must repeat the first item of identifiers." else Some(Texts items)
                            | Some items, None -> Some(Texts items)
                            | None, other -> other |> Option.map Texts
                        | _, [_, value] -> Some(decodeCell kind property value)
                        | _ -> invalidOp ("Use only one of " + String.concat ", " (present |> List.map fst) + " on " + kind + ".")
                    match cell with
                    | Some value -> properties.Add((property.Name, value))
                    | None -> ()
            for name, cell in endpoints do properties.Add((name, cell))
            let has name = Seq.exists (fun (key, _) -> key = name) properties
            if kind = "Dataset" && not (has "conformsTo") then
                let any names = names |> List.exists (fun name -> (find name entries).IsSome)
                let profiles =
                    [ if any ["processes"] then "process-provenance"
                      if any ["descriptors"] then "semantic-designation"
                      if any ["title"; "description"; "license"; "datePublished"; "dateCreated"; "dateModified"; "dataFiles"; "agents"; "citations"] then "administrative" ]
                // Data entries in hasPart count as data files, which is only known once references resolve.
                properties.Add(("~inferred", Texts profiles))
            if kind = "Agent" && not (has "name") then
                let part name = properties |> Seq.tryPick (fun (key, cell) -> match cell with Text v when key = name -> Some v | _ -> None)
                properties.Add(("name", Text((String.concat " " (List.choose id [part "givenName"; part "familyName"])).Trim())))
            let extensions =
                entries
                |> List.filter (fun (key, _) -> not (consumed.Contains key))
                |> List.map (fun (key, value) -> key, decodeExtension value)
            Map.ofSeq properties, Map.ofList extensions

        let section (targets: string list) (element: YAMLElement) =
            let items = match classify element with Node.Items values -> values | _ -> [element]
            for item in items do
                let entries = match classify item with Node.Entries values -> values | _ -> []
                if List.isEmpty entries || (referenceOf entries).IsSome || (suppliedId entries).IsNone then
                    invalidOp "Entries of an index section need a full definition with an id."
                decodeEntries targets false entries |> ignore

        let document = YAMLicious.Reader.read source
        let rootEntries = match classify document with Node.Entries entries -> entries | _ -> invalidOp "Expected a YAML mapping for the root Dataset."
        let sections = [ "annotations", ["Annotation"]; "recipes", ["Recipe"]; "labProtocols", ["Recipe"]; "agentIndex", ["Agent"]; "samples", ["Sample"; "Data"] ]
        if (find "recipes" rootEntries).IsSome && (find "labProtocols" rootEntries).IsSome then invalidOp "Use only one of recipes, labProtocols."
        for name, targets in sections do
            match find name rootEntries with
            | Some element -> section targets element
            | None -> ()
        let rootKey = match decodeEntries ["Dataset"] true rootEntries with [one] -> one | _ -> invalidOp "The root entry must be a single Dataset."

        // Keep what the root reaches. Definitions that nothing references are dropped.
        let reachable = HashSet<string>()
        let pending = ResizeArray<string>([rootKey])
        while pending.Count > 0 do
            let key = pending[pending.Count - 1]
            pending.RemoveAt(pending.Count - 1)
            if reachable.Add key then
                let found, row = rows.TryGetValue key
                if not found then invalidOp ("Unresolved reference: " + key.Substring 2)
                for KeyValue(_, cell) in row.Properties do
                    match cell with
                    | Links targets -> pending.AddRange targets
                    | _ -> ()
                for KeyValue(_, cell) in row.Extensions do
                    pending.AddRange(Model.extensionReferences cell)
        let kept = order |> Seq.filter reachable.Contains |> List.ofSeq
        // Profiles follow from the fields of the Dataset, with Data from hasPart counted as data files.
        let finishProfiles (key: string) =
            let row = rows[key]
            match Map.tryFind "~inferred" row.Properties with
            | Some(Texts inferred) ->
                let hasFiles = (match Map.tryFind "dataFiles" row.Properties with Some(Links files) -> not (List.isEmpty files) | _ -> false)
                let withFiles = if hasFiles && not (List.contains "administrative" inferred) then inferred @ ["administrative"] else inferred
                let profiles = if List.isEmpty withFiles then ["administrative"] else withFiles
                rows[key] <- { row with Properties = row.Properties |> Map.remove "~inferred" |> Map.add "conformsTo" (Texts profiles) }
            | _ -> ()
        for key in kept do
            let row = rows[key]
            if row.Kind = "Dataset" then
                match Map.tryFind "hasParts" row.Properties with
                | Some(Links targets) ->
                    let isData target = rows[target].Kind = "Data"
                    if List.exists isData targets then
                        let files = match Map.tryFind "dataFiles" row.Properties with Some(Links existing) -> existing | _ -> []
                        let properties =
                            row.Properties
                            |> Map.add "hasParts" (Links(targets |> List.filter (isData >> not)))
                            |> Map.add "dataFiles" (Links(files @ List.filter isData targets))
                        rows[key] <- { row with Properties = properties }
                | _ -> ()
                finishProfiles key
        let ids = kept |> List.choose (fun key -> match supplied.TryGetValue key with | true, id -> Some(key, id) | _ -> None) |> Map.ofList
        { Root = rootKey; Entities = kept |> List.map (fun key -> rows[key]) }, ids
