namespace ARCSession.Internal

open System
open System.Collections.Generic
open ARCtrl.Internal

/// Objects cannot belong to two live ARC projections, even across repositories.
module private Ownership =
    let private gate = obj()
    let private owners = ResizeArray<obj * string * obj>()
    let assertAvailable repository arcId value =
        lock gate (fun () ->
            if owners |> Seq.exists (fun (owner,arc,existing) ->
                obj.ReferenceEquals(existing,value) && (not (obj.ReferenceEquals(owner,repository)) || arc <> arcId)) then
                invalidOp "Object belongs to another ARC. Supply an independent graph.")
    let claim repository arcId value =
        lock gate (fun () ->
            assertAvailable repository arcId value
            if not (owners |> Seq.exists (fun (owner,arc,existing) ->
                obj.ReferenceEquals(owner,repository) && arc = arcId && obj.ReferenceEquals(existing,value))) then
                owners.Add(repository,arcId,value))
    let release repository arcId value =
        lock gate (fun () ->
            let index = owners |> Seq.tryFindIndex (fun (owner,arc,existing) ->
                obj.ReferenceEquals(owner,repository) && arc = arcId && obj.ReferenceEquals(existing,value))
            index |> Option.iter owners.RemoveAt)
    let forget repository arcId =
        lock gate (fun () ->
            for index in [owners.Count - 1 .. -1 .. 0] do
                let owner,arc,_ = owners[index]
                if obj.ReferenceEquals(owner,repository) && arc = arcId then owners.RemoveAt(index))
    let close repository =
        lock gate (fun () ->
            for index in [owners.Count - 1 .. -1 .. 0] do
                let owner,_,_ = owners[index]
                if obj.ReferenceEquals(owner,repository) then owners.RemoveAt(index))

/// Owns the connection. ARC contexts own projections, never connections.
type internal Repository private (database: Database) =
    let mutable closed = false
    let mutable busy = false
    let gate = obj()
    let active = Dictionary<string, obj * (unit -> unit)>()
    member _.Connection = database.Connection
    member _.DatabasePath = database.Path
    member _.EnsureOpen() = if closed then invalidOp "The ARC repository is closed."
    member this.Access(action: unit -> 'T) : 'T =
        lock gate (fun () ->
            this.EnsureOpen()
            if busy then invalidOp "Overlapping ARC repository operations are unsupported."
            busy <- true
            try action() finally busy <- false)
    member this.AssertAvailable(arcId, value) =
        this.EnsureOpen()
        Ownership.assertAvailable this arcId value
    member this.Claim(arcId, value) =
        this.AssertAvailable(arcId,value)
        Ownership.claim this arcId value
    member this.Release(arcId, value) = Ownership.release this arcId value
    member _.TryActive(arcId) =
        match active.TryGetValue arcId with true, (value,_) -> Some value | _ -> None
    member _.Track(arcId, value, close) = active[arcId] <- value,close
    member this.Forget(arcId) =
        active.Remove(arcId) |> ignore
        Ownership.forget this arcId
    member this.Close() =
        lock gate (fun () ->
            if not closed then
                if busy then invalidOp "Cannot close a repository during an operation."
                for _, close in active.Values |> Seq.toArray do close()
                active.Clear()
                database.Close()
                Ownership.close this
                closed <- true)
    static member private Create(path) =
        let database = Store.openDatabase path
        try Store.initialize database.Connection; new Repository(database)
        with _ -> database.Close(); reraise()
    static member CreateInMemory() = Repository.Create(":memory:")
    static member CreateFile(path) =
        let path = Files.fullPath(Model.required "path" path)
        Files.createExclusive path
        try Repository.Create(path)
        with _ -> Files.remove path; reraise()
    static member OpenFile(path) =
        let path = Files.fullPath(Model.required "path" path)
        if not (Files.exists path) then invalidArg "path" "ARC repository does not exist."
        let database = Store.openDatabase path
        try Store.validateRepository database.Connection; new Repository(database)
        with _ -> database.Close(); reraise()
    interface IDisposable with member this.Dispose() = this.Close()

/// Internal bridge keeps public ARC and Session facades out of a recursive type group.
type internal ISessionOwner =
    abstract Repository: Repository
