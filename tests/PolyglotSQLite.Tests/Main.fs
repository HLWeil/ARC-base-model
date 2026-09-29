module PolyglotSQLite.Tests.Main

open Fable.Pyxpecto

let tests = testList "PolyglotSQLite" [Values.tests; Drivers.tests; Transactions.tests; Repository.tests]

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
let arguments: string array = Fable.Core.JsInterop.emitJsExpr () "process.argv.slice(2)"
if not (Interop.tryRun arguments) then
    async {
        let! _ = Pyxpecto.runTestsAsync [||] tests
        return ()
    }
    |> Async.StartImmediate
#else
[<EntryPoint>]
let main arguments =
    if Interop.tryRun arguments then 0
    else Pyxpecto.runTests [||] tests
#endif
