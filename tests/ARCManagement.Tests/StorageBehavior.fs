module ARCManagement.Tests.StorageBehavior

open Fable.Pyxpecto
open ARCManagement.Internal
open PolyglotSQLite

let private sample id name = {Id=id;Kind="Sample";SuppliedId=Some id;Properties=Map.ofList ["name",Text name];Extensions=Map.empty}
let private root extensions =
    {Id="root";Kind="Dataset";SuppliedId=Some "root";Properties=Map.ofList ["conformsTo",Texts ["process-provenance"];"identifiers",Texts ["root"];"additionalTypes",Texts []];Extensions=extensions}
let private state entities = {Root="root";Entities=entities}
let private withStore initial action =
    let database = Store.openDatabase ":memory:"
    try
        Store.initialize database.Connection
        Store.addArc database.Connection "arc" initial None None
        action database.Connection
    finally database.Close()
let private save (db: SqliteConnection) expected before after sequence =
    db.WithTransaction(fun () ->
        Expect.isTrue (Store.claimRevision db "arc" expected) "Expected revision claimed"
        Store.commit db "arc" before after sequence ("op-"+string sequence) "test.change")
let private count (db: SqliteConnection) sql = (db.Scalar(sql) |> Option.get).AsInteger()
let private undo (db: SqliteConnection) sequence redo =
    db.WithTransaction(fun () ->
        let before,after,_,_ = Store.historyRows db "arc" sequence redo
        Store.writeDelta db "arc" before after
        Store.restoreHistoryOrder db "arc" sequence redo)

let tests = testList "Normalized management storage" [
    testCase "fresh schema has one normalized authority and no version or live state snapshot" (fun _ ->
        let initial = state [root Map.empty]
        withStore initial (fun db ->
            Store.validateRepository db
            Expect.equal (Store.readState db "arc") initial "Normalized round trip includes empty collection headers"
            let columns = db.Query("PRAGMA table_info(session)") |> Seq.map (fun row -> row.GetByName("name").AsText()) |> Seq.toList
            Expect.isFalse (List.contains "state" columns) "No full current snapshot column"
            Expect.equal (count db "SELECT count(*) FROM sqlite_master WHERE name='repository'") 0L "No schema version table"))
    testCase "scalar storage and history writes stay bounded beside a large collection" (fun _ ->
        let values = [for index in 0..1999 -> "value-"+string index]
        let original = {root Map.empty with Properties=Map.add "additionalTypes" (Texts values) (root Map.empty).Properties}
        let initial = state [original]
        withStore initial (fun db ->
            let edited = {original with Properties=Map.add "title" (Text "Updated") original.Properties}
            StorageDiagnostics.reset()
            save db 0L initial (state [edited]) 1
            Expect.equal StorageDiagnostics.CellsWritten 1 "Only the changed scalar written"
            Expect.equal StorageDiagnostics.HistoryCellsWritten 1 "One property delta recorded"
            Expect.equal StorageDiagnostics.FullStateReads 0 "No full registry read during a focused edit"
            Expect.equal (count db "SELECT count(*) FROM entity_value WHERE property='additionalTypes'") 2000L "Unchanged collection retained"
            Expect.isTrue (count db "SELECT coalesce(length(before_value),0)+coalesce(length(after_value),0) FROM history_change WHERE property='title'" < 1000L) "History excludes unchanged collection"
            Expect.equal (Store.readState db "arc") (state [edited]) "Current values persisted"
            undo db 1 false
            Expect.equal (Store.readState db "arc") initial "Inverse scalar delta"
            undo db 1 true
            Expect.equal (Store.readState db "arc") (state [edited]) "Forward scalar delta"))
    testCase "opening rejects declarations with missing constraints" (fun _ ->
        withStore (state [root Map.empty]) (fun db ->
            let forged=Store.openDatabase ":memory:"
            try
                let declarations=db.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index') ORDER BY rowid")
                for row in declarations do
                    forged.Connection.Execute(row.Get(0).AsText().Replace("CHECK(cursor>=0)",""))
                Expect.throws (fun () -> Store.validateRepository forged.Connection) "Matching columns and keys do not hide a missing CHECK constraint"
            finally forged.Close()))
    testCase "collection splice preserves repeated values and shifts safely in both directions" (fun _ ->
        let original = {root Map.empty with Properties=Map.add "additionalTypes" (Texts ["a";"b";"b";"c"]) (root Map.empty).Properties}
        let initial = state [original]
        withStore initial (fun db ->
            let append = {original with Properties=Map.add "additionalTypes" (Texts ["a";"b";"b";"c";"c"]) original.Properties}
            StorageDiagnostics.reset()
            save db 0L initial (state [append]) 1
            Expect.equal StorageDiagnostics.CellsWritten 1 "Appending writes one item"
            Expect.equal StorageDiagnostics.HistoryCellsWritten 1 "One splice history item"
            let replacement = {append with Properties=Map.add "additionalTypes" (Texts ["a";"x";"c"]) append.Properties}
            save db 1L (state [append]) (state [replacement]) 2
            Expect.equal (Store.readState db "arc") (state [replacement]) "Middle deletion/insertion retains order"
            undo db 2 false
            Expect.equal (Store.readState db "arc") (state [append]) "Undo restores duplicate items"
            undo db 2 true
            Expect.equal (Store.readState db "arc") (state [replacement]) "Redo reapplies splice"))
    testCase "selective closure follows core and extension cycles without unrelated rows" (fun _ ->
        let a = {sample "a" "A" with Extensions=Map.ofList ["back",ExtensionObject "root";"blob",ExtensionBlob "AAH/"]}
        let r = root (Map.ofList ["items",ExtensionCollection [ExtensionNull;ExtensionBool false;ExtensionCollection [];ExtensionObject "a"]])
        let initial = state [r;a;sample "outside" "Outside"]
        withStore initial (fun db ->
            let selected = Store.readClosure db "arc" ["root"]
            Expect.equal (selected.Entities |> List.map (fun row -> row.Id) |> Set.ofList) (Set.ofList ["root";"a"]) "Cycle terminates and unrelated entity stays unloaded"
            Expect.equal (Store.readState db "arc") initial "Extension scalars, nested emptiness and blob round trip"))
    testCase "nested extension edits store and reverse only the changed subtree" (fun _ ->
        let values=[for index in 0..1999 -> ExtensionCollection [ExtensionText(string index);ExtensionBool false]]
        let original=root (Map.ofList ["large",ExtensionCollection values])
        let initial=state [original]
        withStore initial (fun db ->
            let changed=values |> List.mapi (fun index value -> if index=1500 then ExtensionCollection [ExtensionText "updated";ExtensionBool false] else value)
            let edited=state [{original with Extensions=Map.ofList ["large",ExtensionCollection changed]}]
            StorageDiagnostics.reset()
            save db 0L initial edited 1
            Expect.equal StorageDiagnostics.CellsWritten 1 "Unchanged extension siblings are not rewritten"
            Expect.equal StorageDiagnostics.HistoryCellsWritten 1 "Only changed subtree recorded"
            Expect.isTrue (count db "SELECT length(before_value)+length(after_value) FROM history_change WHERE property='large'" < 1000L) "History excludes unchanged extension children"
            Expect.equal (Store.readState db "arc") edited "Nested edit persists"
            undo db 1 false
            Expect.equal (Store.readState db "arc") initial "Undo restores exact extension tree"
            undo db 1 true
            Expect.equal (Store.readState db "arc") edited "Redo restores exact extension tree"))
    testCase "deleted entity ordinal is restored by undo after reopening its history" (fun _ ->
        let a,b,c = sample "a" "A",sample "b" "B",sample "c" "C"
        let initial = state [root Map.empty;a;b;c]
        withStore initial (fun db ->
            save db 0L (state [b]) (state []) 1
            Expect.equal (Store.listIds db "arc" (Some "Sample")) ["a";"c"] "Deletion keeps surviving order"
            undo db 1 false
            Expect.equal (Store.listIds db "arc" (Some "Sample")) ["a";"b";"c"] "Undo restores original ordinal"
            undo db 1 true
            Expect.equal (Store.listIds db "arc" (Some "Sample")) ["a";"c"] "Redo removes restored entity"))
    testCase "deferred foreign key failure rolls back revision data and delta history" (fun _ ->
        let initial = state [root Map.empty]
        withStore initial (fun db ->
            let invalid = {initial.Entities.Head with Extensions=Map.ofList ["missing",ExtensionObject "unknown"]}
            Expect.throws (fun () -> save db 0L initial (state [invalid]) 1) "Commit rejects missing extension target"
            Expect.equal (Store.revision db "arc") 0L "Claimed revision rolled back"
            Expect.equal (Store.readState db "arc") initial "Data rolled back"
            Expect.equal (count db "SELECT count(*) FROM history") 0L "Delta history rolled back"
            db.WithTransaction(fun () -> Expect.isFalse (Store.claimRevision db "arc" 99L) "Stale revision rejected")
            Expect.equal (Store.revision db "arc") 0L "Failed claim has no effect"))
    testCase "explicit archive retains displaced normalized values and delta history" (fun _ ->
        let initial = state [root Map.empty]
        withStore initial (fun db ->
            let edited = state [{initial.Entities.Head with Properties=Map.add "title" (Text "Archived") initial.Entities.Head.Properties}]
            save db 0L initial edited 1
            db.WithTransaction(fun () -> Store.archive db "arc" |> ignore)
            Expect.equal (count db "SELECT count(*) FROM archived_history") 1L "Operation archived"
            Expect.equal (count db "SELECT count(*) FROM archived_history_change") 1L "Property delta archived"
            let snapshot = db.Scalar("SELECT state FROM arc_archive") |> Option.get |> fun value -> Codec.decodeState(value.AsText())
            Expect.equal snapshot edited "Explicit archive includes displaced state"))
]
