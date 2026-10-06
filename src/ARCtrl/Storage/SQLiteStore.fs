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
    let mirror (db: SqliteConnection) state =
        for table in ["dataset_part"; "dataset_process"; "process"; "sample"; "dataset"] do db.Execute("DELETE FROM " + table)
        for row in state.Datasets do
            execute db "INSERT INTO dataset(id,payload) VALUES($id,$payload)" ["id", text row.Id; "payload", text (Codec.mapping (Codec.datasetFields row) |> Codec.write)]
        for row in state.Samples do
            execute db "INSERT INTO sample(id,name,payload) VALUES($id,$name,$payload)" ["id", text row.Id; "name", text row.Name; "payload", text (Codec.sampleElement row |> Codec.write)]
        for row in state.Processes do
            execute db "INSERT INTO process(id,name,input_sample_id,output_sample_id,payload) VALUES($id,$name,$input,$output,$payload)"
                ["id", text row.Id; "name", text row.Name; "input", optional row.Input; "output", optional row.Output; "payload", text (Codec.mapping (Codec.processFields row) |> Codec.write)]
        for row in state.Datasets do
            for position, child in row.Parts |> List.indexed do
                execute db "INSERT INTO dataset_part(parent_id,position,child_id) VALUES($parent,$position,$child)"
                    ["parent", text row.Id; "position", SqlValue.Integer(int64 position); "child", text child]
            for position, proc in row.Processes |> List.indexed do
                execute db "INSERT INTO dataset_process(dataset_id,position,process_id) VALUES($dataset,$position,$process)"
                    ["dataset", text row.Id; "position", SqlValue.Integer(int64 position); "process", text proc]
    let initialize (db: SqliteConnection) state baseline =
        db.ExecuteScript("""
CREATE TABLE session(singleton INTEGER PRIMARY KEY CHECK(singleton=1), schema_version INTEGER NOT NULL,
 state TEXT NOT NULL, baseline TEXT, saved_graph TEXT, cursor INTEGER NOT NULL, revision INTEGER NOT NULL);
CREATE TABLE dataset(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
CREATE TABLE sample(id TEXT PRIMARY KEY, name TEXT NOT NULL, payload TEXT NOT NULL);
CREATE TABLE process(id TEXT PRIMARY KEY, name TEXT NOT NULL, input_sample_id TEXT REFERENCES sample(id),
 output_sample_id TEXT REFERENCES sample(id), payload TEXT NOT NULL);
CREATE TABLE dataset_part(parent_id TEXT NOT NULL REFERENCES dataset(id), position INTEGER NOT NULL,
 child_id TEXT NOT NULL UNIQUE REFERENCES dataset(id), PRIMARY KEY(parent_id,position));
CREATE TABLE dataset_process(dataset_id TEXT NOT NULL REFERENCES dataset(id), position INTEGER NOT NULL,
 process_id TEXT NOT NULL UNIQUE REFERENCES process(id), PRIMARY KEY(dataset_id,position));
CREATE TABLE history(sequence INTEGER PRIMARY KEY, operation_id TEXT NOT NULL, kind TEXT NOT NULL,
 before_state TEXT NOT NULL, after_state TEXT NOT NULL);
CREATE TABLE journal(sequence INTEGER PRIMARY KEY, operation_id TEXT NOT NULL, kind TEXT NOT NULL,
 action TEXT NOT NULL, revision INTEGER NOT NULL);
        """)
        db.WithTransaction(fun () ->
            mirror db state
            execute db "INSERT INTO session VALUES(1,1,$state,$baseline,$saved,0,0)"
                ["state", text (Codec.encodeState state); "baseline", optional baseline
                 "saved", optional (baseline |> Option.map (fun _ -> Codec.encodeGraph state))])
    let metadata (db: SqliteConnection) =
        let rows = db.Query("SELECT * FROM session WHERE singleton=1")
        if rows.Count <> 1 || rows[0].GetByName("schema_version").AsInteger() <> 1L then invalidOp "Unsupported session database."
        rows[0]
