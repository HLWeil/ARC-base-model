namespace ARCSession.Internal

open ARCtrl.Helper

open ARCtrl.Internal

open PolyglotSQLite
open Fable.Core

[<AttachMembers>]
type internal Database(connection: SqliteConnection, path: string, close: unit -> unit) =
    member _.Connection = connection
    member _.Path = path
    member _.Close() = close()
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
    let private tables = ["Dataset","dataset"; "Process","process"; "Sample","sample"; "Data","data"; "Recipe","recipe"; "Annotation","annotation"; "FormalParameter","formal_parameter"; "DefinedTerm","defined_term"; "DefinedTermSet","defined_term_set"; "Descriptor","descriptor"; "Agent","agent"; "Organization","organization"; "ScholarlyArticle","scholarly_article"]
    let private extensionDDL = """
CREATE TABLE IF NOT EXISTS entity_extension(
 arc_id TEXT NOT NULL, owner_id TEXT NOT NULL, property TEXT NOT NULL, path TEXT NOT NULL,
 parent_path TEXT, position INTEGER, storage TEXT NOT NULL CHECK(storage IN ('text','number','bool','null','blob','object','collection')),
 value_text TEXT, value_number REAL, value_bool INTEGER CHECK(value_bool IN (0,1)), value_blob BLOB,
 target_id TEXT, PRIMARY KEY(arc_id,owner_id,property,path),
 FOREIGN KEY(arc_id,owner_id) REFERENCES entity(arc_id,id),
 FOREIGN KEY(arc_id,target_id) REFERENCES entity(arc_id,id),
 FOREIGN KEY(arc_id,owner_id,property,parent_path) REFERENCES entity_extension(arc_id,owner_id,property,path) DEFERRABLE INITIALLY DEFERRED,
 CHECK((path='' AND parent_path IS NULL AND position IS NULL) OR (path<>'' AND parent_path IS NOT NULL AND position>=0)),
 CHECK((storage='text' AND value_text IS NOT NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='number' AND value_text IS NULL AND value_number IS NOT NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='bool' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NOT NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='blob' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NOT NULL AND target_id IS NULL)
 OR (storage='object' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NOT NULL)
 OR (storage IN ('null','collection') AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)));
CREATE INDEX IF NOT EXISTS extension_target ON entity_extension(target_id,arc_id,owner_id);
CREATE INDEX IF NOT EXISTS extension_text_search ON entity_extension(property,value_text,arc_id,owner_id);
CREATE INDEX IF NOT EXISTS extension_number_search ON entity_extension(property,value_number,arc_id,owner_id);
"""
#if FABLE_COMPILER && !FABLE_COMPILER_PYTHON
    [<Emit("new Uint8Array($0)")>]
    let private byteBuffer (_values: byte[]) : byte[] = nativeOnly
#else
    let private byteBuffer values = values
#endif
    let private blobBytes (source: string) =
        let alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
        let result = ResizeArray<byte>()
        let mutable buffer = 0
        let mutable bits = 0
        for c in source do
            if c <> '=' then
                buffer <- (buffer <<< 6) ||| alphabet.IndexOf(c)
                bits <- bits + 6
                if bits >= 8 then
                    bits <- bits - 8
                    result.Add(byte ((buffer >>> bits) &&& 255))
                    buffer <- buffer &&& ((1 <<< bits) - 1)
        result.ToArray() |> byteBuffer
    let mirror (db: SqliteConnection) arcId state =
        let execute db sql values = execute db sql (("arc",text arcId)::values)
        for table in ["entity_extension"; "entity_reference"; "entity_value"; "dataset_part"; "dataset_process"] do execute db ("DELETE FROM " + table + " WHERE arc_id=$arc") []
        for _,table in tables do execute db ("DELETE FROM " + table + " WHERE arc_id=$arc") []
        execute db "DELETE FROM entity WHERE arc_id=$arc" []
        for row in state.Entities do
            execute db "INSERT INTO entity(arc_id,id,type) VALUES($arc,$id,$type)" ["id",text row.Id; "type",text row.Kind]
        for row in state.Entities do
            let table = tables |> List.tryFind (fun (kind,_) -> kind = row.Kind) |> Option.map snd
            let payload = Codec.rowElement row |> Codec.write
            if row.Kind = "Process" then
                let endpoint key kind = Model.links key row |> List.tryHead |> Option.filter (fun id -> state.Entities |> List.exists (fun r -> r.Id = id && r.Kind = kind))
                execute db "INSERT INTO process(arc_id,id,name,input_sample_id,output_sample_id,input_data_id,output_data_id,payload) VALUES($arc,$id,$name,$input,$output,$inputData,$outputData,$payload)"
                    ["id",text row.Id; "name",optional(Model.text "name" row); "input",optional(endpoint "input" "Sample"); "output",optional(endpoint "output" "Sample"); "inputData",optional(endpoint "input" "Data"); "outputData",optional(endpoint "output" "Data"); "payload",text payload]
            elif row.Kind = "Sample" then
                execute db "INSERT INTO sample(arc_id,id,name,payload) VALUES($arc,$id,$name,$payload)" ["id",text row.Id; "name",optional(Model.text "name" row); "payload",text payload]
            elif table.IsSome then execute db ("INSERT INTO " + table.Value + "(arc_id,id,payload) VALUES($arc,$id,$payload)") ["id",text row.Id; "payload",text payload]
        for row in state.Entities do
            for key,cell in Map.toList row.Properties do
                let value position storage stringValue numberValue =
                    execute db "INSERT INTO entity_value VALUES($arc,$id,$property,$position,$storage,$text,$number)"
                        ["id",text row.Id; "property",text key; "position",SqlValue.Integer(int64 position); "storage",text storage; "text",optional stringValue; "number",numberValue |> Option.map SqlValue.Real |> Option.defaultWith SqlValue.Null]
                match cell with
                | Text v -> value 0 "text" (Some v) None
                | Number v -> value 0 "number" None (Some v)
                | Texts vs -> for position,v in List.indexed vs do value position "texts" (Some v) None
                | Links vs ->
                    for position,id in List.indexed vs do
                        execute db "INSERT INTO entity_reference VALUES($arc,$owner,$property,$position,$target)"
                            ["owner",text row.Id; "property",text key; "position",SqlValue.Integer(int64 position); "target",text id]
                        if row.Kind = "Dataset" && (key = "hasParts" || key = "processes") then
                            let table, owner, target = if key = "hasParts" then "dataset_part","parent_id","child_id" else "dataset_process","dataset_id","process_id"
                            execute db ("INSERT INTO " + table + "(arc_id," + owner + ",position," + target + ") VALUES($arc,$owner,$position,$target)")
                                ["owner",text row.Id; "position",SqlValue.Integer(int64 position); "target",text id]
        for row in state.Entities do
            for KeyValue(key, cell) in row.Extensions do
                let rec insert path parent position cell =
                    let storage, textValue, numberValue, boolValue, blobValue, target =
                        match cell with
                        | ExtensionText v -> "text", text v, SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Null()
                        | ExtensionNumber v -> "number", SqlValue.Null(), SqlValue.Real(v), SqlValue.Null(), SqlValue.Null(), SqlValue.Null()
                        | ExtensionBool v -> "bool", SqlValue.Null(), SqlValue.Null(), SqlValue.Integer(if v then 1L else 0L), SqlValue.Null(), SqlValue.Null()
                        | ExtensionBlob v -> "blob", SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Blob(blobBytes v), SqlValue.Null()
                        | ExtensionObject id -> "object", SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), text id
                        | ExtensionNull -> "null", SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Null()
                        | ExtensionCollection _ -> "collection", SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Null(), SqlValue.Null()
                    execute db "INSERT INTO entity_extension VALUES($arc,$owner,$key,$path,$parent,$position,$storage,$text,$number,$bool,$blob,$target)"
                        ["owner", text row.Id; "key", text key; "path", text path; "parent", optional parent
                         "position", position |> Option.map (int64 >> SqlValue.Integer) |> Option.defaultWith SqlValue.Null
                         "storage", text storage; "text", textValue; "number", numberValue; "bool", boolValue; "blob", blobValue; "target", target]
                    match cell with
                    | ExtensionCollection values ->
                        for index, value in List.indexed values do insert (path + "/" + string index) (Some path) (Some index) value
                    | _ -> ()
                insert "" None None cell
    let initialize (db: SqliteConnection) =
        let entityTables = tables |> List.map (fun (_,table) ->
            let fields =
                if table = "process" then
                    "name TEXT NOT NULL, input_sample_id TEXT, output_sample_id TEXT, input_data_id TEXT, output_data_id TEXT, "
                elif table = "sample" then "name TEXT NOT NULL, "
                else ""
            let endpoints =
                if table = "process" then
                    ["input_sample_id","sample"; "output_sample_id","sample"; "input_data_id","data"; "output_data_id","data"]
                    |> List.map (fun (column,target) -> ", FOREIGN KEY(arc_id," + column + ") REFERENCES " + target + "(arc_id,id) DEFERRABLE INITIALLY DEFERRED")
                    |> String.concat ""
                else ""
            "CREATE TABLE " + table + "(arc_id TEXT NOT NULL, id TEXT NOT NULL, " + fields +
            "payload TEXT NOT NULL, PRIMARY KEY(arc_id,id), FOREIGN KEY(arc_id,id) REFERENCES entity(arc_id,id)" + endpoints + ");") |> String.concat "\n"
        // DDL and metadata are initialized together; never partially initialize a repository.
        db.WithTransaction(fun () ->
            let ddl = ("""
CREATE TABLE repository(singleton INTEGER PRIMARY KEY CHECK(singleton=1), schema_version INTEGER NOT NULL);
INSERT INTO repository VALUES(1,4);
CREATE TABLE session(arc_id TEXT NOT NULL PRIMARY KEY, root_id TEXT NOT NULL, folder TEXT,
 state TEXT NOT NULL, baseline TEXT, saved_graph TEXT, cursor INTEGER NOT NULL, revision INTEGER NOT NULL,
 FOREIGN KEY(arc_id,root_id) REFERENCES dataset(arc_id,id) DEFERRABLE INITIALLY DEFERRED);
CREATE TABLE entity(arc_id TEXT NOT NULL REFERENCES session(arc_id), id TEXT NOT NULL, type TEXT NOT NULL, PRIMARY KEY(arc_id,id));
            """ + entityTables + """
CREATE TABLE entity_value(arc_id TEXT NOT NULL, entity_id TEXT NOT NULL, property TEXT NOT NULL,
 position INTEGER NOT NULL CHECK(position>=0), storage TEXT NOT NULL CHECK(storage IN ('text','number','texts')),
 value_text TEXT, value_number REAL, PRIMARY KEY(arc_id,entity_id,property,position),
 FOREIGN KEY(arc_id,entity_id) REFERENCES entity(arc_id,id),
 CHECK((storage='number' AND value_text IS NULL AND value_number IS NOT NULL) OR (storage<>'number' AND value_text IS NOT NULL AND value_number IS NULL)));
CREATE TABLE entity_reference(arc_id TEXT NOT NULL, owner_id TEXT NOT NULL, property TEXT NOT NULL,
 position INTEGER NOT NULL CHECK(position>=0), target_id TEXT NOT NULL, PRIMARY KEY(arc_id,owner_id,property,position),
 FOREIGN KEY(arc_id,owner_id) REFERENCES entity(arc_id,id), FOREIGN KEY(arc_id,target_id) REFERENCES entity(arc_id,id));
CREATE INDEX reference_target ON entity_reference(target_id,arc_id,owner_id);
CREATE INDEX entity_type ON entity(type,arc_id,id);
CREATE INDEX value_text_search ON entity_value(property,value_text,arc_id,entity_id);
CREATE INDEX value_number_search ON entity_value(property,value_number,arc_id,entity_id);
CREATE TABLE dataset_part(arc_id TEXT NOT NULL, parent_id TEXT NOT NULL, position INTEGER NOT NULL CHECK(position>=0), child_id TEXT NOT NULL,
 PRIMARY KEY(arc_id,parent_id,position), FOREIGN KEY(arc_id,parent_id) REFERENCES dataset(arc_id,id), FOREIGN KEY(arc_id,child_id) REFERENCES dataset(arc_id,id));
CREATE TABLE dataset_process(arc_id TEXT NOT NULL, dataset_id TEXT NOT NULL, position INTEGER NOT NULL CHECK(position>=0), process_id TEXT NOT NULL,
 PRIMARY KEY(arc_id,dataset_id,position), FOREIGN KEY(arc_id,dataset_id) REFERENCES dataset(arc_id,id), FOREIGN KEY(arc_id,process_id) REFERENCES process(arc_id,id));
CREATE TABLE history(arc_id TEXT NOT NULL REFERENCES session(arc_id), sequence INTEGER NOT NULL, operation_id TEXT NOT NULL, kind TEXT NOT NULL,
 before_state TEXT NOT NULL, after_state TEXT NOT NULL, PRIMARY KEY(arc_id,sequence));
CREATE TABLE journal(arc_id TEXT NOT NULL REFERENCES session(arc_id), sequence INTEGER NOT NULL, operation_id TEXT NOT NULL, kind TEXT NOT NULL,
 action TEXT NOT NULL, revision INTEGER NOT NULL, PRIMARY KEY(arc_id,sequence));
CREATE TABLE arc_archive(archive_id TEXT NOT NULL PRIMARY KEY, arc_id TEXT NOT NULL, root_id TEXT NOT NULL, folder TEXT,
 state TEXT NOT NULL, baseline TEXT, saved_graph TEXT, cursor INTEGER NOT NULL, revision INTEGER NOT NULL);
CREATE TABLE archived_history(archive_id TEXT NOT NULL REFERENCES arc_archive(archive_id), sequence INTEGER NOT NULL,
 operation_id TEXT NOT NULL, kind TEXT NOT NULL, before_state TEXT NOT NULL, after_state TEXT NOT NULL, PRIMARY KEY(archive_id,sequence));
CREATE TABLE archived_journal(archive_id TEXT NOT NULL REFERENCES arc_archive(archive_id), sequence INTEGER NOT NULL,
 operation_id TEXT NOT NULL, kind TEXT NOT NULL, action TEXT NOT NULL, revision INTEGER NOT NULL, PRIMARY KEY(archive_id,sequence));
            """ + extensionDDL)
            for statement in ddl.Split(';') do
                if statement.Trim() <> "" then db.Execute(statement))

    let validateRepository (db: SqliteConnection) =
        let rows = db.Query("SELECT schema_version FROM repository WHERE singleton=1")
        if rows.Count <> 1 || rows[0].Get(0).AsInteger() <> 4L then
            invalidOp "Unsupported ARC repository schema. Expected version 4; legacy sessions are not migrated."
        if (db.Query("PRAGMA foreign_keys")).[0].Get(0).AsInteger() <> 1L then
            invalidOp "ARC repositories require foreign-key enforcement."
        // Validate columns, keys and relationship scopes through schema metadata.
        // Do not deserialize snapshots or scan every ARC while opening a database.
        let shapes = [
            "repository","singleton,schema_version",["singleton"]
            "session","arc_id,root_id,folder,state,baseline,saved_graph,cursor,revision",["arc_id"]
            "entity","arc_id,id,type",["arc_id";"id"]
            "entity_value","arc_id,entity_id,property,position,storage,value_text,value_number",["arc_id";"entity_id";"property";"position"]
            "entity_reference","arc_id,owner_id,property,position,target_id",["arc_id";"owner_id";"property";"position"]
            "entity_extension","arc_id,owner_id,property,path,parent_path,position,storage,value_text,value_number,value_bool,value_blob,target_id",["arc_id";"owner_id";"property";"path"]
            "dataset_part","arc_id,parent_id,position,child_id",["arc_id";"parent_id";"position"]
            "dataset_process","arc_id,dataset_id,position,process_id",["arc_id";"dataset_id";"position"]
            "history","arc_id,sequence,operation_id,kind,before_state,after_state",["arc_id";"sequence"]
            "journal","arc_id,sequence,operation_id,kind,action,revision",["arc_id";"sequence"]
            "arc_archive","archive_id,arc_id,root_id,folder,state,baseline,saved_graph,cursor,revision",["archive_id"]
            "archived_history","archive_id,sequence,operation_id,kind,before_state,after_state",["archive_id";"sequence"]
            "archived_journal","archive_id,sequence,operation_id,kind,action,revision",["archive_id";"sequence"]
        ]
        let typedShapes = tables |> List.map (fun (_,table) ->
            let fields = if table = "process" then ",name,input_sample_id,output_sample_id,input_data_id,output_data_id" elif table = "sample" then ",name" else ""
            table,"arc_id,id,payload" + fields,["arc_id";"id"])
        for table,columns,key in shapes @ typedShapes do
            db.Query("SELECT " + columns + " FROM " + table + " LIMIT 0") |> ignore
            let actualKey = db.Query("PRAGMA table_info(" + table + ")")
                            |> Seq.filter (fun row -> row.GetByName("pk").AsInteger() > 0L)
                            |> Seq.sortBy (fun row -> row.GetByName("pk").AsInteger())
                            |> Seq.map (fun row -> row.GetByName("name").AsText()) |> Seq.toList
            if actualKey <> key then invalidOp ("Invalid ARC repository primary key: " + table)
        let relationships = [
            "session","dataset",["arc_id","arc_id";"root_id","id"]
            "entity","session",["arc_id","arc_id"]
            "entity_value","entity",["arc_id","arc_id";"entity_id","id"]
            "entity_reference","entity",["arc_id","arc_id";"owner_id","id"]
            "entity_reference","entity",["arc_id","arc_id";"target_id","id"]
            "entity_extension","entity",["arc_id","arc_id";"owner_id","id"]
            "entity_extension","entity",["arc_id","arc_id";"target_id","id"]
            "entity_extension","entity_extension",["arc_id","arc_id";"owner_id","owner_id";"property","property";"parent_path","path"]
            "dataset_part","dataset",["arc_id","arc_id";"parent_id","id"]
            "dataset_part","dataset",["arc_id","arc_id";"child_id","id"]
            "dataset_process","dataset",["arc_id","arc_id";"dataset_id","id"]
            "dataset_process","process",["arc_id","arc_id";"process_id","id"]
            "history","session",["arc_id","arc_id"]
            "journal","session",["arc_id","arc_id"]
            "archived_history","arc_archive",["archive_id","archive_id"]
            "archived_journal","arc_archive",["archive_id","archive_id"]
        ]
        let typedRelationships = tables |> List.map (fun (_,table) -> table,"entity",["arc_id","arc_id";"id","id"])
        let endpoints = ["input_sample_id","sample";"output_sample_id","sample";"input_data_id","data";"output_data_id","data"]
                        |> List.map (fun (column,target) -> "process",target,["arc_id","arc_id";column,"id"])
        for table,target,columns in relationships @ typedRelationships @ endpoints do
            let actual = db.Query("PRAGMA foreign_key_list(" + table + ")")
                         |> Seq.groupBy (fun row -> row.GetByName("id").AsInteger())
                         |> Seq.map (fun (_,rows) ->
                             let rows = rows |> Seq.sortBy (fun row -> row.GetByName("seq").AsInteger()) |> Seq.toList
                             rows.Head.GetByName("table").AsText(), rows |> List.map (fun row -> row.GetByName("from").AsText(),row.GetByName("to").AsText()))
            if not (actual |> Seq.exists (fun pair -> pair = (target,columns))) then
                invalidOp ("Invalid ARC repository foreign key: " + table + " -> " + target)

    let metadata (db: SqliteConnection) arcId =
        let rows = db.Query("SELECT * FROM session WHERE arc_id=$arc", parameters ["arc",text arcId])
        if rows.Count <> 1 then invalidArg "arcId" "Unknown ARC entry."
        rows[0]

    let addArc (db: SqliteConnection) arcId state folder baseline =
        Model.validate state
        db.WithTransaction(fun () ->
            execute db "INSERT INTO session VALUES($arc,$root,$folder,$state,$baseline,$saved,0,0)"
                ["arc",text arcId; "root",text state.Root; "folder",optional folder; "state",text (Codec.encodeState state)
                 "baseline",optional baseline; "saved",optional (baseline |> Option.map (fun _ -> Codec.encodeGraph state))]
            mirror db arcId state)

    let archive (db: SqliteConnection) arcId =
        let archiveId = Identifier.newId()
        let bindings = ["archive",text archiveId; "arc",text arcId]
        execute db "INSERT INTO arc_archive SELECT $archive,arc_id,root_id,folder,state,baseline,saved_graph,cursor,revision FROM session WHERE arc_id=$arc" bindings
        execute db "INSERT INTO archived_history SELECT $archive,sequence,operation_id,kind,before_state,after_state FROM history WHERE arc_id=$arc" bindings
        execute db "INSERT INTO archived_journal SELECT $archive,sequence,operation_id,kind,action,revision FROM journal WHERE arc_id=$arc" bindings
        archiveId
