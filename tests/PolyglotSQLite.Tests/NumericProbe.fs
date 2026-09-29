namespace PolyglotSQLite.Tests

open Fable.Core
open PolyglotSQLite

/// Executed by native consumers to check F# arithmetic on native-created values.
[<AttachMembers>]
type NumericProbe() =
    static member Divide(left: SqlValue, right: SqlValue) =
        SqlValue.Integer(left.AsInteger() / right.AsInteger())

    static member Remainder(left: SqlValue, right: SqlValue) =
        SqlValue.Integer(left.AsInteger() % right.AsInteger())

    static member Add(left: SqlValue, right: SqlValue) =
        SqlValue.Integer(left.AsInteger() + right.AsInteger())

    static member LessThan(left: SqlValue, right: SqlValue) =
        left.AsInteger() < right.AsInteger()

    static member FormatInteger(value: SqlValue) = string (value.AsInteger())

    static member Hypotenuse(left: SqlValue, right: SqlValue) =
        let x, y = left.AsReal(), right.AsReal()
        SqlValue.Real(sqrt (x * x + y * y))

    static member MutateBlobCopy(value: SqlValue) =
        let bytes = value.AsBlob()
        bytes.[0] <- 99uy
        SqlValue.Blob(bytes)

    static member Identity(value: SqlValue) = value

    static member CountDivision(row: SqlRow) =
        SqlValue.Integer(int64 (row.Count / -2))
