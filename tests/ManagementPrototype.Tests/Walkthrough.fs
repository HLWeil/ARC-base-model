module ManagementPrototype.Tests.Walkthrough

#if !FABLE_COMPILER
open ARCBaseModel
open ARCtrl

/// Run with: dotnet run --project tests/ManagementPrototype.Tests -- --demo <new-folder>
let run folder =
    let root = Dataset(["process-provenance"], ["example-arc"])
    use arc = ARC.create(folder, root)
    let study = arc.Dataset.create("study-1")
    arc.Dataset.addPart(arc.Model, study) |> ignore
    let sample = arc.Sample.create("leaf")
    let proc = arc.Process.create("measurement")
    arc.Dataset.addProcess(study, proc) |> ignore
    arc.Process.setInputSample(proc, sample) |> ignore

    arc.Sample.setName(sample, "edited leaf") |> ignore
    printfn "Edited: %s" sample.Name
    arc.History.undo()
    printfn "Undo:   %s" sample.Name
    arc.History.redo()
    printfn "Redo:   %s" sample.Name

    arc.save()
    printfn "Saved root Dataset to %s/arc.yml" arc.Folder
    let standalone = arc.Sample.create("session-only sample")
    let id = standalone.Id.Value
    arc.close()

    use resumed = ARC.openFolder(folder, "auto")
    printfn "Resumed standalone object: %s" (resumed.Sample.get(id)).Name
    printfn "Session database: %s" resumed.DatabasePath
    printfn "Root dirty: %b; session-only objects: %b" resumed.IsDirty resumed.HasSessionOnlyObjects
#endif

