"""Handwritten native consumer; no Fable runtime helpers are imported."""
import math
import sqlite3
import sys

import polyglot_sqlite as SQLite
from polyglot_sqlite import SqlParameter, SqlRow, SqlValue
from polyglot_sqlite_probe import NumericProbe


def rejects(action):
    try:
        action()
    except Exception:
        return
    raise AssertionError("Expected operation to be rejected")


for value in [-(2**63), -9007199254740993, -1, 0, 9007199254740993, 2**63 - 1]:
    wrapped = SqlValue.Integer(value)
    assert wrapped.Kind == "integer"
    assert type(wrapped.AsInteger()) is int
    assert wrapped.AsInteger() == value
    assert isinstance(wrapped, SqlValue)
    assert NumericProbe.Identity(wrapped) is wrapped
for invalid in [1.0, 1.5, True, False, "1", None, 2**63, -(2**63) - 1]:
    rejects(lambda: SqlValue.Integer(invalid))
for value in [0, 0.125, 1.0, math.inf, -math.inf]:
    wrapped = SqlValue.Real(value)
    assert type(wrapped.AsReal()) is float
    assert wrapped.AsReal() == value
for invalid in [math.nan, True, False, "1", None]:
    rejects(lambda: SqlValue.Real(invalid))
assert SqlValue.Null().IsNull is True
assert SqlValue.Text("").AsText() == ""
for invalid in [1, True, None]:
    rejects(lambda: SqlValue.Text(invalid))
rejects(lambda: SqlValue.Real(1.0).AsInteger())
rejects(lambda: SqlValue.Integer(1).AsReal())
rejects(lambda: setattr(SqlValue.Null(), "Kind", "text"))

blob_input = bytearray([0, 128, 255])
blob = SqlValue.Blob(blob_input)
blob_input[0] = 12
assert type(blob.AsBlob()) is bytes
assert blob.AsBlob() == bytes([0, 128, 255])
assert NumericProbe.MutateBlobCopy(blob).AsBlob() == bytes([99, 128, 255])
assert blob.AsBlob() == bytes([0, 128, 255])
assert SqlValue.Blob(bytes()).AsBlob() == bytes()
rejects(lambda: SqlValue.Blob([0, 1, 2]))

negative, divisor = SqlValue.Integer(-17), SqlValue.Integer(5)
assert NumericProbe.Divide(negative, divisor).AsInteger() == -3
assert NumericProbe.Remainder(negative, divisor).AsInteger() == -2
assert NumericProbe.Add(SqlValue.Integer(2**63 - 2), SqlValue.Integer(1)).AsInteger() == 2**63 - 1
assert NumericProbe.Add(SqlValue.Integer(-(2**63) + 1), SqlValue.Integer(-1)).AsInteger() == -(2**63)
assert NumericProbe.LessThan(negative, divisor) is True
assert NumericProbe.FormatInteger(SqlValue.Integer(-(2**63))) == "-9223372036854775808"
assert NumericProbe.Hypotenuse(SqlValue.Real(3), SqlValue.Real(4)).AsReal() == 5.0

row = SqlRow(["z", "x", "x"], [SqlValue.Integer(11), SqlValue.Text("first"), SqlValue.Text("second")])
assert type(row.Count) is int
assert row.Count == 3
assert NumericProbe.CountDivision(row).AsInteger() == -1
assert row.GetColumnName(0) == "z"
assert row.GetByName("z").AsInteger() == 11
assert row.Get(2).AsText() == "second"
assert row.TryGetByName("missing") is None
rejects(lambda: row.GetByName("x"))
for invalid in [0.5, True, False, -1, 3]:
    rejects(lambda: row.Get(invalid))
    rejects(lambda: row.GetColumnName(invalid))
assert SqlParameter("value", SqlValue.Null()).Name == "$value"
rejects(lambda: SqlParameter("@value", SqlValue.Null()))

if "--values-only" not in sys.argv:
    Sqlite, Table, TableRepository = SQLite.Sqlite, SQLite.Table, SQLite.TableRepository
    db = Sqlite.OpenInMemory()
    try:
        columns = ["group", 'key"part', "value"]
        table = Table("native values", columns, ["group", 'key"part'],
            lambda value: [SqlValue.Text(value["group"]), SqlValue.Integer(value["key"]), SqlValue.Text(value["value"])],
            lambda result: {"group": result.Get(0).AsText(), "key": result.Get(1).AsInteger(), "value": result.Get(2).AsText()})
        columns[0] = "mutated"
        repository = TableRepository(db, table)
        db.Execute('CREATE TABLE "native values" ("group" TEXT, "key""part" INTEGER, "value" TEXT, PRIMARY KEY ("group", "key""part"))')
        repository.Insert({"group": "a", "key": 9007199254740993, "value": "quoted';--"})
        key = [SqlValue.Text("a"), SqlValue.Integer(9007199254740993)]
        assert repository.Get(key) == {"group": "a", "key": 9007199254740993, "value": "quoted';--"}
        repository.Update({"group": "a", "key": 9007199254740993, "value": "updated"})
        assert type(repository.List()) is list
        assert repository.List()[0]["value"] == "updated"
        repository.Delete(key)
        assert repository.Get(key) is None

        db.Execute("CREATE TABLE native_transaction (value)")

        def success():
            db.Execute("INSERT INTO native_transaction VALUES ($value)", [SqlParameter("value", SqlValue.Integer(7))])
            return "native callback result"

        assert db.WithTransaction(success) == "native callback result"

        def failure():
            db.Execute("INSERT INTO native_transaction VALUES (8)")
            raise ValueError("native callback failure")

        rejects(lambda: db.WithTransaction(failure))

        class AwaitableResult:
            def __await__(self):
                yield
                return "async result"

        def asynchronous():
            db.Execute("INSERT INTO native_transaction VALUES (9)")
            return AwaitableResult()

        rejects(lambda: db.WithTransaction(asynchronous))
        async_calls = []

        async def known_async():
            async_calls.append("invoked")
            db.Execute("INSERT INTO native_transaction VALUES (10)")

        rejects(lambda: db.WithTransaction(known_async))
        assert async_calls == []
        assert db.Scalar("SELECT count(*) FROM native_transaction").AsInteger() == 1
        invalid_codec = Table("native_transaction", ["value"], ["value"],
            lambda _: [SqlValue.Integer(7)], lambda _: None)
        invalid_repository = TableRepository(db, invalid_codec)
        assert invalid_repository.Get([SqlValue.Integer(999)]) is None
        rejects(lambda: invalid_repository.Get([SqlValue.Integer(7)]))
        rejects(lambda: invalid_repository.List())
        for falsy in [False, 0, ""]:
            valid_codec = Table("native_transaction", ["value"], ["value"],
                lambda _: [SqlValue.Integer(7)], lambda _, result=falsy: result)
            valid_repository = TableRepository(db, valid_codec)
            assert valid_repository.Get([SqlValue.Integer(7)]) == falsy
            assert valid_repository.List() == [falsy]
        assert db.Scalar("SELECT NULL").IsNull is True
        assert db.Scalar("SELECT 1 WHERE 0") is None
        duplicates = db.Query("SELECT 1 AS x, 2 AS x")
        assert type(duplicates) is list
        assert isinstance(duplicates[0], SqlRow)
        assert isinstance(duplicates[0].Get(0), SqlValue)
        assert duplicates[0].Get(1).AsInteger() == 2
        rejects(lambda: duplicates[0].TryGetByName("x"))
        rejects(lambda: db.Execute("INSERT INTO native_transaction VALUES ($x)", [SqlParameter("x", SqlValue.Integer(1)), SqlParameter("$x", SqlValue.Integer(2))]))
    finally:
        db.Close()
    db.Close()
    rejects(lambda: db.Query("SELECT 1"))

    native = sqlite3.connect(":memory:")
    try:
        original_isolation = native.isolation_level
        native.execute("PRAGMA foreign_keys=OFF")
        # A borrowed custom row factory must not reorder the library's results.
        native.row_factory = sqlite3.Row
        wrapped = Sqlite.WrapConnection(native)
        assert wrapped.Scalar("PRAGMA foreign_keys").AsInteger() == 1
        assert wrapped.Scalar("SELECT 11 AS z, 22 AS a").AsInteger() == 11
        wrapped.Execute("CREATE TABLE borrowed (value)")
        wrapped.BeginTransaction()
        wrapped.Execute("INSERT INTO borrowed VALUES (1)")
        wrapped.Close()
        assert native.isolation_level == original_isolation
        assert native.row_factory is sqlite3.Row
        assert native.execute("PRAGMA foreign_keys").fetchone()[0] == 0
        assert native.execute("SELECT count(*) FROM borrowed").fetchone()[0] == 0
        native.execute("INSERT INTO borrowed VALUES (2)")
        assert native.in_transaction
        rejects(lambda: Sqlite.WrapConnection(native))
        assert native.in_transaction
        native.rollback()
        assert native.execute("SELECT count(*) FROM borrowed").fetchone()[0] == 0
        native.text_factory = bytes
        rejects(lambda: Sqlite.WrapConnection(native))
    finally:
        native.close()

    # A denied cleanup must retain ownership of the script's transaction so
    # Close can retry it instead of treating it as unexpected caller work.
    native = sqlite3.connect(":memory:")
    try:
        native.execute("CREATE TABLE cleanup_retry (value)")
        original_isolation = native.isolation_level
        wrapped = Sqlite.WrapConnection(native)

        def deny_rollback(action, argument1, argument2, database, trigger):
            if action == sqlite3.SQLITE_TRANSACTION and argument1 == "ROLLBACK":
                return sqlite3.SQLITE_DENY
            return sqlite3.SQLITE_OK

        native.set_authorizer(deny_rollback)
        rejects(lambda: wrapped.ExecuteScript("BEGIN; INSERT INTO cleanup_retry VALUES (1);"))
        assert native.in_transaction
        native.set_authorizer(None)
        wrapped.Close()
        assert not native.in_transaction
        assert native.isolation_level == original_isolation
        assert native.execute("SELECT count(*) FROM cleanup_retry").fetchone()[0] == 0
    finally:
        native.set_authorizer(None)
        native.close()

print("PolyglotSQLite native Python checks passed")
