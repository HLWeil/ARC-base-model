namespace ARCtrl

open System
open Fable.Core
open ARCBaseModel
open ARCManagement
open ARCManagement.Helper
open ARCManagement.Internal
open ARCtrl.Helper
open ARCtrl.Internal

/// A folder workspace backed by one managed graph and its retained SQL state.
[<AttachMembers>]
type ARC private (graph: ManagedGraph, ownedSession: ARCManagement.Session option) =
    member _.ManagedGraph = graph
    member _.Model = graph.Model
    member _.Revision = graph.Revision
    member _.readModel() = graph.readModel()
    member _.Entity = graph.Entity
    member _.Dataset = graph.Dataset
    member _.Process = graph.Process
    member _.Sample = graph.Sample
    member _.Organization = graph.Organization
    member _.Agent = graph.Agent
    member _.ScholarlyArticle = graph.ScholarlyArticle
    member _.Annotation = graph.Annotation
    member _.FormalParameter = graph.FormalParameter
    member _.DefinedTermSet = graph.DefinedTermSet
    member _.DefinedTerm = graph.DefinedTerm
    member _.Descriptor = graph.Descriptor
    member _.Data = graph.Data
    member _.Recipe = graph.Recipe
    member _.History = graph.History
    member _.ArcId = graph.ArcId
    member _.FolderBinding = graph.Context.Folder
    member _.Folder = graph.Context.Folder |> Option.defaultWith (fun () -> invalidOp "ARC has no folder binding.")
    member _.bindFolder(folder: string) = Workspace.bind graph.Context folder
    member _.recoverFolder(source: string) = Workspace.recover graph.Context source
    member _.DatabasePath = graph.DatabasePath
    member _.IsDirty = graph.Context.IsDirty
    member _.HasSessionOnlyObjects = graph.HasSessionOnlyObjects
    member _.save() = Workspace.save graph.Context
    member _.close() =
        graph.close()
        ownedSession |> Option.iter (fun session -> session.close())
    static member bind(graph: ManagedGraph, folder: string) =
        Model.required "graph" graph |> ignore
        Workspace.bind graph.Context folder
        new ARC(graph,None)
    static member fromGraph(graph: ManagedGraph) =
        Model.required "graph" graph |> ignore
        if graph.Context.Folder.IsNone then invalidOp "ARC has no folder binding. Use ARC.bind."
        new ARC(graph,None)
    static member create(folder: string, rootDataset: Dataset) =
        let folder = Path.fullPath(Model.required "folder" folder)
        Model.required "rootDataset" rootDataset |> ignore
        if Path.pathExists (Workspace.path folder) || Path.pathExists (Path.combineNative folder Layout.ARCFileName) then invalidOp "Workspace already exists. Use openFolder."
        Path.createDirectory(Path.combineNative folder Layout.ARCConfigFolderName)
        let session = ARCManagement.Session.createFile(Workspace.path folder)
        try
            let context = ArcFactory.createWithMetadata session.Repository rootDataset (Some folder) None
            new ARC(ManagedGraph.Wrap(context),Some session)
        with _ ->
            session.close()
            Path.deleteFile(Workspace.path folder)
            reraise()
    static member openFolder(folder: string, source: string) =
        let session,graph = Workspace.openFolder folder source
        new ARC(graph,Some session)
    static member importFolder(session: ARCManagement.Session, folder: string) =
        Model.required "session" session |> ignore
        new ARC(ManagedGraph.Wrap(Workspace.import session.Repository folder),None)
    interface IDisposable with member this.Dispose() = this.close()
