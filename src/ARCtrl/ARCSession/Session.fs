namespace ARCSession

open System
open Fable.Core
open ARCBaseModel
open ARCSession.Internal
open ARCtrl
open ARCtrl.Internal

/// Lightweight metadata; enumeration does not deserialize or hydrate a graph.
[<AttachMembers>]
type ArcInfo internal (arcId: string, rootEntityId: string, folder: string option) =
    let _arcId = arcId
    let _rootEntityId = rootEntityId
    let _folder = folder
    member _.ArcId = _arcId
    member _.RootEntityId = _rootEntityId
    member _.Folder = _folder

/// A database container holding independently editable ARC entries.
[<AttachMembers>]
type Session private (repository: Repository) =
    member _.DatabasePath = if repository.DatabasePath = ":memory:" then None else Some repository.DatabasePath
    member _.createArc(rootDataset: Dataset) = ARC.Wrap(ArcFactory.create repository rootDataset,false)
    member _.openArc(arcId: string) =
        Model.required "arcId" arcId |> ignore
        repository.Access(fun () ->
            match repository.TryActive arcId with
            | Some arc -> unbox<ARC> arc
            | None -> ARC.Wrap(ArcFactory.load repository arcId,false))
    member _.listArcs() =
        repository.Access(fun () ->
            repository.Connection.Query("SELECT arc_id,root_id,folder FROM session ORDER BY rowid")
            |> Seq.map (fun row -> ArcInfo(row.GetByName("arc_id").AsText(),row.GetByName("root_id").AsText(),Store.optText row "folder"))
            |> ResizeArray)
    member _.close() = repository.Close()
    static member createInMemory() = new Session(Repository.CreateInMemory())
    static member createFile(path: string) = new Session(Repository.CreateFile(path))
    static member openFile(path: string) = new Session(Repository.OpenFile(path))
    interface ISessionOwner with member _.Repository = repository
    interface IDisposable with member this.Dispose() = this.close()
