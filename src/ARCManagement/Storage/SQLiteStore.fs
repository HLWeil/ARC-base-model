namespace ARCManagement.Internal

open System
open System.Collections.Generic
open ARCManagement.Helper
open PolyglotSQLite
open Fable.Core

[<AttachMembers>]
type internal Database(connection: SqliteConnection, path: string, close: unit -> unit) =
    member _.Connection = connection
    member _.Path = path
    member _.Close() = close()

/// Structural counters make performance regressions testable on all runtimes.
module internal StorageDiagnostics =
    let mutable RowsRead = 0
    let mutable CellsWritten = 0
    let mutable HistoryCellsWritten = 0
    let mutable FullStateReads = 0
    let mutable Queries = 0
    let reset () =
        RowsRead <- 0
        CellsWritten <- 0
        HistoryCellsWritten <- 0
        FullStateReads <- 0
        Queries <- 0

module internal Store =
    let openDatabase path =
        let connection = if path = ":memory:" then Sqlite.OpenInMemory() else Sqlite.OpenFile(path)
        Database(connection,path,connection.Close)
    let text value = SqlValue.Text(value)
    let optional value = value |> Option.map text |> Option.defaultWith SqlValue.Null
    let parameters values = values |> List.map (fun (name,value) -> SqlParameter(name,value))
    let execute (db: SqliteConnection) sql values = db.Execute(sql,parameters values)
    let optText (row: SqlRow) name = let value = row.GetByName(name) in if value.IsNull then None else Some(value.AsText())
    let private integer value = SqlValue.Integer(value)
    let private rows (db: SqliteConnection) arcId sql values =
        StorageDiagnostics.Queries <- StorageDiagnostics.Queries+1
        db.Query(sql,parameters (("arc",text arcId)::values))
    let private run db arcId sql values = execute db sql (("arc",text arcId)::values)

    /// The normative core SQL fixture is separate from management storage.
    let validateCore (db: SqliteConnection) =
        let errors = db.Query("SELECT dataset_id,message FROM core_validation_errors ORDER BY dataset_id,message")
        if errors.Count>0 then
            errors |> Seq.map (fun r -> r.GetByName("dataset_id").AsText()+": "+r.GetByName("message").AsText()) |> String.concat "; " |> invalidOp
        if db.Query("PRAGMA foreign_key_check").Count>0 then invalidOp "Core database contains invalid foreign keys."

    let private ddl = """
CREATE TABLE session(arc_id TEXT NOT NULL PRIMARY KEY,root_id TEXT NOT NULL,folder TEXT,baseline TEXT,saved_graph TEXT,
 cursor INTEGER NOT NULL CHECK(cursor>=0),revision INTEGER NOT NULL CHECK(typeof(revision)='integer' AND revision>=0),next_ordinal INTEGER NOT NULL CHECK(next_ordinal>=0),
 FOREIGN KEY(arc_id,root_id) REFERENCES entity(arc_id,id) DEFERRABLE INITIALLY DEFERRED);
CREATE TABLE entity(arc_id TEXT NOT NULL REFERENCES session(arc_id),id TEXT NOT NULL,type TEXT NOT NULL,supplied_id TEXT,ordinal INTEGER NOT NULL CHECK(ordinal>=0),
 PRIMARY KEY(arc_id,id),UNIQUE(arc_id,ordinal));
CREATE INDEX entity_type ON entity(arc_id,type,ordinal);
CREATE TABLE entity_property(arc_id TEXT NOT NULL,owner_id TEXT NOT NULL,property TEXT NOT NULL,storage TEXT NOT NULL CHECK(storage IN ('text','number','texts','links')),
 PRIMARY KEY(arc_id,owner_id,property),FOREIGN KEY(arc_id,owner_id) REFERENCES entity(arc_id,id) ON DELETE CASCADE);
CREATE TABLE entity_value(arc_id TEXT NOT NULL,entity_id TEXT NOT NULL,property TEXT NOT NULL,position INTEGER NOT NULL CHECK(position>=0),
 storage TEXT NOT NULL CHECK(storage IN ('text','number','texts')),value_text TEXT,value_number REAL,PRIMARY KEY(arc_id,entity_id,property,position),
 FOREIGN KEY(arc_id,entity_id,property) REFERENCES entity_property(arc_id,owner_id,property) ON DELETE CASCADE,
 CHECK((storage='number' AND value_text IS NULL AND value_number IS NOT NULL) OR (storage<>'number' AND value_text IS NOT NULL AND value_number IS NULL)));
CREATE TABLE entity_reference(arc_id TEXT NOT NULL,owner_id TEXT NOT NULL,property TEXT NOT NULL,position INTEGER NOT NULL CHECK(position>=0),target_id TEXT NOT NULL,
 PRIMARY KEY(arc_id,owner_id,property,position),FOREIGN KEY(arc_id,owner_id,property) REFERENCES entity_property(arc_id,owner_id,property) ON DELETE CASCADE,
 FOREIGN KEY(arc_id,target_id) REFERENCES entity(arc_id,id) DEFERRABLE INITIALLY DEFERRED);
CREATE INDEX reference_target ON entity_reference(arc_id,target_id,owner_id);
CREATE INDEX reference_owner ON entity_reference(arc_id,owner_id,target_id);
CREATE TABLE entity_extension(arc_id TEXT NOT NULL,owner_id TEXT NOT NULL,property TEXT NOT NULL,path TEXT NOT NULL,parent_path TEXT,position INTEGER,
 storage TEXT NOT NULL CHECK(storage IN ('text','number','bool','null','blob','object','collection')),
 value_text TEXT,value_number REAL,value_bool INTEGER CHECK(value_bool IN (0,1)),value_blob BLOB,target_id TEXT,
 PRIMARY KEY(arc_id,owner_id,property,path),FOREIGN KEY(arc_id,owner_id) REFERENCES entity(arc_id,id) ON DELETE CASCADE,
 FOREIGN KEY(arc_id,target_id) REFERENCES entity(arc_id,id) DEFERRABLE INITIALLY DEFERRED,
 FOREIGN KEY(arc_id,owner_id,property,parent_path) REFERENCES entity_extension(arc_id,owner_id,property,path) DEFERRABLE INITIALLY DEFERRED,
 CHECK((path='' AND parent_path IS NULL AND position IS NULL) OR (path<>'' AND parent_path IS NOT NULL AND position>=0)),
 CHECK((storage='text' AND value_text IS NOT NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='number' AND value_text IS NULL AND value_number IS NOT NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='bool' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NOT NULL AND value_blob IS NULL AND target_id IS NULL)
 OR (storage='blob' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NOT NULL AND target_id IS NULL)
 OR (storage='object' AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NOT NULL)
 OR (storage IN ('null','collection') AND value_text IS NULL AND value_number IS NULL AND value_bool IS NULL AND value_blob IS NULL AND target_id IS NULL)));
CREATE INDEX extension_target ON entity_extension(arc_id,target_id,owner_id) WHERE target_id IS NOT NULL;
CREATE INDEX extension_owner ON entity_extension(arc_id,owner_id,target_id) WHERE target_id IS NOT NULL;
CREATE TABLE history(arc_id TEXT NOT NULL REFERENCES session(arc_id),sequence INTEGER NOT NULL,operation_id TEXT NOT NULL,kind TEXT NOT NULL,PRIMARY KEY(arc_id,sequence));
CREATE TABLE history_change(arc_id TEXT NOT NULL,sequence INTEGER NOT NULL,position INTEGER NOT NULL,entity_id TEXT NOT NULL,domain TEXT NOT NULL,
 property TEXT NOT NULL,splice_start INTEGER,before_value TEXT,after_value TEXT,ordinal INTEGER,PRIMARY KEY(arc_id,sequence,position),
 FOREIGN KEY(arc_id,sequence) REFERENCES history(arc_id,sequence) ON DELETE CASCADE);
CREATE TABLE journal(arc_id TEXT NOT NULL REFERENCES session(arc_id),sequence INTEGER NOT NULL,operation_id TEXT NOT NULL,kind TEXT NOT NULL,
 action TEXT NOT NULL,revision INTEGER NOT NULL,PRIMARY KEY(arc_id,sequence));
CREATE TABLE arc_archive(archive_id TEXT NOT NULL PRIMARY KEY,arc_id TEXT NOT NULL,root_id TEXT NOT NULL,folder TEXT,state TEXT NOT NULL,
 baseline TEXT,saved_graph TEXT,cursor INTEGER NOT NULL,revision INTEGER NOT NULL);
CREATE TABLE archived_history(archive_id TEXT NOT NULL REFERENCES arc_archive(archive_id),sequence INTEGER NOT NULL,operation_id TEXT NOT NULL,
 kind TEXT NOT NULL,PRIMARY KEY(archive_id,sequence));
CREATE TABLE archived_history_change(archive_id TEXT NOT NULL,sequence INTEGER NOT NULL,position INTEGER NOT NULL,entity_id TEXT NOT NULL,domain TEXT NOT NULL,
 property TEXT NOT NULL,splice_start INTEGER,before_value TEXT,after_value TEXT,ordinal INTEGER,PRIMARY KEY(archive_id,sequence,position),
 FOREIGN KEY(archive_id,sequence) REFERENCES archived_history(archive_id,sequence));
CREATE TABLE archived_journal(archive_id TEXT NOT NULL REFERENCES arc_archive(archive_id),sequence INTEGER NOT NULL,operation_id TEXT NOT NULL,
 kind TEXT NOT NULL,action TEXT NOT NULL,revision INTEGER NOT NULL,PRIMARY KEY(archive_id,sequence));
"""
    let initialize (db: SqliteConnection) =
        db.WithTransaction(fun () -> for statement in ddl.Split(';') do if statement.Trim()<>"" then db.Execute(statement))
    let validateRepository (db: SqliteConnection) =
        if db.Query("PRAGMA foreign_keys").[0].Get(0).AsInteger()<>1L then invalidOp "ARC repositories require foreign-key enforcement."
        // This fresh experimental format has one supported declaration set.
        // PRAGMA foreign_key_list omits deferral and cannot verify CHECK or
        // UNIQUE constraints, so validate the stored declarations as well.
        let compact (sql:string)=sql.Replace(" ","").Replace("\r","").Replace("\n","").Replace("\t","")
        for statement in ddl.Split(';') do
            let statement=statement.Trim()
            if statement.StartsWith("CREATE TABLE ") then
                let declaration=statement.Substring("CREATE TABLE ".Length)
                let name=declaration.Substring(0,declaration.IndexOf('(')).Trim()
                let actual=db.Query("SELECT sql FROM sqlite_master WHERE type='table' AND name=$name",[SqlParameter("name",text name)])
                if actual.Count<>1 || compact(actual[0].Get(0).AsText())<>compact statement then
                    invalidOp ("Invalid ARC repository table constraints: "+name)
        let shapes = [
            "session","arc_id,root_id,folder,baseline,saved_graph,cursor,revision,next_ordinal",["arc_id"]
            "entity","arc_id,id,type,supplied_id,ordinal",["arc_id";"id"]
            "entity_property","arc_id,owner_id,property,storage",["arc_id";"owner_id";"property"]
            "entity_value","arc_id,entity_id,property,position,storage,value_text,value_number",["arc_id";"entity_id";"property";"position"]
            "entity_reference","arc_id,owner_id,property,position,target_id",["arc_id";"owner_id";"property";"position"]
            "entity_extension","arc_id,owner_id,property,path,parent_path,position,storage,value_text,value_number,value_bool,value_blob,target_id",["arc_id";"owner_id";"property";"path"]
            "history","arc_id,sequence,operation_id,kind",["arc_id";"sequence"]
            "history_change","arc_id,sequence,position,entity_id,domain,property,splice_start,before_value,after_value,ordinal",["arc_id";"sequence";"position"]
            "journal","arc_id,sequence,operation_id,kind,action,revision",["arc_id";"sequence"]
            "arc_archive","archive_id,arc_id,root_id,folder,state,baseline,saved_graph,cursor,revision",["archive_id"]
            "archived_history","archive_id,sequence,operation_id,kind",["archive_id";"sequence"]
            "archived_history_change","archive_id,sequence,position,entity_id,domain,property,splice_start,before_value,after_value,ordinal",["archive_id";"sequence";"position"]
            "archived_journal","archive_id,sequence,operation_id,kind,action,revision",["archive_id";"sequence"] ]
        for table,columns,key in shapes do
            db.Query("SELECT "+columns+" FROM "+table+" LIMIT 0") |> ignore
            let actual = db.Query("PRAGMA table_info("+table+")") |> Seq.filter (fun r -> r.GetByName("pk").AsInteger()>0L)
                         |> Seq.sortBy (fun r -> r.GetByName("pk").AsInteger()) |> Seq.map (fun r -> r.GetByName("name").AsText()) |> Seq.toList
            if actual<>key then invalidOp ("Invalid ARC repository primary key: "+table)
        for index,columns in ["reference_owner",["arc_id";"owner_id";"target_id"];"extension_owner",["arc_id";"owner_id";"target_id"]] do
            let actual=db.Query("PRAGMA index_info("+index+")") |> Seq.sortBy (fun row -> row.GetByName("seqno").AsInteger()) |> Seq.map (fun row -> row.GetByName("name").AsText()) |> Seq.toList
            if actual<>columns then invalidOp ("Invalid ARC repository traversal index: "+index)
        for table,target,columns in [
            "session","entity",["arc_id","arc_id";"root_id","id"]
            "entity","session",["arc_id","arc_id"]
            "entity_property","entity",["arc_id","arc_id";"owner_id","id"]
            "entity_value","entity_property",["arc_id","arc_id";"entity_id","owner_id";"property","property"]
            "entity_reference","entity_property",["arc_id","arc_id";"owner_id","owner_id";"property","property"]
            "entity_reference","entity",["arc_id","arc_id";"target_id","id"]
            "entity_extension","entity",["arc_id","arc_id";"owner_id","id"]
            "entity_extension","entity",["arc_id","arc_id";"target_id","id"]
            "entity_extension","entity_extension",["arc_id","arc_id";"owner_id","owner_id";"property","property";"parent_path","path"]
            "history_change","history",["arc_id","arc_id";"sequence","sequence"] ] do
            let actual = db.Query("PRAGMA foreign_key_list("+table+")") |> Seq.groupBy (fun r -> r.GetByName("id").AsInteger())
                         |> Seq.map (fun (_,rs) ->
                             let rs = rs |> Seq.sortBy (fun r -> r.GetByName("seq").AsInteger()) |> Seq.toList
                             rs.Head.GetByName("table").AsText(),rs |> List.map (fun r -> r.GetByName("from").AsText(),r.GetByName("to").AsText()))
            if not (actual |> Seq.exists ((=) (target,columns))) then invalidOp ("Invalid ARC repository foreign key: "+table)
    let metadata db arcId =
        let found = rows db arcId "SELECT arc_id,root_id,folder,baseline,saved_graph,cursor,revision,next_ordinal FROM session WHERE arc_id=$arc" []
        if found.Count<>1 then invalidArg "arcId" "Unknown ARC entry."
        found[0]
    let revision db arcId =
        let found = rows db arcId "SELECT revision FROM session WHERE arc_id=$arc" []
        if found.Count<>1 then invalidArg "arcId" "Unknown ARC entry."
        found[0].Get(0).AsInteger()
    let private metadataInteger db arcId column =
        let found = rows db arcId ("SELECT "+column+" FROM session WHERE arc_id=$arc") []
        if found.Count<>1 then invalidArg "arcId" "Unknown ARC entry."
        found[0].Get(0).AsInteger()
    let private rootId db arcId =
        let found = rows db arcId "SELECT root_id FROM session WHERE arc_id=$arc" []
        if found.Count<>1 then invalidArg "arcId" "Unknown ARC entry."
        found[0].Get(0).AsText()
    /// Caller owns the transaction; this must be its first SQL statement.
    let claimRevision db arcId expected =
        if expected=Int64.MaxValue then invalidOp "ARC revision space is exhausted."
        run db arcId "UPDATE session SET revision=revision+1 WHERE arc_id=$arc AND revision=$expected" ["expected",integer expected]
        (db.Scalar("SELECT changes()") |> Option.get).AsInteger()=1L

#if FABLE_COMPILER && !FABLE_COMPILER_PYTHON
    [<Emit("new Uint8Array($0)")>]
    let private byteBuffer (_values: byte[]) : byte[] = nativeOnly
#else
    let private byteBuffer values = values
#endif
    let private alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
    let private blobBytes (source: string) =
        let result = ResizeArray<byte>()
        let mutable buffer = 0
        let mutable bits = 0
        for c in source do
            if c<>'=' then
                buffer <- (buffer <<< 6) ||| alphabet.IndexOf(c)
                bits <- bits+6
                if bits>=8 then
                    bits <- bits-8
                    result.Add(byte ((buffer >>> bits) &&& 255))
                    buffer <- buffer &&& ((1 <<< bits)-1)
        result.ToArray() |> byteBuffer
    let private blobText (source: byte[]) =
        let result = System.Text.StringBuilder()
        for index in 0 .. 3 .. source.Length-1 do
            let a = int source[index]
            let b = if index+1<source.Length then int source[index+1] else 0
            let c = if index+2<source.Length then int source[index+2] else 0
            result.Append(alphabet[a >>> 2]) |> ignore
            result.Append(alphabet[((a &&& 3) <<< 4) ||| (b >>> 4)]) |> ignore
            result.Append(if index+1<source.Length then alphabet[((b &&& 15) <<< 2) ||| (c >>> 6)] else '=') |> ignore
            result.Append(if index+2<source.Length then alphabet[c &&& 63] else '=') |> ignore
        result.ToString()

    let private decodeRows db arcId selection bindings =
        let selected = rows db arcId ("SELECT e.id,e.type,e.supplied_id,e.ordinal FROM entity e WHERE e.arc_id=$arc AND "+selection+" ORDER BY e.ordinal") bindings
        let condition column = " AND "+column+" IN (SELECT e.id FROM entity e WHERE e.arc_id=$arc AND "+selection+")"
        let headers = rows db arcId ("SELECT p.owner_id,p.property,p.storage FROM entity_property p WHERE p.arc_id=$arc"+condition "p.owner_id") bindings
        let values = rows db arcId ("SELECT v.* FROM entity_value v WHERE v.arc_id=$arc"+condition "v.entity_id"+" ORDER BY v.position") bindings
        let references = rows db arcId ("SELECT r.* FROM entity_reference r WHERE r.arc_id=$arc"+condition "r.owner_id"+" ORDER BY r.position") bindings
        let extensions = rows db arcId ("SELECT x.* FROM entity_extension x WHERE x.arc_id=$arc"+condition "x.owner_id"+" ORDER BY x.position") bindings
        let props = Dictionary<string,Map<string,Cell>>()
        let exts = Dictionary<string,Map<string,ExtensionCell>>()
        let groupedValues = values |> Seq.groupBy (fun r -> r.GetByName("entity_id").AsText(),r.GetByName("property").AsText()) |> Map.ofSeq
        let groupedRefs = references |> Seq.groupBy (fun r -> r.GetByName("owner_id").AsText(),r.GetByName("property").AsText()) |> Map.ofSeq
        for row in headers do
            let id,key = row.GetByName("owner_id").AsText(),row.GetByName("property").AsText()
            let cells = Map.tryFind (id,key) groupedValues |> Option.defaultValue Seq.empty |> Seq.toList
            let cell =
                match row.GetByName("storage").AsText() with
                | "text" -> if cells.Length<>1 then invalidOp "Invalid stored scalar." else Text(cells.Head.GetByName("value_text").AsText())
                | "number" -> if cells.Length<>1 then invalidOp "Invalid stored scalar." else Number(cells.Head.GetByName("value_number").AsReal())
                | "texts" -> Texts(cells |> List.map (fun r -> r.GetByName("value_text").AsText()))
                | "links" -> Links(Map.tryFind (id,key) groupedRefs |> Option.defaultValue Seq.empty |> Seq.map (fun r -> r.GetByName("target_id").AsText()) |> Seq.toList)
                | _ -> invalidOp "Invalid stored property shape."
            let old = match props.TryGetValue id with true,v -> v | _ -> Map.empty
            props[id] <- Map.add key cell old
        for (id,key),nodes in extensions |> Seq.groupBy (fun r -> r.GetByName("owner_id").AsText(),r.GetByName("property").AsText()) do
            let nodes = nodes |> Seq.toList
            let byPath = nodes |> List.map (fun r -> r.GetByName("path").AsText(),r) |> Map.ofList
            let children = nodes |> Seq.choose (fun r -> optText r "parent_path" |> Option.map (fun p -> p,r)) |> Seq.groupBy fst
                           |> Seq.map (fun (p,rs) -> p,rs |> Seq.map snd |> Seq.sortBy (fun r -> r.GetByName("position").AsInteger()) |> Seq.toList) |> Map.ofSeq
            let rec value path =
                let row = byPath[path]
                match row.GetByName("storage").AsText() with
                | "text" -> ExtensionText(row.GetByName("value_text").AsText())
                | "number" -> ExtensionNumber(row.GetByName("value_number").AsReal())
                | "bool" -> ExtensionBool(row.GetByName("value_bool").AsInteger()=1L)
                | "null" -> ExtensionNull
                | "blob" -> ExtensionBlob(row.GetByName("value_blob").AsBlob() |> blobText)
                | "object" -> ExtensionObject(row.GetByName("target_id").AsText())
                | "collection" -> ExtensionCollection(Map.tryFind path children |> Option.defaultValue [] |> List.map (fun r -> value (r.GetByName("path").AsText())))
                | _ -> invalidOp "Invalid stored extension."
            let old = match exts.TryGetValue id with true,v -> v | _ -> Map.empty
            exts[id] <- Map.add key (value "") old
        let result = [for row in selected do
                        let id = row.GetByName("id").AsText()
                        yield {Id=id;Kind=row.GetByName("type").AsText();SuppliedId=optText row "supplied_id"
                               Properties=(match props.TryGetValue id with true,v -> v | _ -> Map.empty)
                               Extensions=(match exts.TryGetValue id with true,v -> v | _ -> Map.empty)}]
        StorageDiagnostics.RowsRead <- StorageDiagnostics.RowsRead+result.Length
        result
    let private idSelection (ids: seq<string>) =
        let bindings = ids |> Seq.distinct |> Seq.mapi (fun i id -> "i"+string i,text id) |> Seq.toList
        let selection = if bindings.IsEmpty then "0" else "e.id IN ("+(bindings |> List.map (fun (name,_) -> "$"+name) |> String.concat ",")+")"
        selection,bindings
    let readRows db arcId (ids: seq<string>) =
        ids |> Seq.distinct |> Seq.chunkBySize 400 |> Seq.collect (fun batch -> let selection,bindings = idSelection batch in decodeRows db arcId selection bindings) |> Seq.toList
    /// Validation needs target identity/kind, not every target's stored values.
    let readKinds db arcId (ids: seq<string>) =
        ids |> Seq.distinct |> Seq.chunkBySize 400 |> Seq.collect (fun batch ->
            let selection,bindings=idSelection batch
            rows db arcId ("SELECT e.id,e.type FROM entity e WHERE e.arc_id=$arc AND "+selection) bindings
            |> Seq.map (fun row -> row.GetByName("id").AsText(),row.GetByName("type").AsText())) |> Seq.toList
    let listIds db arcId kind =
        let condition,bindings = match kind with Some k -> " AND type=$kind",["kind",text k] | None -> "",[]
        rows db arcId ("SELECT id FROM entity WHERE arc_id=$arc"+condition+" ORDER BY ordinal") bindings |> Seq.map (fun r -> r.Get(0).AsText()) |> Seq.toList
    let readState db arcId =
        StorageDiagnostics.FullStateReads <- StorageDiagnostics.FullStateReads+1
        {Root=rootId db arcId;Entities=decodeRows db arcId "1" []}
    let loadState = readState
    let readClosure db arcId (ids: seq<string>) =
        let result = HashSet<string>()
        for batch in ids |> Seq.distinct |> Seq.chunkBySize 400 do
            let selection,bindings = idSelection batch
            let sql = "WITH RECURSIVE reachable(id) AS (SELECT e.id FROM entity e WHERE e.arc_id=$arc AND "+selection+
                      " UNION SELECT r.target_id FROM reachable q CROSS JOIN entity_reference r INDEXED BY reference_owner ON r.arc_id=$arc AND r.owner_id=q.id"+
                      " UNION SELECT x.target_id FROM reachable q CROSS JOIN entity_extension x INDEXED BY extension_owner ON x.arc_id=$arc AND x.owner_id=q.id WHERE x.target_id IS NOT NULL) SELECT id FROM reachable"
            for row in rows db arcId sql bindings do result.Add(row.Get(0).AsText()) |> ignore
        {Root=rootId db arcId;Entities=readRows db arcId result}

    let private cellKind = function Text _ -> "text" | Number _ -> "number" | Texts _ -> "texts" | Links _ -> "links"
    let private splice before after =
        let before,after = List.toArray before,List.toArray after
        let mutable prefix = 0
        while prefix<before.Length && prefix<after.Length && before[prefix]=after[prefix] do prefix <- prefix+1
        let mutable suffix = 0
        while suffix<before.Length-prefix && suffix<after.Length-prefix && before[before.Length-1-suffix]=after[after.Length-1-suffix] do suffix <- suffix+1
        prefix,(before |> Array.skip prefix |> Array.take (before.Length-prefix-suffix) |> Array.toList),(after |> Array.skip prefix |> Array.take (after.Length-prefix-suffix) |> Array.toList)
    let private writeCore db arcId id key before after =
        let scope = ["id",text id;"property",text key]
        let delete table owner = run db arcId ("DELETE FROM "+table+" WHERE arc_id=$arc AND "+owner+"=$id AND property=$property") scope
        match after with
        | None -> delete "entity_property" "owner_id"
        | Some cell ->
            run db arcId "INSERT INTO entity_property VALUES($arc,$id,$property,$storage) ON CONFLICT(arc_id,owner_id,property) DO UPDATE SET storage=$storage" (("storage",text (cellKind cell))::scope)
            let add position value =
                match value with
                | Text v | Texts [v] ->
                    run db arcId "INSERT INTO entity_value VALUES($arc,$id,$property,$position,$storage,$text,NULL) ON CONFLICT(arc_id,entity_id,property,position) DO UPDATE SET storage=$storage,value_text=$text,value_number=NULL"
                        (["position",integer (int64 position);"storage",text (cellKind value);"text",text v] @ scope)
                | Number v ->
                    run db arcId "INSERT INTO entity_value VALUES($arc,$id,$property,$position,'number',NULL,$number) ON CONFLICT(arc_id,entity_id,property,position) DO UPDATE SET storage='number',value_text=NULL,value_number=$number"
                        (["position",integer (int64 position);"number",SqlValue.Real(v)] @ scope)
                | Links [target] -> run db arcId "INSERT INTO entity_reference VALUES($arc,$id,$property,$position,$target)" (["position",integer (int64 position);"target",text target] @ scope)
                | _ -> invalidOp "Invalid cell item."
                StorageDiagnostics.CellsWritten <- StorageDiagnostics.CellsWritten+1
            let editCollection table owner wrap oldValues newValues =
                let start,removed,inserted = splice oldValues newValues
                let finish = start+removed.Length
                run db arcId ("DELETE FROM "+table+" WHERE arc_id=$arc AND "+owner+"=$id AND property=$property AND position>=$start AND position<$finish")
                    (["start",integer (int64 start);"finish",integer (int64 finish)] @ scope)
                let shift = inserted.Length-removed.Length
                if shift<>0 then
                    // A disjoint positive range avoids transient primary-key collisions.
                    let offset = int64 (oldValues.Length+newValues.Length+1)
                    run db arcId ("UPDATE "+table+" SET position=position+$offset WHERE arc_id=$arc AND "+owner+"=$id AND property=$property AND position>=$finish")
                        (["offset",integer offset;"finish",integer (int64 finish)] @ scope)
                    run db arcId ("UPDATE "+table+" SET position=position-$offset+$shift WHERE arc_id=$arc AND "+owner+"=$id AND property=$property AND position>=$threshold")
                        (["offset",integer offset;"shift",integer (int64 shift);"threshold",integer (int64 finish+offset)] @ scope)
                inserted |> List.iteri (fun index value -> add (start+index) (wrap value))
            match before,cell with
            | Some(Texts old),Texts values -> editCollection "entity_value" "entity_id" (fun v -> Texts [v]) old values
            | Some(Links old),Links values -> editCollection "entity_reference" "owner_id" (fun v -> Links [v]) old values
            | _ ->
                delete "entity_value" "entity_id"
                delete "entity_reference" "owner_id"
                match cell with
                | Texts values -> values |> List.iteri (fun index value -> add index (Texts [value]))
                | Links values -> values |> List.iteri (fun index value -> add index (Links [value]))
                | value -> add 0 value
    let rec private extensionChanges path before after = seq {
        if before<>after then
            match before,after with
            | Some(ExtensionCollection old),Some(ExtensionCollection updated) when old.Length=updated.Length ->
                for index,(a,b) in List.zip old updated |> List.indexed do
                    yield! extensionChanges (path+"/"+string index) (Some a) (Some b)
            | _ -> yield path,before,after
    }
    let private writeExtension db arcId id key before after =
        let rec insert path parent position cell =
            let storage,t,n,b,blob,target =
                match cell with
                | ExtensionText v -> "text",text v,SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Null()
                | ExtensionNumber v -> "number",SqlValue.Null(),SqlValue.Real(v),SqlValue.Null(),SqlValue.Null(),SqlValue.Null()
                | ExtensionBool v -> "bool",SqlValue.Null(),SqlValue.Null(),integer (if v then 1L else 0L),SqlValue.Null(),SqlValue.Null()
                | ExtensionBlob v -> "blob",SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Blob(blobBytes v),SqlValue.Null()
                | ExtensionObject v -> "object",SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),text v
                | ExtensionNull -> "null",SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Null()
                | ExtensionCollection _ -> "collection",SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Null(),SqlValue.Null()
            run db arcId "INSERT INTO entity_extension VALUES($arc,$id,$key,$path,$parent,$position,$storage,$text,$number,$bool,$blob,$target)"
                ["id",text id;"key",text key;"path",text path;"parent",optional parent;"position",position |> Option.map (int64 >> integer) |> Option.defaultWith SqlValue.Null
                 "storage",text storage;"text",t;"number",n;"bool",b;"blob",blob;"target",target]
            StorageDiagnostics.CellsWritten <- StorageDiagnostics.CellsWritten+1
            match cell with ExtensionCollection values -> values |> List.iteri (fun index value -> insert (path+"/"+string index) (Some path) (Some index) value) | _ -> ()
        for path,_,replacement in extensionChanges "" before after do
            let scope=["id",text id;"key",text key]
            if path="" then run db arcId "DELETE FROM entity_extension WHERE arc_id=$arc AND owner_id=$id AND property=$key" scope
            else
                run db arcId "DELETE FROM entity_extension WHERE arc_id=$arc AND owner_id=$id AND property=$key AND (path=$path OR substr(path,1,$length)=$prefix)"
                    (scope @ ["path",text path;"length",integer(int64(path.Length+1));"prefix",text(path+"/")])
            let parent,position=
                if path="" then None,None
                else let slash=path.LastIndexOf('/') in Some(path.Substring(0,slash)),Some(Int32.Parse(path.Substring(slash+1)))
            replacement |> Option.iter (insert path parent position)
    let private rowMap (state: State) = state.Entities |> List.map (fun row -> row.Id,row) |> Map.ofList
    let private changedKeys before after = Set.union (before |> Map.toSeq |> Seq.map fst |> Set.ofSeq) (after |> Map.toSeq |> Seq.map fst |> Set.ofSeq)
    /// The two States may contain only affected entities. All other SQL rows remain untouched.
    let writeDelta db arcId (before: State) (next: State) =
        let oldRows,newRows = rowMap before,rowMap next
        let mutable ordinal = metadataInteger db arcId "next_ordinal"
        for row in next.Entities do
            match Map.tryFind row.Id oldRows with
            | None ->
                run db arcId "INSERT INTO entity VALUES($arc,$id,$kind,$supplied,$ordinal)" ["id",text row.Id;"kind",text row.Kind;"supplied",optional row.SuppliedId;"ordinal",integer ordinal]
                ordinal <- ordinal+1L
            | Some old when old.Kind<>row.Kind -> invalidOp "An existing entity ID cannot change type."
            | Some old when old.SuppliedId<>row.SuppliedId -> run db arcId "UPDATE entity SET supplied_id=$supplied WHERE arc_id=$arc AND id=$id" ["id",text row.Id;"supplied",optional row.SuppliedId]
            | _ -> ()
        for row in next.Entities do
            let old = Map.tryFind row.Id oldRows
            let oldProps = old |> Option.map (fun r -> r.Properties) |> Option.defaultValue Map.empty
            let oldExts = old |> Option.map (fun r -> r.Extensions) |> Option.defaultValue Map.empty
            for key in changedKeys oldProps row.Properties do
                let a,b = Map.tryFind key oldProps,Map.tryFind key row.Properties
                if a<>b then writeCore db arcId row.Id key a b
            for key in changedKeys oldExts row.Extensions do
                let a,b = Map.tryFind key oldExts,Map.tryFind key row.Extensions
                if a<>b then writeExtension db arcId row.Id key a b
        for KeyValue(id,_) in oldRows do
            if not (Map.containsKey id newRows) then
                run db arcId "DELETE FROM entity_property WHERE arc_id=$arc AND owner_id=$id" ["id",text id]
                run db arcId "DELETE FROM entity_extension WHERE arc_id=$arc AND owner_id=$id" ["id",text id]
        for KeyValue(id,_) in oldRows do if not (Map.containsKey id newRows) then run db arcId "DELETE FROM entity WHERE arc_id=$arc AND id=$id" ["id",text id]
        run db arcId "UPDATE session SET next_ordinal=$ordinal WHERE arc_id=$arc" ["ordinal",integer ordinal]

    let private encodeCell value =
        {Id="cell";Kind="Entity";SuppliedId=None;Properties=Map.ofList ["value",value];Extensions=Map.empty} |> Codec.rowElement |> Codec.write
    let private decodeCell source = Codec.readRow (YAMLicious.Reader.read source) |> fun row -> row.Properties["value"]
    let private encodeMetadata row = {row with Properties=Map.empty;Extensions=Map.empty} |> Codec.rowElement |> Codec.write
    let private changes db arcId (before: State) (next: State) =
        let oldRows,newRows = rowMap before,rowMap next
        let changes = ResizeArray<StorageChange>()
        let mutable nextOrdinal = metadataInteger db arcId "next_ordinal"
        let ordinal id =
            let found = rows db arcId "SELECT ordinal FROM entity WHERE arc_id=$arc AND id=$id" ["id",text id]
            if found.Count=1 then Some(found[0].Get(0).AsInteger())
            else let value = nextOrdinal in nextOrdinal <- nextOrdinal+1L; Some value
        let add id domain key start a b position = changes.Add {EntityId=id;Domain=domain;Property=key;Start=start;Before=a;After=b;Ordinal=position}
        let allIds = Seq.append (before.Entities |> Seq.map (fun r -> r.Id)) (next.Entities |> Seq.map (fun r -> r.Id)) |> Seq.distinct
        for id in allIds do
            let a,b = Map.tryFind id oldRows,Map.tryFind id newRows
            if a.IsNone || b.IsNone || (a.Value.Kind,a.Value.SuppliedId)<>(b.Value.Kind,b.Value.SuppliedId) then
                add id "entity" "" None (a |> Option.map encodeMetadata) (b |> Option.map encodeMetadata) (ordinal id)
            let oldProps = a |> Option.map (fun r -> r.Properties) |> Option.defaultValue Map.empty
            let newProps = b |> Option.map (fun r -> r.Properties) |> Option.defaultValue Map.empty
            for key in changedKeys oldProps newProps do
                let av,bv = Map.tryFind key oldProps,Map.tryFind key newProps
                if av<>bv then
                    match av,bv with
                    | Some(Texts old),Some(Texts updated) -> let start,removed,inserted = splice old updated in add id "core" key (Some start) (Some(encodeCell(Texts removed))) (Some(encodeCell(Texts inserted))) None
                    | Some(Links old),Some(Links updated) -> let start,removed,inserted = splice old updated in add id "core" key (Some start) (Some(encodeCell(Links removed))) (Some(encodeCell(Links inserted))) None
                    | _ -> add id "core" key None (av |> Option.map encodeCell) (bv |> Option.map encodeCell) None
            let oldExts = a |> Option.map (fun r -> r.Extensions) |> Option.defaultValue Map.empty
            let newExts = b |> Option.map (fun r -> r.Extensions) |> Option.defaultValue Map.empty
            for key in changedKeys oldExts newExts do
                let av,bv = Map.tryFind key oldExts,Map.tryFind key newExts
                for path,a,b in extensionChanges "" av bv do
                    add id ("extension"+path) key None (a |> Option.map (Codec.extensionElement >> Codec.write)) (b |> Option.map (Codec.extensionElement >> Codec.write)) None
        List.ofSeq changes
    /// Record before writing data so deleted entities' ordinals remain available.
    let recordHistory db arcId sequence operationId operationKind before next =
        let delta = changes db arcId before next
        run db arcId "DELETE FROM history WHERE arc_id=$arc AND sequence>=$sequence" ["sequence",integer (int64 sequence)]
        run db arcId "INSERT INTO history VALUES($arc,$sequence,$id,$kind)" ["sequence",integer (int64 sequence);"id",text operationId;"kind",text operationKind]
        for position,change in List.indexed delta do
            run db arcId "INSERT INTO history_change VALUES($arc,$sequence,$position,$id,$domain,$property,$start,$before,$after,$ordinal)"
                ["sequence",integer (int64 sequence);"position",integer (int64 position);"id",text change.EntityId;"domain",text change.Domain;"property",text change.Property
                 "start",change.Start |> Option.map (int64 >> integer) |> Option.defaultWith SqlValue.Null;"before",optional change.Before;"after",optional change.After
                 "ordinal",change.Ordinal |> Option.map integer |> Option.defaultWith SqlValue.Null]
            StorageDiagnostics.HistoryCellsWritten <- StorageDiagnostics.HistoryCellsWritten+1
    let commit db arcId before next sequence operationId operationKind =
        recordHistory db arcId sequence operationId operationKind before next
        writeDelta db arcId before next
    let commitRows = commit
    let private historyChanges db arcId sequence = rows db arcId "SELECT * FROM history_change WHERE arc_id=$arc AND sequence=$sequence ORDER BY position" ["sequence",integer (int64 sequence)]
    let historyState db arcId sequence redo (current: State) =
        let operation = rows db arcId "SELECT operation_id,kind FROM history WHERE arc_id=$arc AND sequence=$sequence" ["sequence",integer (int64 sequence)]
        if operation.Count<>1 then invalidOp (if redo then "Nothing to redo." else "Nothing to undo.")
        let mutable updated = rowMap current
        let delta = historyChanges db arcId sequence |> Seq.toList
        let selected row = optText row (if redo then "after_value" else "before_value")
        for row in delta do
            if row.GetByName("domain").AsText()="entity" then
                let id = row.GetByName("entity_id").AsText()
                match selected row with
                | None -> updated <- Map.remove id updated
                | Some source ->
                    let metadata = Codec.readRow (YAMLicious.Reader.read source)
                    let value = match Map.tryFind id updated with Some old -> {metadata with Properties=old.Properties;Extensions=old.Extensions} | None -> metadata
                    updated <- Map.add id value updated
        for row in delta do
            let id,key,domain = row.GetByName("entity_id").AsText(),row.GetByName("property").AsText(),row.GetByName("domain").AsText()
            if domain<>"entity" && Map.containsKey id updated then
                let value = updated[id]
                let replacement = selected row
                if domain="core" then
                    let cell = replacement |> Option.map decodeCell
                    let position = row.GetByName("splice_start")
                    let cell =
                        if position.IsNull then cell else
                        let start = int (position.AsInteger())
                        let removed = optText row (if redo then "before_value" else "after_value") |> Option.map decodeCell |> Option.get
                        let replace old removed inserted = List.take start old @ inserted @ List.skip (start+List.length removed) old
                        match value.Properties[key],removed,cell with
                        | Texts old,Texts removed,Some(Texts inserted) -> Some(Texts(replace old removed inserted))
                        | Links old,Links removed,Some(Links inserted) -> Some(Links(replace old removed inserted))
                        | _ -> invalidOp "Invalid stored collection splice."
                    updated <- Map.add id {value with Properties=(match cell with Some v -> Map.add key v value.Properties | None -> Map.remove key value.Properties)} updated
                elif domain="extension" || domain.StartsWith("extension/") then
                    let replacement = replacement |> Option.map (YAMLicious.Reader.read >> Codec.readExtension)
                    let path=domain.Substring("extension".Length).Split('/') |> Array.filter ((<>) "") |> Array.map Int32.Parse |> Array.toList
                    let rec replace path current =
                        match path,current with
                        | [],_ -> replacement
                        | index::tail,Some(ExtensionCollection values) when index>=0 && index<values.Length ->
                            values |> List.mapi (fun position item ->
                                if position=index then replace tail (Some item) |> Option.defaultWith (fun () -> invalidOp "An extension child cannot be absent.")
                                else item) |> ExtensionCollection |> Some
                        | _ -> invalidOp "Invalid stored extension path."
                    let extension = replace path (Map.tryFind key value.Extensions)
                    updated <- Map.add id {value with Extensions=(match extension with Some v -> Map.add key v value.Extensions | None -> Map.remove key value.Extensions)} updated
        let order = Dictionary<string,int64>()
        // Only current/affected identities are needed, including historical insertion ordinals.
        for batch in current.Entities |> Seq.map (fun r -> r.Id) |> Seq.chunkBySize 400 do
            let selection,bindings = idSelection batch
            for row in rows db arcId ("SELECT e.id,e.ordinal FROM entity e WHERE e.arc_id=$arc AND "+selection) bindings do order[row.GetByName("id").AsText()] <- row.GetByName("ordinal").AsInteger()
        for row in delta do if not (row.GetByName("ordinal").IsNull) then order[row.GetByName("entity_id").AsText()] <- row.GetByName("ordinal").AsInteger()
        let entities = updated |> Map.toSeq |> Seq.map snd |> Seq.sortBy (fun row -> match order.TryGetValue row.Id with true,v -> v | _ -> Int64.MaxValue) |> Seq.toList
        {current with Entities=entities},operation[0].GetByName("operation_id").AsText(),operation[0].GetByName("kind").AsText()
    let historyRows db arcId sequence redo =
        let ids = historyChanges db arcId sequence |> Seq.map (fun r -> r.GetByName("entity_id").AsText()) |> Seq.distinct |> Seq.toList
        let before = {Root=rootId db arcId;Entities=readRows db arcId ids}
        let after,operationId,operationKind = historyState db arcId sequence redo before
        before,after,operationId,operationKind
    let restoreHistoryOrder db arcId sequence redo =
        let key = if redo then "after_value" else "before_value"
        for row in historyChanges db arcId sequence do
            if row.GetByName("domain").AsText()="entity" && not (row.GetByName(key).IsNull) && not (row.GetByName("ordinal").IsNull) then
                run db arcId "UPDATE entity SET ordinal=$ordinal WHERE arc_id=$arc AND id=$id" ["ordinal",integer (row.GetByName("ordinal").AsInteger());"id",text (row.GetByName("entity_id").AsText())]
    /// Bulk recovery replaces the entry, including entity kinds and ordinal order.
    /// The caller updates its deferred root foreign key in the same transaction.
    let mirror db arcId state =
        run db arcId "DELETE FROM entity_property WHERE arc_id=$arc" []
        run db arcId "DELETE FROM entity_extension WHERE arc_id=$arc" []
        run db arcId "DELETE FROM entity WHERE arc_id=$arc" []
        run db arcId "UPDATE session SET next_ordinal=0 WHERE arc_id=$arc" []
        writeDelta db arcId {Root=state.Root;Entities=[]} state
    let addArc (db: SqliteConnection) arcId state folder baseline =
        Model.validate state
        db.WithTransaction(fun () ->
            execute db "INSERT INTO session VALUES($arc,$root,$folder,$baseline,$saved,0,0,0)"
                ["arc",text arcId;"root",text state.Root;"folder",optional folder;"baseline",optional baseline;"saved",optional (baseline |> Option.map (fun _ -> Codec.encodeGraph state))]
            writeDelta db arcId {Root=state.Root;Entities=[]} state)
    let archive db arcId =
        let archiveId = Identifier.newId()
        let state = readState db arcId |> Codec.encodeState
        execute db "INSERT INTO arc_archive SELECT $archive,arc_id,root_id,folder,$state,baseline,saved_graph,cursor,revision FROM session WHERE arc_id=$arc"
            ["archive",text archiveId;"arc",text arcId;"state",text state]
        for target,source,columns in [
            "archived_history","history","sequence,operation_id,kind"
            "archived_history_change","history_change","sequence,position,entity_id,domain,property,splice_start,before_value,after_value,ordinal"
            "archived_journal","journal","sequence,operation_id,kind,action,revision"] do
            execute db ("INSERT INTO "+target+" SELECT $archive,"+columns+" FROM "+source+" WHERE arc_id=$arc") ["archive",text archiveId;"arc",text arcId]
        archiveId
