namespace ARCManagement.Internal

open System.Collections.Generic
open ARCManagement.Helper

open ARCManagement.Internal

open PolyglotSQLite
open Fable.Core

[<AttachMembers>]
type internal Database(connection: SqliteConnection, path: string, close: unit -> unit) =
    member _.Connection = connection
    member _.Path = path
    member _.Close() = close()

/// Comparable column values for the diff writer. SqlValue has no structural equality.
[<RequireQualifiedAccess>]
type internal Sql =
    | Null
    | Text of string
    | Real of float

/// One row of entity_extension, flattened from an extension tree.
type internal ExtRow = { XPath: string; XParent: string option; XPosition: int option; XStorage: string; XText: string option; XNumber: float option; XBool: bool option; XTarget: string option }

/// The relational rows that represent one entity: scalar columns, ordered lists, process endpoints and extensions.
type internal Projection =
    { PColumns: (string * Sql) list
      PLists: (string * string * string * string list) list
      PEndpoints: (string * string option * string option) list
      PExtensions: (string * ExtRow list) list }

module internal Store =
    let openDatabase path =
        let connection = if path = ":memory:" then Sqlite.OpenInMemory() else Sqlite.OpenFile(path)
        Database(connection,path,connection.Close)
    let text value = SqlValue.Text(value)
    let optional value = value |> Option.map text |> Option.defaultWith SqlValue.Null
    let parameters values = values |> List.map (fun (name, value) -> SqlParameter(name, value))
    let execute (db: SqliteConnection) sql values = db.Execute(sql, parameters values)
    let optText (row: SqlRow) name = let value = row.GetByName(name) in if value.IsNull then None else Some(value.AsText())
    /// Validate a completed core-profile database separately from session metadata.
    let validateCore (db: SqliteConnection) =
        let errors = db.Query("SELECT dataset_id,message FROM core_validation_errors ORDER BY dataset_id,message")
        if errors.Count > 0 then
            errors
            |> Seq.map (fun row -> row.GetByName("dataset_id").AsText() + ": " + row.GetByName("message").AsText())
            |> String.concat "; "
            |> invalidOp
        if db.Query("PRAGMA foreign_key_check").Count > 0 then
            invalidOp "Core database contains invalid foreign keys."

    let private sqlOf value =
        match value with
        | Sql.Null -> SqlValue.Null()
        | Sql.Text v -> SqlValue.Text v
        | Sql.Real v -> SqlValue.Real v
    let private sqlOfOption value = match value with Some v -> SqlValue.Text v | None -> SqlValue.Null()
    let private integer (value: int) = SqlValue.Integer(int64 value)
    let private realOf (value: SqlValue) = if value.Kind = "integer" then float (value.AsInteger()) else value.AsReal()

    /// Whitespace runs collapse to one space, so stored statement text compares independent of layout.
    let private collapse (value: string) =
        let builder = System.Text.StringBuilder()
        let mutable pending = false
        for c in value do
            if c = ' ' || c = '\n' || c = '\r' || c = '\t' then pending <- builder.Length > 0
            else
                if pending then builder.Append(' ') |> ignore
                pending <- false
                builder.Append(c) |> ignore
        builder.ToString()

    let private objectName (statement: string) =
        let name = statement.Split(' ').[2]
        let index = name.IndexOf('(')
        if index >= 0 then name.Substring(0, index) else name

    let initialize (db: SqliteConnection) =
        // DDL and metadata are initialized together; never partially initialize a repository.
        db.WithTransaction(fun () ->
            for statement in CoreSchema.ddl do db.Execute(statement) |> ignore
            execute db "INSERT INTO repository(singleton,schema_version) VALUES(1,$version)" ["version", SqlValue.Integer(CoreSchema.version)] |> ignore)

    let validateRepository (db: SqliteConnection) =
        let rows = db.Query("SELECT schema_version FROM repository WHERE singleton=1")
        if rows.Count <> 1 || rows[0].Get(0).AsInteger() <> CoreSchema.version then
            invalidOp "Unsupported ARC repository schema. Expected version 5; older repositories are not migrated. Re-import the ARC from arc.yml."
        if (db.Query("PRAGMA foreign_keys")).[0].Get(0).AsInteger() <> 1L then
            invalidOp "ARC repositories require foreign-key enforcement."
        // Every object of the expected schema must exist with identical text. Snapshots are not read.
        let stored = Dictionary<string, string>()
        for row in db.Query("SELECT name,sql FROM sqlite_master WHERE sql IS NOT NULL") do
            stored[row.GetByName("name").AsText()] <- row.GetByName("sql").AsText()
        for statement in CoreSchema.ddl do
            let name = objectName statement
            // A boolean avoids a match with a unit branch, which Fable Python compiles to an unconditional raise.
            let matches = match stored.TryGetValue name with | true, sql -> collapse sql = collapse statement | _ -> false
            if not matches then invalidOp ("Invalid ARC repository schema: " + name)

    let metadata (db: SqliteConnection) arcId =
        let rows = db.Query("SELECT * FROM arc WHERE arc_id=$arc", parameters ["arc",text arcId])
        if rows.Count <> 1 then invalidArg "arcId" "Unknown ARC entry."
        rows[0]

    // ---- projection of rows onto the core profile ----

    let private referenceColumns (spec: Schema.EntitySpec) =
        spec.Properties |> List.collect (fun property ->
            match property.Storage, property.Shape with
            | Schema.Storage.Column column, Schema.Shape.Link _ -> [column]
            | Schema.Storage.TextOrLinkColumns(_, link), _ -> [link]
            | _ -> [])

    let private flattenExtension (cell: ExtensionCell) =
        let rows = ResizeArray<ExtRow>()
        let rec visit path parent position cell =
            let add storage value number flag target =
                rows.Add({ XPath = path; XParent = parent; XPosition = position; XStorage = storage; XText = value; XNumber = number; XBool = flag; XTarget = target })
            match cell with
            | ExtensionText v -> add "text" (Some v) None None None
            | ExtensionNumber v -> add "number" None (Some v) None None
            | ExtensionBool v -> add "bool" None None (Some v) None
            | ExtensionNull -> add "null" None None None None
            | ExtensionBlob v -> add "blob" (Some v) None None None
            | ExtensionObject id -> add "object" None None None (Some id)
            | ExtensionCollection values ->
                add "collection" None None None None
                values |> List.iteri (fun index value -> visit (path + "/" + string index) (Some path) (Some index) value)
        visit "" None None cell
        List.ofSeq rows

    /// Rows that represent an entity. The kind lookup decides between sample and data columns.
    let private project (kindOf: string -> string option) (row: EntityRow) : Projection =
        let cell key = Map.tryFind key row.Properties
        let single key = match cell key with Some(Links [id]) -> Some id | _ -> None
        let extensions = row.Extensions |> Map.toList |> List.map (fun (key, value) -> key, flattenExtension value)
        match Schema.tryEntity row.Kind with
        | None ->
            { PColumns = []; PLists = ["entity_additional_type", "entity_id", "value", Model.texts "additionalTypes" row]; PEndpoints = []; PExtensions = extensions }
        | Some spec ->
            let columns = ResizeArray<string * Sql>()
            let lists = ResizeArray<string * string * string * string list>()
            let endpoints = ResizeArray<string * string option * string option>()
            for property in spec.Properties do
                let key = property.Name
                match property.Storage with
                | Schema.Storage.Column column ->
                    columns.Add(column, (match cell key with Some(Text v) -> Sql.Text v | Some(Links [id]) -> Sql.Text id | _ -> Sql.Null))
                | Schema.Storage.TextOrNumberColumns(textColumn, numberColumn) ->
                    match cell key with
                    | Some(Text v) -> columns.Add(textColumn, Sql.Text v); columns.Add(numberColumn, Sql.Null)
                    | Some(Number v) -> columns.Add(textColumn, Sql.Null); columns.Add(numberColumn, Sql.Real v)
                    | _ -> columns.Add(textColumn, Sql.Null); columns.Add(numberColumn, Sql.Null)
                | Schema.Storage.TextOrLinkColumns(textColumn, linkColumn) ->
                    match cell key with
                    | Some(Text v) -> columns.Add(textColumn, Sql.Text v); columns.Add(linkColumn, Sql.Null)
                    | Some(Links [id]) -> columns.Add(textColumn, Sql.Null); columns.Add(linkColumn, Sql.Text id)
                    | _ -> columns.Add(textColumn, Sql.Null); columns.Add(linkColumn, Sql.Null)
                | Schema.Storage.SampleOrDataColumns(sampleColumn, dataColumn) ->
                    match single key with
                    | Some id when kindOf id = Some "Data" -> columns.Add(sampleColumn, Sql.Null); columns.Add(dataColumn, Sql.Text id)
                    | Some id -> columns.Add(sampleColumn, Sql.Text id); columns.Add(dataColumn, Sql.Null)
                    | None -> columns.Add(sampleColumn, Sql.Null); columns.Add(dataColumn, Sql.Null)
                | Schema.Storage.Association(table, valueColumn) ->
                    let values = match cell key with Some(Texts v) -> v | Some(Links v) -> v | _ -> []
                    lists.Add(table, spec.Table + "_id", valueColumn, values)
                | Schema.Storage.Endpoint direction ->
                    match single key with
                    | Some id when kindOf id = Some "Data" -> endpoints.Add(direction, None, Some id)
                    | Some id -> endpoints.Add(direction, Some id, None)
                    | None -> ()
            { PColumns = List.ofSeq columns; PLists = List.ofSeq lists; PEndpoints = List.ofSeq endpoints; PExtensions = extensions }

    // ---- statements ----

    type private Op = string * (string * SqlValue) list

    let private registryInsert id kind (ordinal: int64) : Op =
        "INSERT INTO entity(arc_id,id,type,ordinal) VALUES($arc,$id,$type,$ordinal)",
        ["id", text id; "type", text kind; "ordinal", SqlValue.Integer(ordinal)]

    let private coreInsert (table: string) id kind (columns: (string * Sql) list) : Op =
        let all = ("type", Sql.Text kind) :: columns
        "INSERT INTO " + table + "(arc_id,id," + (all |> List.map fst |> String.concat ",") + ") VALUES($arc,$id," + (all |> List.mapi (fun index _ -> "$p" + string index) |> String.concat ",") + ")",
        ("id", text id) :: (all |> List.mapi (fun index (_, value) -> "p" + string index, sqlOf value))

    let private coreUpdate (table: string) id (columns: (string * Sql) list) : Op =
        "UPDATE " + table + " SET " + (columns |> List.mapi (fun index (column, _) -> column + "=$p" + string index) |> String.concat ",") + " WHERE arc_id=$arc AND id=$id",
        ("id", text id) :: (columns |> List.mapi (fun index (_, value) -> "p" + string index, sqlOf value))

    /// Insert values from the given list position on.
    let private listInserts id (start: int) (table: string, owner: string, valueColumn: string, values: string list) : Op list =
        values |> List.skip start |> List.mapi (fun offset value ->
            let position = start + offset
            "INSERT INTO " + table + "(arc_id," + owner + ",position," + valueColumn + ") VALUES($arc,$id,$position,$value)",
            ["id", text id; "position", integer position; "value", text value])

    let private listDelete id (table: string, owner: string) : Op =
        "DELETE FROM " + table + " WHERE arc_id=$arc AND " + owner + "=$id", ["id", text id]

    let private listDeleteFrom id (position: int) (table: string, owner: string) : Op =
        "DELETE FROM " + table + " WHERE arc_id=$arc AND " + owner + "=$id AND position >= $position", ["id", text id; "position", integer position]

    let private endpointInsert id (direction: string, sample: string option, data: string option) : Op =
        "INSERT INTO process_io(arc_id,process_id,direction,sample_id,data_id) VALUES($arc,$id,$direction,$sample,$data)",
        ["id", text id; "direction", text direction; "sample", sqlOfOption sample; "data", sqlOfOption data]

    let private endpointDelete id (direction: string) : Op =
        "DELETE FROM process_io WHERE arc_id=$arc AND process_id=$id AND direction=$direction", ["id", text id; "direction", text direction]

    let private extensionInserts id (property: string) (rows: ExtRow list) : Op list =
        rows |> List.map (fun row ->
            "INSERT INTO entity_extension(arc_id,owner_id,property,path,parent_path,position,storage,value_text,value_number,value_bool,target_id) VALUES($arc,$owner,$property,$path,$parent,$position,$storage,$text,$number,$bool,$target)",
            ["owner", text id; "property", text property; "path", text row.XPath; "parent", sqlOfOption row.XParent
             "position", (match row.XPosition with Some v -> integer v | None -> SqlValue.Null())
             "storage", text row.XStorage; "text", sqlOfOption row.XText
             "number", (match row.XNumber with Some v -> SqlValue.Real v | None -> SqlValue.Null())
             "bool", (match row.XBool with Some v -> integer (if v then 1 else 0) | None -> SqlValue.Null())
             "target", sqlOfOption row.XTarget])

    let private extensionDelete id (property: string) : Op =
        "DELETE FROM entity_extension WHERE arc_id=$arc AND owner_id=$id AND property=$property", ["id", text id; "property", text property]

    /// Rows are equal when everything except the pre-adoption supplied ID matches.
    let private sameIgnoringSuppliedId (a: EntityRow) (b: EntityRow) = obj.ReferenceEquals(a, b) || ({ a with SuppliedId = None } = { b with SuppliedId = None })

    /// Apply the difference between two states to one ARC. Call inside a transaction. The ordinal map
    /// supplies positions for known IDs, new IDs follow the highest known ordinal in list order.
    /// The statements run in an order that respects immediate RESTRICT references and immediate CHECK constraints.
    let write (db: SqliteConnection) arcId (ordinals: Map<string, int64>) (before: State) (after: State) : Map<string, int64> =
        let beforeRows = Dictionary<string, EntityRow>()
        for row in before.Entities do beforeRows[row.Id] <- row
        let afterRows = Dictionary<string, EntityRow>()
        for row in after.Entities do afterRows[row.Id] <- row
        let beforeKind id = match beforeRows.TryGetValue id with | true, row -> Some row.Kind | _ -> None
        let afterKind id = match afterRows.TryGetValue id with | true, row -> Some row.Kind | _ -> None
        let mutable highest = ordinals |> Map.fold (fun current _ value -> max current value) 0L
        let assigned = Dictionary<string, int64>()
        for row in after.Entities do
            match Map.tryFind row.Id ordinals with
            | Some ordinal -> assigned[row.Id] <- ordinal
            | None ->
                highest <- highest + 1L
                assigned[row.Id] <- highest
        let hasReplacement = after.Entities |> List.exists (fun row -> match beforeRows.TryGetValue row.Id with | true, old -> old.Kind <> row.Kind | _ -> false)

        let inserts = ResizeArray<Op>()
        let removals = ResizeArray<Op>()
        let updates = ResizeArray<Op>()
        let clears = ResizeArray<Op>()
        let descriptorDeletes = ResizeArray<Op>()
        let coreDeletes = ResizeArray<Op>()
        let registryDeletes = ResizeArray<Op>()
        let replacements = ResizeArray<Op>()
        let additions = ResizeArray<Op>()

        let insertEntity (target: ResizeArray<Op>) (row: EntityRow) (projection: Projection) =
            target.Add(registryInsert row.Id row.Kind assigned[row.Id])
            match Schema.tryEntity row.Kind with
            | Some spec -> target.Add(coreInsert spec.Table row.Id row.Kind projection.PColumns)
            | None -> ()
        let insertRows (row: EntityRow) (projection: Projection) =
            for list in projection.PLists do additions.AddRange(listInserts row.Id 0 list)
            for endpoint in projection.PEndpoints do additions.Add(endpointInsert row.Id endpoint)
            for property, rows in projection.PExtensions do additions.AddRange(extensionInserts row.Id property rows)
        let removeAllRows (row: EntityRow) (projection: Projection) =
            for table, owner, _, values in projection.PLists do
                if not (List.isEmpty values) then removals.Add(listDelete row.Id (table, owner))
            for direction, _, _ in projection.PEndpoints do removals.Add(endpointDelete row.Id direction)
            if not (List.isEmpty projection.PExtensions) then
                removals.Add("DELETE FROM entity_extension WHERE arc_id=$arc AND owner_id=$id", ["id", text row.Id])
        let deleteEntity (row: EntityRow) (projection: Projection) =
            removeAllRows row projection
            match Schema.tryEntity row.Kind with
            | Some spec ->
                let references = referenceColumns spec
                let held = projection.PColumns |> List.filter (fun (column, value) -> List.contains column references && value <> Sql.Null)
                if not (List.isEmpty held) then
                    clears.Add(coreUpdate spec.Table row.Id (held |> List.map (fun (column, _) -> column, Sql.Null)))
                let delete = "DELETE FROM " + spec.Table + " WHERE arc_id=$arc AND id=$id", ["id", text row.Id]
                if row.Kind = "Descriptor" then descriptorDeletes.Add(delete) else coreDeletes.Add(delete)
            | None -> ()
            registryDeletes.Add("DELETE FROM entity WHERE arc_id=$arc AND id=$id", ["id", text row.Id])

        // Classify entities. Rows with the same ID and another kind are removed and inserted again.
        for row in before.Entities do
            match afterRows.TryGetValue row.Id with
            | true, next when next.Kind = row.Kind ->
                let changed = not (sameIgnoringSuppliedId row next)
                if changed || (hasReplacement && (row.Kind = "Process" || row.Kind = "Descriptor")) then
                    let old, current = project beforeKind row, project afterKind next
                    for (table, owner, _, oldValues), ((_, _, _, newValues) as list) in List.zip old.PLists current.PLists do
                        if oldValues <> newValues then
                            // Rows before the first difference keep their positions.
                            let kept = List.zip (List.truncate newValues.Length oldValues) (List.truncate oldValues.Length newValues) |> List.takeWhile (fun (a, b) -> a = b) |> List.length
                            if oldValues.Length > kept then
                                removals.Add(if kept = 0 then listDelete row.Id (table, owner) else listDeleteFrom row.Id kept (table, owner))
                            additions.AddRange(listInserts row.Id kept list)
                    for direction in ["input"; "output"] do
                        let find (endpoints: (string * string option * string option) list) = endpoints |> List.tryFind (fun (name, _, _) -> name = direction)
                        let oldEndpoint, newEndpoint = find old.PEndpoints, find current.PEndpoints
                        if oldEndpoint <> newEndpoint then
                            if oldEndpoint.IsSome then removals.Add(endpointDelete row.Id direction)
                            match newEndpoint with Some endpoint -> additions.Add(endpointInsert row.Id endpoint) | None -> ()
                    let oldExtensions, newExtensions = Map.ofList old.PExtensions, Map.ofList current.PExtensions
                    let properties = Seq.append (Map.toSeq oldExtensions |> Seq.map fst) (Map.toSeq newExtensions |> Seq.map fst) |> Seq.distinct |> List.ofSeq
                    for property in properties do
                        let oldRows, newRows = Map.tryFind property oldExtensions, Map.tryFind property newExtensions
                        if oldRows <> newRows then
                            if oldRows.IsSome then removals.Add(extensionDelete row.Id property)
                            match newRows with Some rows -> additions.AddRange(extensionInserts row.Id property rows) | None -> ()
                    let changedColumns = List.zip old.PColumns current.PColumns |> List.filter (fun (a, b) -> a <> b) |> List.map snd
                    if not (List.isEmpty changedColumns) then
                        updates.Add(coreUpdate (Schema.tryEntity row.Kind).Value.Table row.Id changedColumns)
            | true, next ->
                deleteEntity row (project beforeKind row)
                let projection = project afterKind next
                insertEntity replacements next projection
                insertRows next projection
            | _ -> deleteEntity row (project beforeKind row)
        for row in after.Entities do
            if not (beforeRows.ContainsKey row.Id) then
                let projection = project afterKind row
                insertEntity inserts row projection
                insertRows row projection

        for phase in [inserts; removals; updates; clears; descriptorDeletes; coreDeletes; registryDeletes; replacements; additions] do
            for sql, values in phase do execute db sql (("arc", text arcId) :: values) |> ignore
        assigned |> Seq.map (fun pair -> pair.Key, pair.Value) |> Map.ofSeq

    // ---- reading ----

    /// Rebuild the state of one ARC. Entities follow their registry ordinals.
    let load (db: SqliteConnection) arcId : State * Map<string, int64> =
        db.WithTransaction(fun () ->
            let query (sql: string) = db.Query(sql, parameters ["arc", text arcId])
            let root = (query "SELECT root_id FROM arc WHERE arc_id=$arc").[0].Get(0).AsText()
            let registry = query "SELECT id,type,ordinal FROM entity WHERE arc_id=$arc ORDER BY ordinal"
            let coreRows = Dictionary<string, Dictionary<string, SqlRow>>()
            let lists = Dictionary<string, Dictionary<string, ResizeArray<string>>>()
            let collect (table: string) (rows: seq<SqlRow>) =
                let owners = Dictionary<string, ResizeArray<string>>()
                for row in rows do
                    let id = row.Get(0).AsText()
                    if not (owners.ContainsKey id) then owners[id] <- ResizeArray<string>()
                    owners[id].Add(row.Get(1).AsText())
                lists[table] <- owners
            for spec in Schema.entities do
                let rows = Dictionary<string, SqlRow>()
                for row in query ("SELECT * FROM " + spec.Table + " WHERE arc_id=$arc") do rows[row.GetByName("id").AsText()] <- row
                coreRows[spec.Table] <- rows
                for property in spec.Properties do
                    match property.Storage with
                    | Schema.Storage.Association(table, valueColumn) ->
                        collect table (query ("SELECT " + spec.Table + "_id," + valueColumn + " FROM " + table + " WHERE arc_id=$arc ORDER BY " + spec.Table + "_id,position"))
                    | _ -> ()
            collect "entity_additional_type" (query "SELECT entity_id,value FROM entity_additional_type WHERE arc_id=$arc ORDER BY entity_id,position")
            let endpoints = Dictionary<string, SqlRow>()
            for row in query "SELECT process_id,direction,sample_id,data_id FROM process_io WHERE arc_id=$arc" do
                endpoints[row.Get(0).AsText() + "/" + row.Get(1).AsText()] <- row
            let extensionRows = Dictionary<string, Dictionary<string, ResizeArray<SqlRow>>>()
            for row in query "SELECT * FROM entity_extension WHERE arc_id=$arc" do
                let owner, property = row.GetByName("owner_id").AsText(), row.GetByName("property").AsText()
                if not (extensionRows.ContainsKey owner) then extensionRows[owner] <- Dictionary<string, ResizeArray<SqlRow>>()
                let byProperty = extensionRows[owner]
                if not (byProperty.ContainsKey property) then byProperty[property] <- ResizeArray<SqlRow>()
                byProperty[property].Add(row)
            let extension (rows: ResizeArray<SqlRow>) =
                let byPath = Dictionary<string, SqlRow>()
                let children = Dictionary<string, ResizeArray<int * string>>()
                for row in rows do
                    let path = row.GetByName("path").AsText()
                    byPath[path] <- row
                    match optText row "parent_path" with
                    | Some parent ->
                        if not (children.ContainsKey parent) then children[parent] <- ResizeArray<int * string>()
                        children[parent].Add(int (row.GetByName("position").AsInteger()), path)
                    | None -> ()
                let rec build path =
                    let row = byPath[path]
                    match row.GetByName("storage").AsText() with
                    | "text" -> ExtensionText(row.GetByName("value_text").AsText())
                    | "number" -> ExtensionNumber(realOf (row.GetByName("value_number")))
                    | "bool" -> ExtensionBool(row.GetByName("value_bool").AsInteger() = 1L)
                    | "null" -> ExtensionNull
                    | "blob" -> ExtensionBlob(row.GetByName("value_text").AsText())
                    | "object" -> ExtensionObject(row.GetByName("target_id").AsText())
                    | "collection" ->
                        match children.TryGetValue path with
                        | true, items -> ExtensionCollection(items |> Seq.sortBy fst |> Seq.map (snd >> build) |> List.ofSeq)
                        | _ -> ExtensionCollection []
                    | _ -> invalidOp "Unknown extension storage type."
                build ""
            let values table id =
                match lists.TryGetValue table with
                | true, owners -> (match owners.TryGetValue id with | true, items -> List.ofSeq items | _ -> [])
                | _ -> []
            let entities = ResizeArray<EntityRow>()
            let ordinals = ResizeArray<string * int64>()
            for entry in registry do
                let id, kind = entry.Get(0).AsText(), entry.Get(1).AsText()
                let properties =
                    match Schema.tryEntity kind with
                    | None -> ["additionalTypes", Texts(values "entity_additional_type" id)]
                    | Some spec ->
                        let row =
                            match coreRows[spec.Table].TryGetValue id with
                            | true, found -> found
                            | _ -> invalidOp ("Missing " + kind + " row: " + id)
                        let present column = let value = row.GetByName(column) in if value.IsNull then None else Some(value.AsText())
                        spec.Properties |> List.collect (fun property ->
                            let key = property.Name
                            match property.Storage with
                            | Schema.Storage.Column column ->
                                match present column, property.Shape with
                                | Some v, Schema.Shape.Link _ -> [key, Links [v]]
                                | Some v, _ -> [key, Text v]
                                | None, _ -> []
                            | Schema.Storage.TextOrNumberColumns(textColumn, numberColumn) ->
                                match present textColumn with
                                | Some v -> [key, Text v]
                                | None ->
                                    let number = row.GetByName(numberColumn)
                                    if number.IsNull then [] else [key, Number(realOf number)]
                            | Schema.Storage.TextOrLinkColumns(textColumn, linkColumn) ->
                                match present textColumn, present linkColumn with
                                | Some v, _ -> [key, Text v]
                                | None, Some v -> [key, Links [v]]
                                | None, None -> []
                            | Schema.Storage.SampleOrDataColumns(sampleColumn, dataColumn) ->
                                match present sampleColumn, present dataColumn with
                                | Some v, _ -> [key, Links [v]]
                                | None, Some v -> [key, Links [v]]
                                | None, None -> []
                            | Schema.Storage.Association(table, _) ->
                                match property.Shape with
                                | Schema.Shape.Texts -> [key, Texts(values table id)]
                                | _ -> [key, Links(values table id)]
                            | Schema.Storage.Endpoint direction ->
                                match endpoints.TryGetValue(id + "/" + direction) with
                                | true, found ->
                                    let sample, data = found.GetByName("sample_id"), found.GetByName("data_id")
                                    [key, Links [(if sample.IsNull then data.AsText() else sample.AsText())]]
                                | _ -> [])
                let extensions =
                    match extensionRows.TryGetValue id with
                    | true, byProperty -> byProperty |> Seq.map (fun pair -> pair.Key, extension pair.Value) |> Map.ofSeq
                    | _ -> Map.empty
                entities.Add({ Id = id; Kind = kind; SuppliedId = Some id; Properties = Map.ofList properties; Extensions = extensions })
                ordinals.Add(id, entry.Get(2).AsInteger())
            { Root = root; Entities = List.ofSeq entities }, Map.ofSeq ordinals)

    // ---- history records ----

    /// Change records for every entity that differs between two states.
    let changes (before: State) (after: State) (ordinalOf: string -> int64) : Change list =
        let beforeRows = Dictionary<string, EntityRow>()
        for row in before.Entities do beforeRows[row.Id] <- row
        let afterRows = Dictionary<string, EntityRow>()
        for row in after.Entities do afterRows[row.Id] <- row
        let result = ResizeArray<Change>()
        for row in before.Entities do
            match afterRows.TryGetValue row.Id with
            | true, next ->
                if not (sameIgnoringSuppliedId row next) then
                    result.Add({ EntityId = row.Id; Ordinal = ordinalOf row.Id; Before = Some row; After = Some next })
            | _ -> result.Add({ EntityId = row.Id; Ordinal = ordinalOf row.Id; Before = Some row; After = None })
        for row in after.Entities do
            if not (beforeRows.ContainsKey row.Id) then
                result.Add({ EntityId = row.Id; Ordinal = ordinalOf row.Id; Before = None; After = Some row })
        List.ofSeq result

    /// Apply the recorded rows to a state: the before rows for undo, the after rows for redo.
    /// Restored entities return to their recorded list position, and the second result carries those ordinals.
    let applyChanges (state: State) (ordinals: Map<string, int64>) (records: Change list) (redo: bool) : State * Map<string, int64> =
        let rows = Dictionary<string, EntityRow * int64>()
        for row in state.Entities do rows[row.Id] <- (row, Map.find row.Id ordinals)
        let mutable known = ordinals
        for record in records do
            match (if redo then record.After else record.Before) with
            | Some row ->
                rows[record.EntityId] <- (row, record.Ordinal)
                known <- Map.add record.EntityId record.Ordinal known
            | None -> rows.Remove(record.EntityId) |> ignore
        let entities = rows.Values |> Seq.sortBy snd |> Seq.map fst |> List.ofSeq
        { state with Entities = entities }, known

    let addArc (db: SqliteConnection) arcId state folder baseline =
        Model.validate state
        db.WithTransaction(fun () ->
            execute db "INSERT INTO arc(arc_id,root_id,folder,baseline,saved_graph,cursor,revision) VALUES($arc,$root,$folder,$baseline,$saved,0,0)"
                ["arc",text arcId; "root",text state.Root; "folder",optional folder
                 "baseline",optional baseline; "saved",optional (baseline |> Option.map (fun _ -> Codec.encodeGraph state))] |> ignore
            write db arcId Map.empty { Root = state.Root; Entities = [] } state |> ignore)

    /// Keep the displaced state and its history before a reload replaces them.
    let archive (db: SqliteConnection) arcId (current: State) =
        let archiveId = Identifier.newId()
        let bindings = ["archive",text archiveId; "arc",text arcId]
        execute db "INSERT INTO arc_archive(archive_id,arc_id,root_id,folder,state,baseline,saved_graph,cursor,revision) SELECT $archive,arc_id,root_id,folder,$state,baseline,saved_graph,cursor,revision FROM arc WHERE arc_id=$arc"
            (("state", text (Codec.encodeState current)) :: bindings) |> ignore
        execute db "INSERT INTO archived_history(archive_id,sequence,operation_id,kind,changes) SELECT $archive,sequence,operation_id,kind,changes FROM history WHERE arc_id=$arc" bindings |> ignore
        archiveId
