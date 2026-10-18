module ARCManagement.Tests.Main

open Fable.Pyxpecto

let tests = testList "ARCManagement" [
    Helpers.tests
    Behavior.tests
    Safety.tests
    CoreSql.tests
    StorageBehavior.tests
    Performance.tests
    FullModel.tests
    Toolbox.tests
    Yaml.tests
]

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
let arguments: string array = Fable.Core.JsInterop.emitJsExpr () "process.argv.slice(2)"
if not (Interop.tryRun arguments || Benchmark.tryRun arguments) then
    async {
        let! _ = Pyxpecto.runTestsAsync [||] tests
        return ()
    } |> Async.StartImmediate
#else
[<EntryPoint>]
let main arguments =
    if Interop.tryRun arguments || Benchmark.tryRun arguments then 0 else
    match arguments with
#if !FABLE_COMPILER
    | [|"--demo"; folder|] -> Walkthrough.run folder; 0
#endif
    | [|"--core-sql"|] -> Pyxpecto.runTests [||] CoreSql.tests
    | [||] -> Pyxpecto.runTests [||] tests
    | _ -> eprintfn "Usage: [--demo <new-folder> | --core-sql]"; 1
#endif
