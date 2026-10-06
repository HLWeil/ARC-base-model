module ManagementPrototype.Tests.PortableMain

open Fable.Pyxpecto

[<EntryPoint>]
let main arguments = Pyxpecto.runTests [||] FullModel.tests
