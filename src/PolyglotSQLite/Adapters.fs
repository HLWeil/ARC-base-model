namespace PolyglotSQLite

open System

#if FABLE_COMPILER_PYTHON
open Fable.Core
open Fable.Core.PyInterop

module internal Adapters =
    [<Import("connect", "sqlite3")>]
    let private connect (_path: string): obj = nativeOnly

    [<Emit("isinstance($0, __import__('sqlite3').Connection)")>]
    let private isConnection (_connection: obj): bool = nativeOnly

    [<Emit("$0.in_transaction")>]
    let private inTransaction (_connection: obj): bool = nativeOnly

    [<Emit("$0.isolation_level")>]
    let private isolationLevel (_connection: obj): obj = nativeOnly

    [<Emit("$0.isolation_level = $1")>]
    let private setIsolationLevel (_connection: obj) (_value: obj): unit = nativeOnly

    [<Emit("$0.text_factory is str")>]
    let private hasDefaultTextFactory (_connection: obj): bool = nativeOnly

    [<Emit("$0.cursor()")>]
    let private cursor (_connection: obj): obj = nativeOnly

    [<Emit("$0.row_factory = None")>]
    let private clearRowFactory (_cursor: obj): unit = nativeOnly

    [<Emit("$0.close()")>]
    let private close (_value: obj): unit = nativeOnly

    [<Emit("$0.execute($1, $2)")>]
    let private execute (_cursor: obj) (_sql: string) (_parameters: obj): unit = nativeOnly

    [<Emit("$0.executescript($1)")>]
    let private script (_cursor: obj) (_sql: string): unit = nativeOnly

    [<Emit("$0.description")>]
    let private description (_cursor: obj): obj[] = nativeOnly

    [<Emit("$0.fetchall()")>]
    let private fetchAll (_cursor: obj): obj[] = nativeOnly

    [<Emit("$0[int($1)]")>]
    let private item (_value: obj) (_index: int): obj = nativeOnly

    [<Emit("$0 is None")>]
    let private isNone (_value: obj): bool = nativeOnly

    [<Emit("type($0).__name__")>]
    let private typeName (_value: obj): string = nativeOnly

    [<Emit("type($0) is str")>]
    let private isText (_value: obj): bool = nativeOnly

    [<Emit("type($0) is int")>]
    let private isInteger (_value: obj): bool = nativeOnly

    [<Emit("type($0) is float")>]
    let private isReal (_value: obj): bool = nativeOnly

    [<Emit("type($0) is bytes")>]
    let private isBlob (_value: obj): bool = nativeOnly

    [<Emit("int($0)")>]
    let private nativeInteger (_value: int64): obj = nativeOnly

    [<Emit("float($0)")>]
    let private nativeReal (_value: float): obj = nativeOnly

    [<Emit("bytes(int(item) for item in $0)")>]
    let private nativeBlob (_value: byte[]): obj = nativeOnly

    [<Emit("{}")>]
    let private emptyParameters (): obj = nativeOnly

    [<Emit("$0[$1] = $2")>]
    let private setParameter (_parameters: obj) (_name: string) (_value: obj): unit = nativeOnly

    let private toNative (value: SqlValue) =
        match value.Kind with
        | "null" -> null
        | "text" -> box (value.AsText())
        | "integer" -> nativeInteger (value.AsInteger())
        | "real" -> nativeReal (value.AsReal())
        | "blob" -> nativeBlob (value.AsBlob())
        | kind -> invalidOp ("Unknown SQL storage class: " + kind)

    let private fromNative value =
        if isNone value then SqlValue.Null()
        elif isText value then SqlValue.Text(unbox<string> value)
        // Factories normalize these native values to their internal Fable representations.
        elif isInteger value then SqlValue.Integer(unbox<int64> value)
        elif isReal value then SqlValue.Real(unbox<float> value)
        elif isBlob value then SqlValue.Blob(unbox<byte[]> value)
        else invalidOp ("Unsupported sqlite3 result type '" + typeName value + "'. Borrowed connections require detect_types=0 and text_factory=str.")

    let private parametersToNative (parameters: ResizeArray<SqlParameter>) =
        let result = emptyParameters ()
        for parameter in parameters do
            setParameter result (parameter.Name.Substring 1) (toNative parameter.Value)
        result

    let private withCursor connection action =
        let current = cursor connection
        try
            clearRowFactory current
            action current
        finally
            close current

    let private executeStatement connection sql parameters =
        withCursor connection (fun current ->
            execute current sql (parametersToNative parameters)
            // Exhaust RETURNING and SELECT statements before releasing the cursor.
            fetchAll current |> ignore)

    let private query connection sql parameters =
        withCursor connection (fun current ->
            execute current sql (parametersToNative parameters)
            let metadata = description current
            if isNone (box metadata) then ResizeArray<SqlRow>()
            else
                let columns = metadata |> Array.map (fun column -> unbox<string> (item column 0))
                let rows = ResizeArray<SqlRow>()
                for row in fetchAll current do
                    let values = columns |> Array.mapi (fun index _ -> fromNative (item row index))
                    rows.Add(SqlRow(columns, values))
                rows)

    let private foreignKeys connection =
        let result = query connection "PRAGMA foreign_keys" (ResizeArray())
        result.Count = 1 && result[0].Get(0).AsInteger() = 1L

    let private setForeignKeys connection enabled =
        executeStatement connection (if enabled then "PRAGMA foreign_keys = ON" else "PRAGMA foreign_keys = OFF") (ResizeArray())

    let private create connection owned: ISqliteBackend =
        let mutable oldIsolation: obj = null
        let mutable oldForeignKeys = false
        let mutable settingsCaptured = false
        try
            if not (isConnection connection) then invalidArg "nativeConnection" "Expected an open sqlite3.Connection."
            // Accessing in_transaction also rejects a closed handle. Check before changing any settings.
            if inTransaction connection then invalidArg "nativeConnection" "Cannot wrap a connection with an active transaction."
            if not (hasDefaultTextFactory connection) then
                invalidArg "nativeConnection" "Borrowed sqlite3 connections require text_factory=str and detect_types=0."
            oldIsolation <- isolationLevel connection
            oldForeignKeys <- foreignKeys connection
            settingsCaptured <- true
            setIsolationLevel connection null
            setForeignKeys connection true
            if not (foreignKeys connection) then invalidOp "SQLite foreign-key enforcement could not be enabled."
            { new ISqliteBackend with
                member _.Execute(sql, parameters) = executeStatement connection sql parameters
                member _.Query(sql, parameters) = query connection sql parameters
                member _.ExecuteScript(sql) = withCursor connection (fun current -> script current sql)
                member _.InTransaction = inTransaction connection
                member _.Close() =
                    if owned then close connection
                    else
                        try setForeignKeys connection oldForeignKeys
                        finally setIsolationLevel connection oldIsolation }
        with _ ->
            // Never mask the setup exception with cleanup failures.
            try
                if owned then close connection
                elif settingsCaptured then
                    try setForeignKeys connection oldForeignKeys
                    finally setIsolationLevel connection oldIsolation
            with _ -> ()
            reraise ()

    let openFile (path: string) = create (connect path) true

    /// The sqlite3 public API cannot expose detect_types: callers must supply detect_types=0.
    /// text_factory must be str. Per-cursor row_factory=None preserves positional results.
    let wrapConnection (nativeConnection: obj) = create nativeConnection false

#else
#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
open Fable.Core
open Fable.Core.JsInterop

module internal Adapters =
    [<ImportDefault("better-sqlite3")>]
    let private constructor: obj = jsNative

    [<Emit("$0 != null && $0.open === true && typeof $0.prepare === 'function'")>]
    let private isOpen (_connection: obj): bool = jsNative

    [<Emit("$0.inTransaction")>]
    let private inTransaction (_connection: obj): bool = jsNative

    [<Emit("$0.close()")>]
    let private close (_connection: obj): unit = jsNative

    [<Emit("Boolean($0.pragma('foreign_keys', {simple: true}))")>]
    let private foreignKeys (_connection: obj): bool = jsNative

    [<Emit("$0.pragma($1 ? 'foreign_keys = ON' : 'foreign_keys = OFF')")>]
    let private setForeignKeys (_connection: obj) (_enabled: bool): unit = jsNative

    [<Emit("$0.prepare($1).safeIntegers(true)")>]
    let private prepare (_connection: obj) (_sql: string): obj = jsNative

    [<Emit("$0.reader")>]
    let private isReader (_statement: obj): bool = jsNative

    [<Emit("$0.run($1)")>]
    let private run (_statement: obj) (_parameters: obj): unit = jsNative

    [<Emit("$0.raw(true).all($1)")>]
    let private all (_statement: obj) (_parameters: obj): obj[][] = jsNative

    [<Emit("$0.columns().map(column => column.name)")>]
    let private columns (_statement: obj): string[] = jsNative

    [<Emit("$0.exec($1)")>]
    let private script (_connection: obj) (_sql: string): unit = jsNative

    [<Emit("typeof $0")>]
    let private typeName (_value: obj): string = jsNative

    [<Emit("$0 === null")>]
    let private isNullValue (_value: obj): bool = jsNative

    [<Emit("$0 instanceof Uint8Array")>]
    let private isBlob (_value: obj): bool = jsNative

    [<Emit("Object.create(null)")>]
    let private emptyParameters (): obj = jsNative

    [<Emit("$0[$1] = $2")>]
    let private setParameter (_parameters: obj) (_name: string) (_value: obj): unit = jsNative

    let private toNative (value: SqlValue) =
        match value.Kind with
        | "null" -> null
        | "text" -> box (value.AsText())
        | "integer" -> box (value.AsInteger())
        | "real" -> box (value.AsReal())
        | "blob" -> box (value.AsBlob())
        | kind -> invalidOp ("Unknown SQL storage class: " + kind)

    let private fromNative value =
        if isNullValue value then SqlValue.Null()
        else
            match typeName value with
            | "string" -> SqlValue.Text(unbox<string> value)
            | "bigint" -> SqlValue.Integer(unbox<int64> value)
            | "number" -> SqlValue.Real(unbox<float> value)
            | _ when isBlob value -> SqlValue.Blob(unbox<byte[]> value)
            | kind -> invalidOp ("Unsupported better-sqlite3 result type: " + kind)

    let private parametersToNative (parameters: ResizeArray<SqlParameter>) =
        let result = emptyParameters ()
        for parameter in parameters do
            setParameter result (parameter.Name.Substring 1) (toNative parameter.Value)
        result

    let private executeStatement connection sql parameters =
        let statement = prepare connection sql
        let bound = parametersToNative parameters
        if isReader statement then all statement bound |> ignore
        else run statement bound

    let private query connection sql parameters =
        let statement = prepare connection sql
        let bound = parametersToNative parameters
        let rows = ResizeArray<SqlRow>()
        if isReader statement then
            let names = columns statement
            for row in all statement bound do rows.Add(SqlRow(names, Array.map fromNative row))
        else run statement bound
        rows

    let private create connection owned: ISqliteBackend =
        let mutable previousForeignKeys = false
        let mutable settingsCaptured = false
        try
            if not (isOpen connection) then invalidArg "nativeConnection" "Expected an open better-sqlite3 database."
            if inTransaction connection then invalidArg "nativeConnection" "Cannot wrap a connection with an active transaction."
            previousForeignKeys <- foreignKeys connection
            settingsCaptured <- true
            setForeignKeys connection true
            if not (foreignKeys connection) then invalidOp "SQLite foreign-key enforcement could not be enabled."
            { new ISqliteBackend with
                member _.Execute(sql, parameters) = executeStatement connection sql parameters
                member _.Query(sql, parameters) = query connection sql parameters
                member _.ExecuteScript(sql) = script connection sql
                member _.InTransaction = inTransaction connection
                member _.Close() =
                    if owned then close connection
                    else setForeignKeys connection previousForeignKeys }
        with _ ->
            try
                if owned then close connection
                elif settingsCaptured then setForeignKeys connection previousForeignKeys
            with _ -> ()
            reraise ()

    let openFile (path: string) = create (createNew constructor path) true
    let wrapConnection (nativeConnection: obj) = create nativeConnection false

#else
open System.Data
open Microsoft.Data.Sqlite

module internal Adapters =
    let private parameterValue (value: SqlValue): obj =
        match value.Kind with
        | "null" -> box DBNull.Value
        | "text" -> box (value.AsText())
        | "integer" -> box (value.AsInteger())
        | "real" -> box (value.AsReal())
        | "blob" -> box (value.AsBlob())
        | kind -> invalidOp ("Unknown SQL storage class: " + kind)

    let private fromNative (value: obj) =
        match value with
        | null -> SqlValue.Null()
        | :? DBNull -> SqlValue.Null()
        | :? string as text -> SqlValue.Text text
        | :? int64 as integer -> SqlValue.Integer integer
        | :? double as real -> SqlValue.Real real
        | :? (byte[]) as blob -> SqlValue.Blob blob
        | other -> invalidOp ("Unsupported Microsoft.Data.Sqlite result type: " + other.GetType().FullName)

    let private command (connection: Microsoft.Data.Sqlite.SqliteConnection) sql (parameters: ResizeArray<SqlParameter>) =
        let result = connection.CreateCommand()
        try
            result.CommandText <- sql
            for parameter in parameters do
                let bound = result.CreateParameter()
                bound.ParameterName <- parameter.Name
                bound.Value <- parameterValue parameter.Value
                result.Parameters.Add(bound) |> ignore
            result
        with _ ->
            result.Dispose()
            reraise ()

    let private execute connection sql parameters =
        use current = command connection sql parameters
        current.ExecuteNonQuery() |> ignore

    let private query connection sql parameters =
        use current = command connection sql parameters
        use reader = current.ExecuteReader()
        let columns = [| for index in 0 .. reader.FieldCount - 1 -> reader.GetName(index) |]
        let rows = ResizeArray<SqlRow>()
        while reader.Read() do
            let values = [| for index in 0 .. reader.FieldCount - 1 -> fromNative (reader.GetValue index) |]
            rows.Add(SqlRow(columns, values))
        rows

    let private inTransaction (connection: Microsoft.Data.Sqlite.SqliteConnection) =
        if connection.State <> ConnectionState.Open then invalidOp "The SQLite connection is closed."
        SQLitePCL.raw.sqlite3_get_autocommit(connection.Handle) = 0

    let private foreignKeys connection =
        let rows = query connection "PRAGMA foreign_keys" (ResizeArray())
        rows.Count = 1 && rows[0].Get(0).AsInteger() = 1L

    let private setForeignKeys connection enabled =
        execute connection (if enabled then "PRAGMA foreign_keys = ON" else "PRAGMA foreign_keys = OFF") (ResizeArray())

    let private create (connection: Microsoft.Data.Sqlite.SqliteConnection) owned: ISqliteBackend =
        let mutable previousForeignKeys = false
        let mutable settingsCaptured = false
        try
            if isNull connection || connection.State <> ConnectionState.Open then
                invalidArg "nativeConnection" "Expected an open Microsoft.Data.Sqlite.SqliteConnection."
            if inTransaction connection then invalidArg "nativeConnection" "Cannot wrap a connection with an active transaction."
            previousForeignKeys <- foreignKeys connection
            settingsCaptured <- true
            setForeignKeys connection true
            if not (foreignKeys connection) then invalidOp "SQLite foreign-key enforcement could not be enabled."
            { new ISqliteBackend with
                member _.Execute(sql, parameters) = execute connection sql parameters
                member _.Query(sql, parameters) = query connection sql parameters
                member _.ExecuteScript(sql) = execute connection sql (ResizeArray())
                member _.InTransaction = inTransaction connection
                member _.Close() =
                    if owned then connection.Dispose()
                    else setForeignKeys connection previousForeignKeys }
        with _ ->
            try
                if owned then connection.Dispose()
                elif settingsCaptured then setForeignKeys connection previousForeignKeys
            with _ -> ()
            reraise ()

    let openFile (path: string) =
        let builder = SqliteConnectionStringBuilder()
        builder.DataSource <- path
        let connection = new Microsoft.Data.Sqlite.SqliteConnection(builder.ToString())
        try
            connection.Open()
            create connection true
        with _ ->
            connection.Dispose()
            reraise ()

    let wrapConnection (nativeConnection: obj) =
        match nativeConnection with
        | :? Microsoft.Data.Sqlite.SqliteConnection as connection -> create connection false
        | _ -> invalidArg "nativeConnection" "Expected an open Microsoft.Data.Sqlite.SqliteConnection."
#endif
#endif
