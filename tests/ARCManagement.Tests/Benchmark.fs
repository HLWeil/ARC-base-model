module ARCManagement.Tests.Benchmark

open System
open System.Globalization
open Fable.Core
open ARCBaseModel
open ARCManagement
open ARCManagement.Internal
open ARCManagement.Helper

#if FABLE_COMPILER_PYTHON
let private runtime = "python"
[<Fable.Core.Emit("__import__('time').perf_counter() * 1000.0")>]
let private now () : float = nativeOnly
[<Fable.Core.Emit("(__import__('tracemalloc').start() if not __import__('tracemalloc').is_tracing() else None)")>]
let private beginMemory () : unit = nativeOnly
[<Fable.Core.Emit("float(__import__('tracemalloc').get_traced_memory()[0])")>]
let private memory () : float = nativeOnly
#else
#if FABLE_COMPILER
let private runtime = "javascript"
[<Fable.Core.Emit("performance.now()")>]
let private now () : float = jsNative
let private beginMemory () = ()
[<Fable.Core.Emit("process.memoryUsage().heapUsed")>]
let private memory () : float = jsNative
#else
let private runtime = "dotnet"
let private now () = float (Diagnostics.Stopwatch.GetTimestamp()) * 1000.0 / float Diagnostics.Stopwatch.Frequency
let private beginMemory () = ()
let private memory () = float (GC.GetTotalMemory(false))
#endif
#endif

let private fixture size =
    let sample = Sample("sample",id="sample")
    let processes = [for index in 0..size-1 -> Process("process-"+string index,id="p-"+string index,input=EntityReference.Sample sample)]
    Dataset(["process-provenance"],["benchmark"],id="root",processes=processes)

/// Timings include the updated immutable return value and its reachable closure.
/// SQL/history counters isolate storage cost from that intentional output cost.
let run outputPath sizes =
    beginMemory()
    let output = ResizeArray<string>()
    output.Add("runtime,processes,operation,repetitions,median_ms,p95_ms,memory_bytes,queries,rows_read,cells_written,history_cells,full_state_reads,materialized_values")
    Path.ensureDirectoryOfFile outputPath
    Path.writeFileText outputPath (output[0]+"\n")
    for size in sizes do
        if size<1 then invalidArg "size" "Benchmark size must be positive."
        use session = Session.createInMemory()
        let graph = session.createArc(fixture size)
        let mutable counter = 0
        let measure name repetitions action =
            StorageDiagnostics.reset()
            let materialized = graph.Context.MaterializedCount
            let elapsed = [|for _ in 1..repetitions do
                                let started = now()
                                action()
                                yield now()-started|] |> Array.sort
            let median = elapsed[elapsed.Length/2]
            let p95 = elapsed[min (elapsed.Length-1) (int (ceil(float elapsed.Length*0.95))-1)]
            let number (value:float) = value.ToString("F3",CultureInfo.InvariantCulture)
            let line = String.concat "," [runtime;string size;name;string repetitions;number median;number p95;number(memory())
                                          string StorageDiagnostics.Queries;string StorageDiagnostics.RowsRead;string StorageDiagnostics.CellsWritten;string StorageDiagnostics.HistoryCellsWritten
                                          string StorageDiagnostics.FullStateReads;string(graph.Context.MaterializedCount-materialized)]
            output.Add(line)
            Path.appendFileText outputPath (line+"\n")
            printfn "BENCHMARK %s" line
        measure "cold_sample_read" 1 (fun () -> graph.Sample.get("sample") |> ignore)
        measure "warm_sample_read" 100 (fun () -> graph.Sample.get("sample") |> ignore)
        measure "storage_root_closure" 1 (fun () -> Store.readClosure session.Repository.Connection graph.ArcId ["root"] |> ignore)
        measure "cold_root_read" 1 (fun () -> graph.readModel() |> ignore)
        measure "warm_root_read" 100 (fun () -> graph.readModel() |> ignore)
        let original=graph.Model
        let sample=graph.Sample.get("sample")
        measure "static_root_title_copy" 100 (fun () -> Dataset.withTitle(original,"copy") |> ignore)
        measure "focused_sample_write" 5 (fun () ->
            counter <- counter+1
            graph.Sample.setName(sample,"sample-"+string counter) |> ignore)
        measure "no_op_sample_write" 5 (fun () ->
            let observed=graph.Sample.read("sample")
            graph.Sample.setName(observed.Value,observed.Value.Name) |> ignore)
        measure "focused_root_title_write" 3 (fun () ->
            counter <- counter+1
            graph.Dataset.setTitle(original,"title-"+string counter) |> ignore)
        measure "recursive_graph_upsert" 3 (fun () ->
            counter <- counter+1
            let observed = graph.readModel()
            let first = Process.withName(observed.Processes[0],"changed-"+string counter)
            let changed = Dataset.withProcesses(observed,Seq.append [first] (observed.Processes |> Seq.skip 1))
            graph.Dataset.upsert(changed,baseline=observed,recursive=true) |> ignore)
        measure "append_duplicate_reference" 3 (fun () -> graph.Dataset.addProcess(original,original.Processes[0]) |> ignore)
        measure "undo" 3 (fun () -> graph.History.undo() |> ignore)
        measure "redo" 3 (fun () -> graph.History.redo() |> ignore)
    Path.ensureDirectoryOfFile outputPath
    Path.writeFileText outputPath (String.concat "\n" output+"\n")
    printfn "Benchmark CSV: %s" outputPath

let tryRun (arguments:string array) =
    match arguments with
    | [|"--benchmark";path|] -> run path [100;1000;10000];true
    | [|"--benchmark";path;size|] -> run path [Int32.Parse(size,CultureInfo.InvariantCulture)];true
    | _ -> false
