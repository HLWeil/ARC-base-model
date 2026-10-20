module ARCManagement.Tests.Performance

open Fable.Pyxpecto
open ARCBaseModel
open ARCManagement
open ARCManagement.Internal

let tests = testList "Management structural performance" [
    testCase "focused root title writes do not rewrite a large reachable collection or scan unrelated entities" (fun _ ->
        let sample = Sample("shared",id="sample")
        let processes = [for index in 0..1999 -> Process("p",id="p-"+string index,input=EntityReference.Sample sample)]
        let root = Dataset(["process-provenance"],["root"],id="root",processes=processes)
        use session = Session.createInMemory()
        let graph = session.createArc(root)
        // Standalone entities are outside the returned root's reachable closure.
        graph.Sample.add(Sample("unrelated",id="unrelated")) |> ignore
        StorageDiagnostics.reset()
        let fullReads = graph.Context.FullReadCount
        let saved = graph.Dataset.setTitle(graph.Dataset.get("root"),"updated")
        Expect.equal saved.Value.Processes.Count 2000 "Returned value contains its full reachable graph"
        Expect.equal StorageDiagnostics.CellsWritten 1 "One scalar cell written beside large relationship collection"
        Expect.equal StorageDiagnostics.HistoryCellsWritten 1 "One scalar history delta"
        Expect.equal StorageDiagnostics.FullStateReads 0 "No full normalized registry read"
        Expect.equal graph.Context.FullReadCount fullReads "Runtime avoids full-state path"
        Expect.isTrue (StorageDiagnostics.Queries<100) "Referenced target validation is batched rather than one query per entity"
        let db = session.Repository.Connection
        Expect.equal ((db.Scalar("SELECT count(*) FROM entity_reference WHERE owner_id='root' AND property='processes'") |> Option.get).AsInteger()) 2000L "Relationship rows preserved"
        Expect.equal saved.Value.Title (Some "updated") "Updated value returned"
        Expect.equal root.Title None "Input remains unchanged")
    testCase "cached reads check SQL revision without materializing or reloading entity rows" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"],id="root"))
        graph.readModel() |> ignore
        StorageDiagnostics.reset()
        let materialized = graph.Context.MaterializedCount
        for _ in 1..100 do graph.readModel() |> ignore
        Expect.equal StorageDiagnostics.RowsRead 0 "No normalized entity reload for current revision"
        Expect.equal StorageDiagnostics.FullStateReads 0 "No whole registry read"
        Expect.equal StorageDiagnostics.Queries 100 "Exactly one small revision query per cached read"
        Expect.equal graph.Context.MaterializedCount materialized "Sealed cached value reused")
]
