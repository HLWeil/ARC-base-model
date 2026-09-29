namespace PolyglotSQLite

open System
open Fable.Core

module private CallbackBoundary =
#if FABLE_COMPILER_PYTHON
    [<Emit("__import__('inspect').iscoroutinefunction($0) or __import__('inspect').iscoroutinefunction(getattr($0, '__call__', None))")>]
    let isAsyncFunction (_action: obj) : bool = nativeOnly
    [<Emit("__import__('inspect').isawaitable($0)")>]
    let isAwaitable (_value: obj) : bool = nativeOnly
    [<Emit("$0.close() if __import__('inspect').iscoroutine($0) else None")>]
    let abandonAwaitable (_value: obj) : unit = nativeOnly
    [<Emit("$0()")>]
    let invokeNative (_action: obj) : obj = nativeOnly
#else
#if FABLE_COMPILER
    [<Emit("Object.prototype.toString.call($0) === '[object AsyncFunction]'")>]
    let isAsyncFunction (_action: obj) : bool = jsNative
    [<Emit("$0 != null && typeof $0.then === 'function'")>]
    let isAwaitable (_value: obj) : bool = jsNative
    let abandonAwaitable (_value: obj) = ()
#else
    let isAsyncFunction (_action: obj) = false
    let isAwaitableType (valueType: Type) =
        let name = valueType.FullName
        typeof<Threading.Tasks.Task>.IsAssignableFrom(valueType)
        || (not (isNull name) && (name.StartsWith("Microsoft.FSharp.Control.FSharpAsync") || name.StartsWith("System.Threading.Tasks.ValueTask")))
    let isAwaitable (value: obj) =
        match value with
        | null -> false
        | other -> isAwaitableType (other.GetType())
    let abandonAwaitable (_value: obj) = ()
#endif
#endif

type private TransactionScope(savepoint: string option) =
    let mutable active = true
    member _.Savepoint = savepoint
    member _.Active with get() = active and set(value) = active <- value

/// A managed transaction or nested savepoint. Close/dispose rolls back unfinished work.
[<AttachMembers>]
type SqliteTransaction internal (commit: unit -> unit, rollback: unit -> unit, close: unit -> unit) =
    member _.Commit() = commit()
    member _.Rollback() = rollback()
    member _.Close() = close()
    interface IDisposable with member this.Dispose() = this.Close()

/// A synchronous SQLite connection. Wrapping a native handle requires exclusive use until Close.
[<AttachMembers>]
type SqliteConnection internal (backend: ISqliteBackend) =
    let scopes = ResizeArray<TransactionScope>()
    let callbacks = ResizeArray<TransactionScope>()
    let mutable closed = false
    let mutable unusable = false
    let mutable scriptRemainder = false
    let mutable nextScope = 0
    let emptyParameters = ResizeArray<SqlParameter>()

    let invalidateScopes () =
        for scope in scopes do scope.Active <- false
        scopes.Clear()

    let ensureOpen () =
        if closed then invalidOp "The SQLite connection is closed."
        if unusable then invalidOp "The SQLite connection is unusable after a transaction cleanup failure."
        if callbacks |> Seq.exists (fun scope -> not scope.Active) then
            invalidOp "The callback transaction was aborted; leave the callback before starting further work."

    let ensureTransactionState () =
        let active = backend.InTransaction
        if scopes.Count > 0 && not active then
            invalidateScopes()
            invalidOp "SQLite has ended the managed transaction; its scopes are no longer active."
        if scopes.Count = 0 && active then
            invalidOp "An external transaction is active; native handles require exclusive use while wrapped."

    let resyncAfterFailure () =
        try
            if scopes.Count > 0 && not backend.InTransaction then invalidateScopes()
        with _ -> unusable <- true

    let run operation =
        ensureOpen()
        ensureTransactionState()
        try
            let result = operation()
            ensureTransactionState()
            result
        with _ ->
            resyncAfterFailure()
            reraise()

    let checkTop (scope: TransactionScope) =
        ensureOpen()
        if not scope.Active then invalidOp "The transaction scope is no longer active."
        ensureTransactionState()
        if scopes.Count = 0 || not (obj.ReferenceEquals(scopes[scopes.Count - 1], scope)) then
            invalidOp "Transaction scopes must finish in reverse creation order."

    let complete (scope: TransactionScope) commit =
        checkTop scope
        try
            match scope.Savepoint, commit with
            | None, true -> backend.Execute("COMMIT", emptyParameters)
            | None, false -> backend.Execute("ROLLBACK", emptyParameters)
            | Some name, true -> backend.Execute("RELEASE SAVEPOINT " + name, emptyParameters)
            | Some name, false ->
                backend.Execute("ROLLBACK TO SAVEPOINT " + name, emptyParameters)
                backend.Execute("RELEASE SAVEPOINT " + name, emptyParameters)
            scopes.RemoveAt(scopes.Count - 1)
            scope.Active <- false
            ensureTransactionState()
        with _ ->
            resyncAfterFailure()
            reraise()

    let beginScope () =
        ensureOpen()
        ensureTransactionState()
        nextScope <- nextScope + 1
        let savepoint = if scopes.Count = 0 then None else Some("polyglot_" + string nextScope)
        match savepoint with
        | None -> backend.Execute("BEGIN DEFERRED", emptyParameters)
        | Some name -> backend.Execute("SAVEPOINT " + name, emptyParameters)
        let scope = TransactionScope(savepoint)
        scopes.Add scope
        scope

    let rollbackCallbackScope (scope: TransactionScope) =
        if scope.Active && not closed then
            if not backend.InTransaction then invalidateScopes()
            else
                while scope.Active do complete scopes[scopes.Count - 1] false

    member _.Execute(sql: string, ?parameters: seq<SqlParameter>) =
        run (fun () ->
            let statement = SqlText.validateSingle sql
            let bindings = Parameters.copyAndValidate parameters
            backend.Execute(statement, bindings))

    member _.Query(sql: string, ?parameters: seq<SqlParameter>) =
        run (fun () ->
            let statement = SqlText.validateSingle sql
            let bindings = Parameters.copyAndValidate parameters
            backend.Query(statement, bindings))

    member this.Scalar(sql: string, ?parameters: seq<SqlParameter>) =
        let rows = this.Query(sql, ?parameters = parameters)
        if rows.Count = 0 then None else Some(rows[0].Get(0))

    /// Scripts are non-atomic unless they contain their own complete transaction.
    member _.ExecuteScript(sql: string) =
        ensureOpen()
        ensureTransactionState()
        if scopes.Count <> 0 then invalidOp "ExecuteScript cannot run inside a managed transaction."
        ValueBoundary.required "sql" sql |> ignore
        try
            backend.ExecuteScript sql
            if backend.InTransaction then
                invalidOp "The script left an unfinished transaction; its remaining work will be rolled back."
        with _ ->
            // Any remaining transaction began in this script: the entry state was idle.
            // Remember ownership even if inspecting native state or rolling back fails.
            scriptRemainder <- true
            try
                if backend.InTransaction then
                    backend.Execute("ROLLBACK", emptyParameters)
                scriptRemainder <- false
            with _ -> unusable <- true
            reraise()

    member _.BeginTransaction() =
        let scope = beginScope()
        new SqliteTransaction((fun () -> complete scope true), (fun () -> complete scope false),
                              (fun () -> if scope.Active then complete scope false))

#if FABLE_COMPILER_PYTHON
    [<CompiledName("_withTransactionFSharp")>]
#endif
    member _.WithTransaction(action: unit -> 'T) : 'T =
        ValueBoundary.required "action" action |> ignore
        if CallbackBoundary.isAsyncFunction (box action) then
            invalidArg "action" "Transaction callbacks must be synchronous; async functions are unsupported."
#if !FABLE_COMPILER
        if CallbackBoundary.isAwaitableType typeof<'T> then
            invalidArg "action" "Transaction callbacks must return synchronous values, not Task, ValueTask, or Async."
#endif
        let scope = beginScope()
        callbacks.Add scope
        try
            try
                let result = action()
                if CallbackBoundary.isAwaitable (box result) then
                    CallbackBoundary.abandonAwaitable (box result)
                    invalidArg "action" "Transaction callbacks must be synchronous; awaitable results are unsupported."
                complete scope true
                result
            with _ ->
                try rollbackCallbackScope scope
                with _ -> unusable <- true
                reraise()
        finally
            callbacks.RemoveAt(callbacks.Count - 1)

#if FABLE_COMPILER_PYTHON
    [<CompiledName("WithTransaction")>]
    member this.NativeWithTransaction(action: obj) : obj =
        ValueBoundary.required "action" action |> ignore
        if CallbackBoundary.isAsyncFunction action then
            invalidArg "action" "Transaction callbacks must be synchronous; async functions are unsupported."
        this.WithTransaction(fun () -> CallbackBoundary.invokeNative action)
#endif

    member _.Close() =
        if not closed then
            // Unexpected external work must never be committed or rolled back by release.
            if scopes.Count = 0 && not scriptRemainder && backend.InTransaction then
                invalidOp "Cannot release a connection with an unexpected external transaction."
            if scopes.Count > 0 || scriptRemainder then
                if backend.InTransaction then backend.Execute("ROLLBACK", emptyParameters)
                invalidateScopes()
                scriptRemainder <- false
            backend.Close()
            closed <- true

    interface IDisposable with member this.Dispose() = this.Close()

/// Factories for owned connections and exclusively borrowed native handles.
[<AttachMembers>]
type Sqlite =
    static member OpenFile(path: string) =
        ValueBoundary.required "path" path |> ignore
        new SqliteConnection(Adapters.openFile path)
    static member OpenInMemory() = Sqlite.OpenFile(":memory:")
#if FABLE_COMPILER
    static member WrapConnection(nativeConnection: obj) =
        ValueBoundary.required "nativeConnection" nativeConnection |> ignore
        new SqliteConnection(Adapters.wrapConnection nativeConnection)
#else
    static member WrapConnection(nativeConnection: Microsoft.Data.Sqlite.SqliteConnection) =
        ValueBoundary.required "nativeConnection" nativeConnection |> ignore
        new SqliteConnection(Adapters.wrapConnection nativeConnection)
#endif
