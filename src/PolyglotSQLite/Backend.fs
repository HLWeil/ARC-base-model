namespace PolyglotSQLite

/// Internal provider boundary. Statement validation and transaction ownership live in the connection facade.
type internal ISqliteBackend =
    abstract Execute: sql: string * parameters: ResizeArray<SqlParameter> -> unit
    abstract Query: sql: string * parameters: ResizeArray<SqlParameter> -> ResizeArray<SqlRow>
    abstract ExecuteScript: sql: string -> unit
    abstract InTransaction: bool
    abstract Close: unit -> unit
