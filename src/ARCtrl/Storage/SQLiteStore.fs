namespace ARCtrl.Internal

open PolyglotSQLite

type internal Database = { Connection: SqliteConnection; Close: unit -> unit }
module internal Store =
    let openDatabase path =
#if !FABLE_COMPILER
        // Own an unpooled handle so archiving a closed database works on Windows.
        let builder = Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
        builder.DataSource <- path
        builder.Pooling <- false
        let native = new Microsoft.Data.Sqlite.SqliteConnection(builder.ToString())
        try
            native.Open()
            let connection = Sqlite.WrapConnection(native)
            { Connection = connection; Close = fun () -> try connection.Close() finally native.Dispose() }
        with _ -> native.Dispose(); reraise()
#else
        let connection = Sqlite.OpenFile(path)
        { Connection = connection; Close = connection.Close }
#endif
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
 owner_id TEXT NOT NULL REFERENCES entity(id), property TEXT NOT NULL, path TEXT NOT NULL,
 parent_path TEXT, position INTEGER, storage TEXT NOT NULL CHECK(storage IN ('text','number','bool','null','blob','object','collection')),
 value_text TEXT, value_number REAL, value_bool INTEGER CHECK(value_bool IN (0,1)), value_blob BLOB,
 target_id TEXT REFERENCES entity(id), PRIMARY KEY(owner_id,property,path),
 FOREIGN KEY(owner_id,property,parent_path) REFERENCES entity_extension(owner_id,property,path) DEFERRABLE INITIALLY DEFERRED,
 CHECK((path='' AND parent_path IS NULL AND position IS NULL) OR (path<>'' AND parent_path IS NOT NULL AND position>=0)),
 CHECK((storage='text' AND value_text IS NOT NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='number' AND value_text IS NULL AND value_number IS NOT NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='bool' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NOT NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='blob' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NOT NULL AND target_id IS NULL)
 OR (storage='object' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NOT NULL)
 OR (storage IN ('null','collection') AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)));
CREATE INDEX IF NOT EXISTS extension_target ON entity_extension(target_id);
"""
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
        result.ToArray()
    let mirror (db: SqliteConnection) state =
        for table in ["entity_extension"; "entity_reference"; "entity_value"; "dataset_part"; "dataset_process"] do db.Execute("DELETE FROM " + table)
        for _,table in tables do db.Execute("DELETE FROM " + table)
        db.Execute("DELETE FROM entity")
        for row in state.Entities do
            execute db "INSERT INTO entity(id,type) VALUES($id,$type)" ["id",text row.Id; "type",text row.Kind]
        for row in state.Entities do
            let table = tables |> List.tryFind (fun (kind,_) -> kind = row.Kind) |> Option.map snd
            let payload = Codec.rowElement row |> Codec.write
            if row.Kind = "Process" then
                let endpoint key kind = Model.links key row |> List.tryHead |> Option.filter (fun id -> state.Entities |> List.exists (fun r -> r.Id = id && r.Kind = kind))
                execute db "INSERT INTO process(id,name,input_sample_id,output_sample_id,input_data_id,output_data_id,payload) VALUES($id,$name,$input,$output,$inputData,$outputData,$payload)"
                    ["id",text row.Id; "name",optional(Model.text "name" row); "input",optional(endpoint "input" "Sample"); "output",optional(endpoint "output" "Sample"); "inputData",optional(endpoint "input" "Data"); "outputData",optional(endpoint "output" "Data"); "payload",text payload]
            elif row.Kind = "Sample" then
                execute db "INSERT INTO sample(id,name,payload) VALUES($id,$name,$payload)" ["id",text row.Id; "name",optional(Model.text "name" row); "payload",text payload]
            elif table.IsSome then execute db ("INSERT INTO " + table.Value + "(id,payload) VALUES($id,$payload)") ["id",text row.Id; "payload",text payload]
        for row in state.Entities do
            for key,cell in Map.toList row.Properties do
                let value position storage stringValue numberValue =
                    execute db "INSERT INTO entity_value VALUES($id,$property,$position,$storage,$text,$number)"
                        ["id",text row.Id; "property",text key; "position",SqlValue.Integer(int64 position); "storage",text storage; "text",optional stringValue; "number",numberValue |> Option.map SqlValue.Real |> Option.defaultWith SqlValue.Null]
                match cell with
                | Text v -> value 0 "text" (Some v) None
                | Number v -> value 0 "number" None (Some v)
                | Texts vs -> for position,v in List.indexed vs do value position "texts" (Some v) None
                | Links vs ->
                    for position,id in List.indexed vs do
                        execute db "INSERT INTO entity_reference VALUES($owner,$property,$position,$target)"
                            ["owner",text row.Id; "property",text key; "position",SqlValue.Integer(int64 position); "target",text id]
                        if row.Kind = "Dataset" && (key = "hasParts" || key = "processes") then
                            let table, owner, target = if key = "hasParts" then "dataset_part","parent_id","child_id" else "dataset_process","dataset_id","process_id"
                            execute db ("INSERT INTO " + table + "(" + owner + ",position," + target + ") VALUES($owner,$position,$target)")
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
                    execute db "INSERT INTO entity_extension VALUES($owner,$key,$path,$parent,$position,$storage,$text,$number,$bool,$blob,$target)"
                        ["owner", text row.Id; "key", text key; "path", text path; "parent", optional parent
                         "position", position |> Option.map (int64 >> SqlValue.Integer) |> Option.defaultWith SqlValue.Null
                         "storage", text storage; "text", textValue; "number", numberValue; "bool", boolValue; "blob", blobValue; "target", target]
                    match cell with
                    | ExtensionCollection values ->
                        for index, value in List.indexed values do insert (path + "/" + string index) (Some path) (Some index) value
                    | _ -> ()
                insert "" None None cell
    let initialize (db: SqliteConnection) state baseline =
        let entityTables = tables |> List.map (fun (_,table) ->
            if table = "process" then "CREATE TABLE process(id TEXT NOT NULL PRIMARY KEY REFERENCES entity(id), name TEXT NOT NULL, input_sample_id TEXT REFERENCES sample(id) DEFERRABLE INITIALLY DEFERRED, output_sample_id TEXT REFERENCES sample(id) DEFERRABLE INITIALLY DEFERRED, input_data_id TEXT REFERENCES data(id) DEFERRABLE INITIALLY DEFERRED, output_data_id TEXT REFERENCES data(id) DEFERRABLE INITIALLY DEFERRED, payload TEXT NOT NULL);"
            elif table = "sample" then "CREATE TABLE sample(id TEXT NOT NULL PRIMARY KEY REFERENCES entity(id), name TEXT NOT NULL, payload TEXT NOT NULL);"
            else "CREATE TABLE " + table + "(id TEXT NOT NULL PRIMARY KEY REFERENCES entity(id), payload TEXT NOT NULL);") |> String.concat "\n"
        db.ExecuteScript("""
CREATE TABLE session(singleton INTEGER PRIMARY KEY CHECK(singleton=1), schema_version INTEGER NOT NULL,
 state TEXT NOT NULL, baseline TEXT, saved_graph TEXT, cursor INTEGER NOT NULL, revision INTEGER NOT NULL);
CREATE TABLE entity(id TEXT NOT NULL PRIMARY KEY, type TEXT NOT NULL);
""" + entityTables + """
CREATE TABLE entity_value(entity_id TEXT NOT NULL REFERENCES entity(id), property TEXT NOT NULL,
 position INTEGER NOT NULL CHECK(position>=0), storage TEXT NOT NULL CHECK(storage IN ('text','number','texts')),
 value_text TEXT, value_number REAL, PRIMARY KEY(entity_id,property,position),
 CHECK((storage='number' AND value_text IS NULL AND value_number IS NOT NULL) OR (storage<>'number' AND value_text IS NOT NULL AND value_number IS NULL)));
CREATE TABLE entity_reference(owner_id TEXT NOT NULL REFERENCES entity(id), property TEXT NOT NULL,
 position INTEGER NOT NULL CHECK(position>=0), target_id TEXT NOT NULL REFERENCES entity(id), PRIMARY KEY(owner_id,property,position));
CREATE INDEX reference_target ON entity_reference(target_id);
CREATE TABLE dataset_part(parent_id TEXT NOT NULL REFERENCES dataset(id), position INTEGER NOT NULL,
 child_id TEXT NOT NULL REFERENCES dataset(id), PRIMARY KEY(parent_id,position));
CREATE TABLE dataset_process(dataset_id TEXT NOT NULL REFERENCES dataset(id), position INTEGER NOT NULL,
 process_id TEXT NOT NULL REFERENCES process(id), PRIMARY KEY(dataset_id,position));
CREATE TABLE history(sequence INTEGER PRIMARY KEY, operation_id TEXT NOT NULL, kind TEXT NOT NULL,
 before_state TEXT NOT NULL, after_state TEXT NOT NULL);
CREATE TABLE journal(sequence INTEGER PRIMARY KEY, operation_id TEXT NOT NULL, kind TEXT NOT NULL,
 action TEXT NOT NULL, revision INTEGER NOT NULL);
        """)
        db.ExecuteScript(extensionDDL)
        db.WithTransaction(fun () ->
            mirror db state
            execute db "INSERT INTO session VALUES(1,3,$state,$baseline,$saved,0,0)"
                ["state", text (Codec.encodeState state); "baseline", optional baseline
                 "saved", optional (baseline |> Option.map (fun _ -> Codec.encodeGraph state))])
    let metadata (db: SqliteConnection) =
        let rows = db.Query("SELECT * FROM session WHERE singleton=1")
        if rows.Count <> 1 then invalidOp "Invalid session metadata."
        let version = rows[0].GetByName("schema_version").AsInteger()
        if version = 2L then
            db.WithTransaction(fun () ->
                for statement in extensionDDL.Split(';') do
                    if statement.Trim() <> "" then db.Execute(statement)
                db.Execute("UPDATE session SET schema_version=3 WHERE singleton=1"))
        elif version <> 3L then invalidOp "Unsupported session database. Version 1 requires explicit reload from persistent IO."
        rows[0]
