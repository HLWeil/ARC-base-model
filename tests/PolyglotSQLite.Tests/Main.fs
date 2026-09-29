module PolyglotSQLite.Tests.Main

open Fable.Pyxpecto

let tests = testList "PolyglotSQLite" [Values.tests; Drivers.tests; Transactions.tests]

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
async {
    let! _ = Pyxpecto.runTestsAsync [||] tests
    return ()
}
|> Async.StartImmediate
#else
[<EntryPoint>]
let main _ = Pyxpecto.runTests [||] tests
#endif
