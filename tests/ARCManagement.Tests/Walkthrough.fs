module ARCManagement.Tests.Walkthrough

#if !FABLE_COMPILER
open ARCBaseModel
open ARCManagement
open ARCtrl

/// dotnet run --project tests/ARCManagement.Tests -- --demo <new-folder>
let run folder =
    use arc = ARC.create(folder,Dataset(["process-provenance"],["example-arc"]))
    let graph = arc.ManagedGraph
    let sample = graph.Sample.add(graph.readModel().Revision,Sample("leaf",id="sample"))
    let edited = graph.Sample.setName(sample.Revision,"sample","edited leaf")
    printfn "Old: %s; edited: %s" sample.Value.Name edited.Value.Name
    let undone = graph.History.undo(edited.Revision)
    printfn "Undo: %s" (graph.Sample.get("sample")).Name
    graph.History.redo(undone.Revision) |> ignore
    arc.save()
    printfn "Saved root to %s/arc.yml; standalone objects: %b" arc.Folder arc.HasSessionOnlyObjects
#endif
