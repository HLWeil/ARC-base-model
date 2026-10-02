module ManagementPrototype.Tests.Main

open Fable.Pyxpecto

[<EntryPoint>]
let main arguments =
    match arguments with
    | [|"--demo"; folder|] -> Walkthrough.run folder; 0
    | [||] -> Pyxpecto.runTests [||] Behavior.tests
    | _ -> eprintfn "Usage: [--demo <new-folder>]"; 1
