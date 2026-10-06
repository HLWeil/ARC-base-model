module ManagementPrototype.Tests.Main

open Fable.Pyxpecto

[<EntryPoint>]
let main arguments =
    match arguments with
    | [|"--demo"; folder|] -> Walkthrough.run folder; 0
    | [|"--core-sql"|] -> Pyxpecto.runTests [||] CoreSql.tests
    | [||] -> Pyxpecto.runTests [||] (testList "ARCtrl" [Behavior.tests; FullModel.tests; CoreSql.tests])
    | _ -> eprintfn "Usage: [--demo <new-folder> | --core-sql]"; 1
