namespace ARCManagement.Internal

open ARCManagement.Helper

open System
open System.Collections.Generic
open ARCManagement.Internal

/// Owns the connection. ARC contexts own projections, never connections.
type Repository private (database: Database) =
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
    member _.TryActive(arcId) =
        match active.TryGetValue arcId with true, (value,_) -> Some value | _ -> None
    member _.Track(arcId, value, close) = active[arcId] <- value,close
    member _.Forget(arcId) =
        active.Remove(arcId) |> ignore

    member this.Close() =
        lock gate (fun () ->
            if not closed then
                if busy then invalidOp "Cannot close a repository during an operation."
                for _, close in active.Values |> Seq.toArray do close()
                active.Clear()
                database.Close()

                closed <- true)
    static member private Create(path) =
        let database = Store.openDatabase path
        try Store.initialize database.Connection; new Repository(database)
        with _ -> database.Close(); reraise()
    static member CreateInMemory() = Repository.Create(":memory:")
    static member CreateFile(path) =
        let path = Path.fullPath(Model.required "path" path)
        Path.createFileExclusive path
        try Repository.Create(path)
        with _ -> Path.deleteFile path; reraise()
    static member OpenFile(path) =
        let path = Path.fullPath(Model.required "path" path)
        if not (Path.pathExists path) then invalidArg "path" "ARC repository does not exist."
        let database = Store.openDatabase path
        try Store.validateRepository database.Connection; new Repository(database)
        with _ -> database.Close(); reraise()
    interface IDisposable with member this.Dispose() = this.Close()
