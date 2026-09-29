---
title: PolyglotSQLite
category: Project
categoryindex: 2
index: 9
---

# PolyglotSQLite

`PolyglotSQLite` is an independent .NET Standard 2.0 F# library for synchronous
SQLite access on .NET, Node.js, and Python. It uses Microsoft.Data.Sqlite,
better-sqlite3, and Python's stdlib sqlite3 respectively. Native consumers use
classes and methods directly through the public packages.

The library provides values, parameters, ordered rows, connection ownership,
transactions, and generic table repositories. A future ARC SQLite implementation
will combine it with [ARCBaseModel](base-model.md) and define the ARC schema,
entity mappings, and ID policies. PolyglotSQLite itself has no ARC dependency.
The legacy ProcessCore implementation remains independent.

## Build and verify

Restore the repository's pinned tools and dependencies (`dotnet tool restore`,
`npm ci`, and `uv sync --frozen`), then run:

```powershell
.\build.cmd BuildPolyglotSQLite
.\build.cmd TestPolyglotSQLite
```

These are named FAKE targets. The build stages the ESM package `polyglot-sqlite`
under `build/out/polyglot-sqlite/js/node_modules` and the Python package
`polyglot_sqlite` under `build/out/polyglot-sqlite/python`. The generated Python
syntax requires Python 3.12 or later. JavaScript runs on Node with the installed
better-sqlite3 native dependency; browser engines are outside this implementation.
Artifacts are local and are not published to registries.

The individual test targets are `TestPolyglotSQLiteDotNet`,
`TestPolyglotSQLiteJS`, `TestPolyglotSQLitePy`, `TestPolyglotSQLiteNative`, and
`TestPolyglotSQLiteInterop`. The aggregate includes shared behavior tests,
handwritten native consumers, TypeScript checks, local packed-artifact checks,
and every combination of .NET/Node/Python database writer and reader.
`RunTests` includes this suite independently of the disabled legacy Python suite.

## Execute SQL

Reference `src/PolyglotSQLite/PolyglotSQLite.fsproj` from F#:

```fsharp
open PolyglotSQLite

use connection = Sqlite.OpenInMemory()
connection.Execute("CREATE TABLE measurements (id INTEGER PRIMARY KEY, value)")
connection.WithTransaction(fun () ->
    connection.Execute("INSERT INTO measurements VALUES ($id, $value)",
        [ SqlParameter("id", SqlValue.Integer(1L))
          SqlParameter("value", SqlValue.Real(23.5)) ]))
let value = connection.Scalar("SELECT value FROM measurements WHERE id = $id",
                [ SqlParameter("id", SqlValue.Integer(1L)) ])
assert (value.Value.AsReal() = 23.5)
```

JavaScript uses native bigint for SQLite INTEGER:

```javascript
import { Sqlite, SqlParameter, SqlValue } from "polyglot-sqlite";

const connection = Sqlite.OpenInMemory();
try {
  connection.Execute("CREATE TABLE measurements (id INTEGER PRIMARY KEY, value)");
  connection.WithTransaction(() => {
    connection.Execute("INSERT INTO measurements VALUES ($id, $value)", [
      new SqlParameter("id", SqlValue.Integer(1n)),
      new SqlParameter("value", SqlValue.Real(23.5)),
    ]);
  });
  const value = connection.Scalar("SELECT value FROM measurements WHERE id = $id", [
    new SqlParameter("id", SqlValue.Integer(1n)),
  ]);
  if (value.AsReal() !== 23.5) throw new Error("Unexpected measurement");
} finally {
  connection.Close();
}
```

Python uses ordinary integers, floats, bytes, and lists:

```python
from polyglot_sqlite import Sqlite, SqlParameter, SqlValue

connection = Sqlite.OpenInMemory()
try:
    connection.Execute("CREATE TABLE measurements (id INTEGER PRIMARY KEY, value)")
    connection.WithTransaction(lambda: connection.Execute(
        "INSERT INTO measurements VALUES ($id, $value)",
        [SqlParameter("id", SqlValue.Integer(1)),
         SqlParameter("value", SqlValue.Real(23.5))]))
    value = connection.Scalar("SELECT value FROM measurements WHERE id = $id",
                             [SqlParameter("id", SqlValue.Integer(1))])
    assert type(value.AsReal()) is float
    assert value.AsReal() == 23.5
finally:
    connection.Close()
```

Parameter names follow `[A-Za-z_][A-Za-z0-9_]*`, with an optional leading `$` in
the constructor. SQL uses `$name`. Duplicate normalized names are rejected;
positional parameters and `@name`/`:name` placeholders are unsupported.

`Execute`, `Query`, and `Scalar` accept one statement, with or without parameters.
Strings, comments, and trigger bodies may contain semicolons. Use `ExecuteScript`
for scripts. Statement validation occurs before execution, including before
PRAGMAs that can take effect during preparation.

## Values and rows

| Storage class | Factory | Native JS value | Native Python value |
|---|---|---|---|
| NULL | `SqlValue.Null()` | Explicit wrapper | Explicit wrapper |
| TEXT | `SqlValue.Text(value)` | string | str |
| INTEGER | `SqlValue.Integer(value)` | bigint | int |
| REAL | `SqlValue.Real(value)` | number | float |
| BLOB | `SqlValue.Blob(value)` | Uint8Array; Buffer input accepted | bytes; bytearray input accepted |

Values expose `Kind`, `IsNull`, and strict `AsText`, `AsInteger`, `AsReal`, and
`AsBlob` readers. A wrong-kind reader throws. INTEGER uses the full signed 64-bit
range without truncation; Boolean numeric inputs and NaN are rejected. SQLite
column affinity may change the stored type. Reads report the actual SQLite
storage class. BLOB factories and readers copy mutable buffers.

`Query` returns an array/list of `SqlRow` objects. Rows preserve column order and
duplicate names. Use `Get(index)` and `GetColumnName(index)` for ordinal access;
`GetByName` and `TryGetByName` require an unambiguous exact name. `Count` reports
the number of columns. `Scalar` always selects column zero of row zero. A query
with no rows returns F# `None`, JavaScript `undefined`, or Python `None`; a row
containing SQL NULL returns a present `SqlValue` with `IsNull = true`.

Python methods adapt numeric and byte-array representations at explicit
boundaries. Transpiled F# retains the compiler's arithmetic semantics, while
native callers receive genuine Python primitives. Both use the same classes;
consumers do not import Fable helpers or run the ARCBaseModel compatibility pass.

## Transactions and ownership

`WithTransaction` accepts a synchronous callback, returns its result, commits
on success, and rolls back on failure. Awaitable callback results are rejected.
Known native async functions are rejected before invocation. Callbacks must
finish their database work before returning and must not schedule deferred work.
`BeginTransaction` returns a scope with `Commit`, `Rollback`, and `Close`;
closing an unfinished scope rolls it back. F# scopes and connections implement
`IDisposable`. Native callers use `Close` in a `finally` block.

Scopes finish in reverse creation order. Nested scopes use savepoints; an outer
rollback also undoes previously completed inner scopes. Ordinary SQL execution
never commits a surrounding transaction. Use the scope methods for transaction
control instead of sending raw BEGIN/COMMIT/ROLLBACK/SAVEPOINT statements through
ordinary execution methods.

If SQLite aborts a callback's transaction, catching the SQL exception does not
restore it. Further operations are rejected until the callback exits, preventing
later writes from accidentally running outside the transaction. A failed cleanup
makes ordinary operations unavailable; `Close` can retry rollback of unfinished
library-owned work before releasing the handle.

`ExecuteScript` runs only when no transaction is active and does not promise
atomicity. A script can contain its own complete transaction. On failure or an
unfinished transaction, the library rolls back the script's remaining active
work; earlier committed statements remain committed.

Factories own and close the handles they open. `WrapConnection` borrows an
already-open, idle native handle and leaves it open on release. It enables
foreign keys and restores settings it changes. Use the handle exclusively
through the wrapper until `Close`; do not retain active native cursors or start
external transactions. Unexpected external transactions are never committed or
rolled back by wrapper release.

Borrowed Python handles must use `detect_types=0` and `text_factory=str`.
Python does not expose a public getter for `detect_types`, so that flag is a
caller precondition; the wrapper cannot verify it. Custom text factories and
unsupported converted scalar types are rejected. Wrapper-created cursors use
positional rows without changing the connection's row factory.

## Generic table repositories

`Table<T>(name, columns, primaryKey, encode, decode)` describes a caller-owned
table. The encoder returns values in declared column order; the decoder receives
a `SqlRow`. `TableRepository<T>(connection, table)` offers `Insert`, `Update`,
`Delete`, `Get`, and `List` and accepts native codec callbacks and objects.
Decoders must return a non-null row value; `Get` reserves native absence for a
missing database row. False, zero, and empty text are valid decoded values.

Metadata is copied and validated, and getters return copies. Names must be
nonempty and contain no NUL. Duplicate columns/keys are rejected ignoring case;
key entries must match the declared column spelling exactly. SQL identifiers are
quoted and binding names are generated independently of them. Case comparison
folds ASCII letters like SQLite; non-ASCII names retain their spelling. Encoder
and key value counts are checked before SQL execution.

Keys are supplied in declared key order. Updates exclude keys and reject tables
with no remaining columns. Missing updates and deletes are no-ops. `Get` returns
absence when missing and rejects multiple matches. `List` orders by the declared
primary key. Schema creation, upserts, identity resolution, and domain validation
remain the caller's responsibility.
Key comparisons support SQL NULL where the caller's schema permits it; SQLite
can allow repeated NULL-containing keys, so `Get` still rejects ambiguous matches.
