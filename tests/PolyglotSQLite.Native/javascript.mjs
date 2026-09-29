import assert from "node:assert/strict";
import Database from "better-sqlite3";
import * as SQLite from "polyglot-sqlite";
import { NumericProbe } from "polyglot-sqlite-probe";

const { SqlValue, SqlParameter, SqlRow } = SQLite;

for (const value of [-(1n << 63n), -9007199254740993n, -1n, 0n, 9007199254740993n, (1n << 63n) - 1n]) {
  const wrapped = SqlValue.Integer(value);
  assert.equal(wrapped.Kind, "integer");
  assert.equal(typeof wrapped.AsInteger(), "bigint");
  assert.equal(wrapped.AsInteger(), value);
  assert.ok(wrapped instanceof SqlValue);
  assert.equal(NumericProbe.Identity(wrapped), wrapped);
}
for (const invalid of [1, 1.5, true, false, "1", null, 1n << 63n, -(1n << 63n) - 1n]) {
  assert.throws(() => SqlValue.Integer(invalid));
}
for (const value of [0, 0.125, 1, Infinity, -Infinity]) {
  const wrapped = SqlValue.Real(value);
  assert.equal(typeof wrapped.AsReal(), "number");
  assert.equal(wrapped.AsReal(), value);
}
for (const invalid of [NaN, true, false, "1", null, 1n]) assert.throws(() => SqlValue.Real(invalid));
assert.equal(SqlValue.Null().IsNull, true);
assert.equal(SqlValue.Text("").AsText(), "");
for (const invalid of [1, true, null]) assert.throws(() => SqlValue.Text(invalid));
assert.throws(() => SqlValue.Real(1).AsInteger());
assert.throws(() => SqlValue.Integer(1n).AsReal());
assert.throws(() => { SqlValue.Null().Kind = "text"; });

const blobInput = Buffer.from([0, 128, 255]);
const blob = SqlValue.Blob(blobInput);
blobInput[0] = 12;
const blobOutput = blob.AsBlob();
assert.ok(blobOutput instanceof Uint8Array);
blobOutput[1] = 42;
assert.deepEqual([...blob.AsBlob()], [0, 128, 255]);
assert.deepEqual([...NumericProbe.MutateBlobCopy(blob).AsBlob()], [99, 128, 255]);
assert.deepEqual([...blob.AsBlob()], [0, 128, 255]);
assert.deepEqual([...SqlValue.Blob(new Uint8Array()).AsBlob()], []);
assert.throws(() => SqlValue.Blob([0, 1, 2]));

const negative = SqlValue.Integer(-17n), divisor = SqlValue.Integer(5n);
assert.equal(NumericProbe.Divide(negative, divisor).AsInteger(), -3n);
assert.equal(NumericProbe.Remainder(negative, divisor).AsInteger(), -2n);
assert.equal(NumericProbe.Add(SqlValue.Integer((1n << 63n) - 2n), SqlValue.Integer(1n)).AsInteger(), (1n << 63n) - 1n);
assert.equal(NumericProbe.Add(SqlValue.Integer(-(1n << 63n) + 1n), SqlValue.Integer(-1n)).AsInteger(), -(1n << 63n));
assert.equal(NumericProbe.LessThan(negative, divisor), true);
assert.equal(NumericProbe.FormatInteger(SqlValue.Integer(-(1n << 63n))), "-9223372036854775808");
assert.equal(NumericProbe.Hypotenuse(SqlValue.Real(3), SqlValue.Real(4)).AsReal(), 5);

const row = new SqlRow(["z", "x", "x"], [SqlValue.Integer(11n), SqlValue.Text("first"), SqlValue.Text("second")]);
assert.equal(typeof row.Count, "number");
assert.equal(row.Count, 3);
assert.equal(NumericProbe.CountDivision(row).AsInteger(), -1n);
assert.equal(row.GetColumnName(0), "z");
assert.equal(row.GetByName("z").AsInteger(), 11n);
assert.equal(row.Get(2).AsText(), "second");
assert.equal(row.TryGetByName("missing"), undefined);
assert.throws(() => row.GetByName("x"));
for (const invalid of [0.5, true, -1, 3]) {
  assert.throws(() => row.Get(invalid));
  assert.throws(() => row.GetColumnName(invalid));
}
assert.equal(new SqlParameter("value", SqlValue.Null()).Name, "$value");
assert.throws(() => new SqlParameter("@value", SqlValue.Null()));

if (!process.argv.includes("--values-only")) {
  const { Sqlite, Table, TableRepository } = SQLite;
  const db = Sqlite.OpenInMemory();
  try {
    const columns = ["group", 'key"part', "value"];
    const table = new Table("native values", columns, ["group", 'key"part'],
      value => [SqlValue.Text(value.group), SqlValue.Integer(value.key), SqlValue.Text(value.value)],
      result => ({group: result.Get(0).AsText(), key: result.Get(1).AsInteger(), value: result.Get(2).AsText()}));
    columns[0] = "mutated";
    const repository = new TableRepository(db, table);
    db.Execute('CREATE TABLE "native values" ("group" TEXT, "key""part" INTEGER, "value" TEXT, PRIMARY KEY ("group", "key""part"))');
    repository.Insert({group: "a", key: 9007199254740993n, value: "quoted';--"});
    const key = [SqlValue.Text("a"), SqlValue.Integer(9007199254740993n)];
    assert.deepEqual(repository.Get(key), {group: "a", key: 9007199254740993n, value: "quoted';--"});
    repository.Update({group: "a", key: 9007199254740993n, value: "updated"});
    assert.equal(repository.List()[0].value, "updated");
    repository.Delete(key);
    assert.equal(repository.Get(key), undefined);

    db.Execute("CREATE TABLE native_transaction (value)");
    assert.equal(db.WithTransaction(() => {
      db.Execute("INSERT INTO native_transaction VALUES ($value)", [new SqlParameter("value", SqlValue.Integer(7n))]);
      return "native callback result";
    }), "native callback result");
    assert.throws(() => db.WithTransaction(() => {
      db.Execute("INSERT INTO native_transaction VALUES (8)");
      throw new Error("native callback failure");
    }), /native callback failure/);
    assert.throws(() => db.WithTransaction(() => {
      db.Execute("INSERT INTO native_transaction VALUES (9)");
      return Promise.resolve("async result");
    }));
    let asyncInvoked = false;
    assert.throws(() => db.WithTransaction(async () => {
      asyncInvoked = true;
      await Promise.resolve();
      db.Execute("INSERT INTO native_transaction VALUES (10)");
    }));
    await Promise.resolve();
    assert.equal(asyncInvoked, false, "known async callback must be rejected before invocation");
    assert.equal(db.Scalar("SELECT count(*) FROM native_transaction").AsInteger(), 1n);
    for (const absent of [null, undefined]) {
      const invalidCodec = new Table("native_transaction", ["value"], ["value"],
        () => [SqlValue.Integer(7n)], () => absent);
      const invalidRepository = new TableRepository(db, invalidCodec);
      assert.equal(invalidRepository.Get([SqlValue.Integer(999n)]), undefined);
      assert.throws(() => invalidRepository.Get([SqlValue.Integer(7n)]));
      assert.throws(() => invalidRepository.List());
    }
    for (const falsy of [false, 0, ""]) {
      const validCodec = new Table("native_transaction", ["value"], ["value"],
        () => [SqlValue.Integer(7n)], () => falsy);
      const validRepository = new TableRepository(db, validCodec);
      assert.equal(validRepository.Get([SqlValue.Integer(7n)]), falsy);
      assert.deepEqual(validRepository.List(), [falsy]);
    }
    assert.equal(db.Scalar("SELECT NULL").IsNull, true);
    assert.equal(db.Scalar("SELECT 1 WHERE 0"), undefined);
    const duplicates = db.Query("SELECT 1 AS x, 2 AS x");
    assert.ok(Array.isArray(duplicates));
    assert.ok(duplicates[0] instanceof SqlRow);
    assert.ok(duplicates[0].Get(0) instanceof SqlValue);
    assert.equal(duplicates[0].Get(1).AsInteger(), 2n);
    assert.throws(() => duplicates[0].TryGetByName("x"));
    assert.throws(() => db.Execute("INSERT INTO native_transaction VALUES ($x)", [new SqlParameter("x", SqlValue.Integer(1n)), new SqlParameter("$x", SqlValue.Integer(2n))]));
  } finally { db.Close(); }
  db.Close();
  assert.throws(() => db.Query("SELECT 1"));

  const native = new Database(":memory:");
  try {
    native.pragma("foreign_keys = OFF");
    const wrapped = Sqlite.WrapConnection(native);
    assert.equal(wrapped.Scalar("PRAGMA foreign_keys").AsInteger(), 1n);
    wrapped.Execute("CREATE TABLE borrowed (value)");
    wrapped.BeginTransaction();
    wrapped.Execute("INSERT INTO borrowed VALUES (1)");
    wrapped.Close();
    assert.equal(native.pragma("foreign_keys", {simple: true}), 0);
    assert.equal(native.prepare("SELECT count(*) AS count FROM borrowed").get().count, 0);
    // Library integer reading must not change better-sqlite3's global defaults.
    assert.equal(typeof native.prepare("SELECT 1 AS value").get().value, "number");
    native.exec("BEGIN; INSERT INTO borrowed VALUES (2)");
    assert.throws(() => Sqlite.WrapConnection(native));
    assert.equal(native.inTransaction, true);
    native.exec("ROLLBACK");
    assert.equal(native.prepare("SELECT count(*) AS count FROM borrowed").get().count, 0);
  } finally { native.close(); }
}
console.log("PolyglotSQLite native JavaScript checks passed");
