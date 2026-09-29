module ARCBaseModel.Tests.Main

open Fable.Pyxpecto

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
// Pyxpecto owns the exit code, so direct Node execution detects failed assertions.
async {
    let! _ = Pyxpecto.runTestsAsync [||] Behavior.tests
    return ()
}
|> Async.StartImmediate
#else
[<EntryPoint>]
let main _ = Pyxpecto.runTests [||] Behavior.tests
#endif
