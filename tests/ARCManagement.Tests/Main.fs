module ARCManagement.Tests.Main

open Fable.Pyxpecto

let tests = testList "ARCManagement" [
    Helpers.tests
    Behavior.tests
    CoreSql.tests
    FullModel.tests
    Toolbox.tests
    Yaml.tests
]

[<EntryPoint>]
let main arguments =
#if FABLE_COMPILER
    if Interop.tryRun arguments then 0 else Pyxpecto.runTests [||] tests
#else
    if Interop.tryRun arguments then 0
    else
        match arguments with
        | [|"--demo"; folder|] -> Walkthrough.run folder; 0
        | [|"--core-sql"|] -> Pyxpecto.runTests [||] CoreSql.tests
        | [||] -> Pyxpecto.runTests [||] tests
        | _ -> eprintfn "Usage: [--demo <new-folder> | --core-sql | --write-fixture <path> | --read-fixture <path>]"; 1
#endif
