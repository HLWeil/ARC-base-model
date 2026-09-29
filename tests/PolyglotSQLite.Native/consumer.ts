// Type-check the curated public entrypoint; this file is deliberately not run.
import { SqlValue, SqlParameter, SqlRow, Sqlite, SqliteConnection, SqliteTransaction, Table, TableRepository } from "polyglot-sqlite";
import { NumericProbe } from "polyglot-sqlite-probe";

const integer: bigint = SqlValue.Integer(9007199254740993n).AsInteger();
const real: number = SqlValue.Real(0.125).AsReal();
const text: string = SqlValue.Text("").AsText();
const blob: Uint8Array = SqlValue.Blob(new Uint8Array([0, 255])).AsBlob();
const parameter = new SqlParameter("name", SqlValue.Text("bound"));
const row = new SqlRow(["id"], [SqlValue.Integer(1n)]);
const count: number = row.Count;
const maybeValue: SqlValue | undefined = row.TryGetByName("missing");
const probeResult: SqlValue = NumericProbe.Divide(SqlValue.Integer(-17n), SqlValue.Integer(5n));

const db: SqliteConnection = Sqlite.OpenInMemory();
db.Execute("SELECT $name", [parameter]);
const rows: SqlRow[] = db.Query("SELECT 1");
const scalar: SqlValue | undefined = db.Scalar("SELECT 1");
const scope: SqliteTransaction = db.BeginTransaction();
scope.Rollback();
scope.Close();
const callback: string = db.WithTransaction(() => "returned");

type NativeRow = { id: bigint; name: string };
const table = new Table<NativeRow>("table", ["id", "name"], ["id"],
  value => [SqlValue.Integer(value.id), SqlValue.Text(value.name)],
  value => ({ id: value.Get(0).AsInteger(), name: value.Get(1).AsText() }));
// @ts-expect-error A codec accepting a required name cannot accept a wider row type.
const widened: Table<{ id: bigint; name: string | undefined }> = table;
// @ts-expect-error Present rows must decode to a non-absent value.
new Table<null>("invalid", ["id"], ["id"], () => [SqlValue.Integer(1n)], () => null);
const falseRows = new Table<boolean>("boolean_rows", ["id"], ["id"], () => [SqlValue.Integer(1n)], () => false);
const repository = new TableRepository(db, table);
repository.Insert({ id: 1n, name: "native" });
repository.Update({ id: 1n, name: "updated" });
const maybeRow: NativeRow | undefined = repository.Get([SqlValue.Integer(1n)]);
const items: NativeRow[] = repository.List();
repository.Delete([SqlValue.Integer(1n)]);
db.Close();

// @ts-expect-error INTEGER requires bigint rather than an imprecise number.
SqlValue.Integer(1);
// @ts-expect-error REAL does not accept bigint.
SqlValue.Real(1n);
// @ts-expect-error A BLOB is an explicit byte buffer.
SqlValue.Blob([0, 255]);
// @ts-expect-error Native callers cannot construct implementation union state.
new SqlValue("integer", 1n);
// @ts-expect-error Values are immutable.
row.Count = 0;
// @ts-expect-error Parameters bind SqlValue objects.
new SqlParameter("name", "unwrapped");
// @ts-expect-error Scalar's missing-row result must be handled.
const nonoptional: SqlValue = db.Scalar("SELECT 1 WHERE 0");
// @ts-expect-error Row codec enforces the caller's row type.
repository.Insert({ id: "wrong", name: "native" });
// @ts-expect-error Keys use typed SQL values.
repository.Get([1n]);
// @ts-expect-error Transactions require synchronous callbacks.
db.WithTransaction(async () => "later");

void [integer, real, text, blob, count, maybeValue, probeResult, rows, scalar, callback, maybeRow, items, nonoptional, widened, falseRows];
