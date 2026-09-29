module PolyglotSQLite.Tests.Values

open System
open PolyglotSQLite
open PolyglotSQLite.Tests
open Fable.Pyxpecto

let tests = testList "values and rows" [
    testCase "storage classes and strict readers" <| fun _ ->
        let values = [
            "null", SqlValue.Null()
            "text", SqlValue.Text("")
            "integer", SqlValue.Integer(0L)
            "real", SqlValue.Real(0.0)
            "blob", SqlValue.Blob([||])
        ]
        for kind, value in values do
            Expect.equal value.Kind kind "Storage kind is explicit"
            Expect.equal value.IsNull (kind = "null") "Only NULL is null"
            if kind <> "text" then Expect.throws (fun () -> value.AsText() |> ignore) "No text coercion"
            if kind <> "integer" then Expect.throws (fun () -> value.AsInteger() |> ignore) "No integer coercion"
            if kind <> "real" then Expect.throws (fun () -> value.AsReal() |> ignore) "No real coercion"
            if kind <> "blob" then Expect.throws (fun () -> value.AsBlob() |> ignore) "No blob coercion"
        Expect.equal (SqlValue.Text("").AsText()) "" "Empty text is a value"
        Expect.equal (SqlValue.Integer(0L).AsInteger()) 0L "Zero is a value"
        Expect.equal (SqlValue.Real(0.0).AsReal()) 0.0 "Real zero is a value"

    testCase "R05 int64 precision, bounds, and F# numeric operations" <| fun _ ->
        for value in [Int64.MinValue; -9007199254740993L; -1L; 0L; 9007199254740993L; Int64.MaxValue] do
            Expect.equal (SqlValue.Integer(value).AsInteger()) value "Integer factory preserves all bits"
        let negative, divisor = SqlValue.Integer(-17L), SqlValue.Integer(5L)
        Expect.equal (NumericProbe.Divide(negative, divisor).AsInteger()) -3L "Division truncates toward zero"
        Expect.equal (NumericProbe.Remainder(negative, divisor).AsInteger()) -2L "Remainder has dividend's sign"
        Expect.equal (NumericProbe.Add(SqlValue.Integer(Int64.MaxValue - 1L), SqlValue.Integer(1L)).AsInteger()) Int64.MaxValue "Addition near upper bound"
        Expect.equal (NumericProbe.Add(SqlValue.Integer(Int64.MinValue + 1L), SqlValue.Integer(-1L)).AsInteger()) Int64.MinValue "Addition near lower bound"
        Expect.isTrue (NumericProbe.LessThan(negative, divisor)) "Comparison uses integer values"
        Expect.equal (NumericProbe.FormatInteger(SqlValue.Integer(Int64.MinValue))) "-9223372036854775808" "Formatting retains precision"

    testCase "real math uses both supplied values and rejects NaN" <| fun _ ->
        Expect.equal (NumericProbe.Hypotenuse(SqlValue.Real(3.0), SqlValue.Real(4.0)).AsReal()) 5.0 "sqrt and multiplication use valid F# numbers"
        Expect.equal (SqlValue.Real(0.125).AsReal()) 0.125 "Fraction is preserved"
        Expect.equal (SqlValue.Real(Double.PositiveInfinity).AsReal()) Double.PositiveInfinity "Positive infinity"
        Expect.equal (SqlValue.Real(Double.NegativeInfinity).AsReal()) Double.NegativeInfinity "Negative infinity"
        Expect.throws (fun () -> SqlValue.Real(Double.NaN) |> ignore) "SQLite must not silently bind NaN as NULL"

    testCase "BLOB inputs and outputs are defensive copies" <| fun _ ->
        let input = [|0uy; 128uy; 255uy|]
        let value = SqlValue.Blob(input)
        input.[0] <- 42uy
        let first = value.AsBlob()
        first.[1] <- 17uy
        Suspect.sequenceEqual (value.AsBlob()) [0uy; 128uy; 255uy] "Neither input nor returned buffers mutate value"
        Suspect.sequenceEqual (NumericProbe.MutateBlobCopy(value).AsBlob()) [99uy; 128uy; 255uy] "Transpiled F# can mutate byte arrays"
        Suspect.sequenceEqual (value.AsBlob()) [0uy; 128uy; 255uy] "Probe mutation cannot escape"

    testCase "R09 parameter names are normalized and validated" <| fun _ ->
        Expect.equal (SqlParameter("name", SqlValue.Null()).Name) "$name" "Bare names normalize"
        Expect.equal (SqlParameter("$name", SqlValue.Null()).Name) "$name" "Canonical names are stable"
        for name in [""; "$"; "@name"; ":name"; "?"; "1name"; "two names"; "$x;DROP TABLE t"; "x\u0000y"] do
            Expect.throws (fun () -> SqlParameter(name, SqlValue.Null()) |> ignore) "Invalid names are rejected"

    testCase "R07 ordered duplicate columns and exact name lookup" <| fun _ ->
        let row = SqlRow(["z"; "x"; "x"], [SqlValue.Integer(11L); SqlValue.Text("first"); SqlValue.Text("second")])
        Expect.equal row.Count 3 "Duplicate names survive"
        Expect.equal (NumericProbe.CountDivision(row).AsInteger()) -1L "Count remains a valid F# integer for division"
        Expect.equal (row.GetColumnName(0)) "z" "Ordinal names survive"
        Expect.equal (row.Get(1).AsText()) "first" "First duplicate is available by ordinal"
        Expect.equal (row.Get(2).AsText()) "second" "Second duplicate is available by ordinal"
        Expect.equal (row.GetByName("z").AsInteger()) 11L "Unique name lookup"
        Expect.isNone (row.TryGetByName("Z")) "Name matching is exact"
        Expect.throws (fun () -> row.GetByName("missing") |> ignore) "Missing required name"
        Expect.throws (fun () -> row.GetByName("x") |> ignore) "Ambiguous required name"
        Expect.throws (fun () -> row.TryGetByName("x") |> ignore) "Ambiguous optional name"
        Expect.throws (fun () -> row.Get(-1) |> ignore) "Negative ordinal"
        Expect.throws (fun () -> row.GetColumnName(3) |> ignore) "Out of range ordinal"

    testCase "row construction copies and validates inputs" <| fun _ ->
        let names = [|"a"|]
        let values = [|SqlValue.Text("original")|]
        let row = SqlRow(names, values)
        names.[0] <- "changed"
        values.[0] <- SqlValue.Text("changed")
        Expect.equal (row.GetColumnName(0)) "a" "Names are copied"
        Expect.equal (row.Get(0).AsText()) "original" "Value collection is copied"
        Expect.throws (fun () -> SqlRow(["a"], []) |> ignore) "Unequal lengths"
]
