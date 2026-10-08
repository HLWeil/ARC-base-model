module ManagementPrototype.Tests.Main

open Fable.Pyxpecto

let tests = testList "ARCtrl" [
    Helpers.tests
    Behavior.tests
    CoreSql.tests
    FullModel.tests
    Toolbox.tests
]

[<EntryPoint>]
let main arguments =
#if FABLE_COMPILER
    Pyxpecto.runTests [||] tests
#else
    match arguments with
    | [|"--demo"; folder|] -> Walkthrough.run folder; 0
    | [|"--core-sql"|] -> Pyxpecto.runTests [||] CoreSql.tests
    | [||] -> Pyxpecto.runTests [||] tests
    | _ -> eprintfn "Usage: [--demo <new-folder> | --core-sql]"; 1
#endif
