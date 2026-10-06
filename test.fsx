// First: dotnet build src/ARCtrl/ARCtrl.fsproj -c Release
// Then:  dotnet fsi test.fsx
#r "nuget: Fable.Core, 5.0.0"
#r "nuget: YAMLicious, 1.0.2"
#r "nuget: Microsoft.Data.Sqlite, 10.0.6"
#r "src/ARCtrl/bin/Release/netstandard2.0/ARCBaseModel.dll"
#r "src/ARCtrl/bin/Release/netstandard2.0/PolyglotSQLite.dll"
#r "src/ARCtrl/bin/Release/netstandard2.0/ARCtrl.dll"

open System
open System.IO
open ARCBaseModel
open ARCtrl

// A fresh folder each run avoids overwriting an existing session.
let folder = @"C:\Users\HLWei\Downloads\test"

let arc = ARC.create(folder, Dataset(["process-provenance"], ["example-arc"]))

arc.Agent.create("Looookas")



arc.save()

arc.Dataset.addAgent(arc.Model, arc.Agent.list()[0]) |> ignore

let sample = arc.Sample.create("leaf")
let proc = arc.Process.create("measurement")

arc.Sample.list()

sample

arc.Dataset.addProcess(arc.Model, proc) |> ignore
arc.Process.setInputSample(proc, sample) |> ignore

arc.Sample.setName(sample, "edited leaf") |> ignore
printfn "Edited: %s" sample.Name
arc.History.undo()
printfn "Undo:   %s" sample.Name
arc.History.redo()
printfn "Redo:   %s" sample.Name
arc.save() // Writes only the root Dataset graph to arc.yml.

let standalone = arc.Sample.create("session-only sample")
let id = standalone.Id.Value
arc.close() // Does not implicitly save.

let resumed = ARC.openFolder(folder, "sql")
printfn "Resumed: %s" (resumed.Sample.get(id)).Name
printfn "YAML: %s" (Path.Combine(folder, "arc.yml"))
printfn "SQLite: %s" resumed.DatabasePath

arc.close()

resumed.save()



resumed.Dataset.setTitle("Resumed Dataset") |> ignore
