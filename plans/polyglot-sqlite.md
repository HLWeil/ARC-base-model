# PolyglotSQLite: Portable SQLite Infrastructure

## Objective and implementation state

Create an independent **PolyglotSQLite** library under `src/PolyglotSQLite`, targeting **netstandard2.0**, with directly usable F#, JavaScript/TypeScript, and Python APIs.

Adapt the useful driver, connection-management, parameter-binding, and generic CRUD code from ProcessCore. All repairs below apply to **PolyglotSQLite**. ProcessCore remains self-contained and receives no dependency on the new library.

Reuse the pinned Fable toolchain, Microsoft.Data.Sqlite, better-sqlite3, Python stdlib sqlite3, and existing build/test tools. No new production dependencies or compiler upgrades are planned. ARC schemas, mappings, ID policies, identity maps, migrations, browser support, asynchronous operations, and package publication remain outside scope.

Update this plan in **every implementation commit**, recording changes, checks actually executed, failures, and deviations.

| Milestone | State | Completion evidence |
|---|---|---|
| Plan baseline | Complete | Accepted plan saved before implementation; initial commit records this baseline. |
| Independent library and portable value API | Not started | Builds and native/transpiled F# boundary checks pending. |
| Runtime adapters and transactions | Not started | Driver and transaction tests on all three runtimes pending. |
| Generic table repositories | Not started | CRUD tests and native codec callbacks pending. |
| Native artifacts and interoperability | Not started | Public entrypoints, declarations, consumers, and database-file exchange pending. |
| Build integration and documentation | Not started | Aggregate targets, CI integration, documentation, and final verification pending. |

### Commit and verification log

- Planning checkpoint: inspected legacy implementations, dependency compatibility, and build integration. Isolated probes verified the Python representation approach. No tracked implementation changes were made during planning.
- Repair-register revision: identified the concrete legacy behaviors below and assigned acceptance checks. These findings do not establish that repairs have been implemented.
- Plan baseline: saved the accepted plan with all implementation milestones and repairs pending. Working tree was clean before creating this document.

### Intended commits

1. `docs: plan PolyglotSQLite implementation`
2. `feat: add portable SQLite value and row API`
3. `feat: add SQLite adapters and transaction scopes`
4. `feat: add generic SQLite table repositories`
5. `feat: add native PolyglotSQLite artifacts and interoperability tests`
6. `chore: integrate PolyglotSQLite builds and documentation`

Include relevant tests with each behavior change. Larger commits are acceptable when necessary for a coherent change. Update repair statuses and cite their IDs in the verification log.

## Explicit repair register

Some entries are correctness defects; others are deliberate legacy restrictions or assumptions that the new contract must replace. The legacy three-storage-class/int32 contract was intentional.

| ID | Legacy issue and location | Required repair and acceptance check | State |
|---|---|---|---|
| **R01 — Unconditional Python commits** | `PythonSqliteDriver.Execute` calls `connection.commit()` after every operation. Foreign-key initialization also uses this path, including when wrapping an existing connection. This prevents caller-controlled transactions. | Ordinary execution never commits an enclosing transaction. Multiple parameterized and parameterless writes roll back together. Wrapping an active connection rejects it without committing its pending work. | Pending |
| **R02 — Statement/script behavior depends on parameters** | JavaScript and Python route parameterless `Execute` through script APIs, while parameterized calls use single statements. .NET permits command batches. | Separate single-statement execution from `ExecuteScript`. Batch rejection and transaction behavior must not depend on whether a parameter collection is empty. Test comments, quoted semicolons, triggers, and trailing statements. | Pending |
| **R03 — Python scalar selects the wrong column** | `PythonSqliteDriver.Scalar` converts the first row's map to an array and selects its first entry, which follows column-name ordering rather than SELECT order. | Read column ordinal zero. `SELECT 11 AS z, 22 AS a` returns integer `11`. | Pending |
| **R04 — JavaScript scalar can select the wrong column** | `BetterSqliteDriver.Scalar` chooses the first `Object.keys` entry. Integer-like column names can be reordered by JavaScript property enumeration. | Use positional results and column metadata. `SELECT 'first' AS "10", 'second' AS "2"` returns `"first"`. | Pending |
| **R05 — Restricted and inconsistent integer handling** | `SqlValue.Int` is int32; .NET and JavaScript explicitly reject larger integers, while Python lacks the corresponding range check. JavaScript converts numeric results through `Number`. | Adopt one signed int64 contract: F# `int64`, JS `bigint`, Python `int`. Verify exact values above `2^53`, both int64 limits, and rejection of out-of-range factory inputs before conversion. | Pending |
| **R06 — REAL/BLOB coercion loses storage information** | Outside the legacy supported types, JavaScript converts fractional numbers to integers; .NET/Python fall back to text for REAL values. BLOBs also fall through text conversion paths. | Add explicit REAL and BLOB cases without fallback stringification or truncation. Verify fractional values, REAL `1.0` versus INTEGER `1` in affinity-free storage, and exact binary round trips including empty BLOBs. | Pending |
| **R07 — Rows lose order and duplicate columns** | `SqlRow` is a map; driver conversions discard SELECT order and collapse duplicate names. JavaScript object rows can lose duplicate columns before map construction. | Preserve ordered names and values. `SELECT 1 AS x, 2 AS x` retains both values by ordinal; name-based lookup reports ambiguity. | Pending |
| **R08 — Missing scalar rows are indistinguishable from NULL** | The scalar contract returns `SqlValue.Null` for both cases. This is an existing API limitation. | Return optional `SqlValue`: `SELECT NULL` yields a present NULL value; `SELECT 1 WHERE 0` yields absence. | Pending |
| **R09 — Parameter normalization collisions** | JavaScript/Python strip parameter sigils and build objects/dictionaries, allowing normalized duplicates to overwrite each other. .NET handles supplied sigils differently. | Adopt canonical `$name` binding and reject duplicate normalized names before SQL execution. Parameters named `x` and `$x` must fail consistently on all runtimes. | Pending |
| **R10 — CRUD assumes simple, trusted identifiers** | `Repository.Crud` interpolates unquoted table/column/key names and derives placeholders directly from column names. This is a limitation of the fixed-catalogue helpers. | Quote identifiers and generate independent placeholders. CRUD must work with reserved words, spaces, and embedded double quotes in identifiers. | Pending |
| **R11 — Generic metadata and encoder shape are unchecked** | `Repository.Table` retains mutable metadata arrays without validation or copying. CRUD forwards encoder parameters without validating their shape against declared columns. | Copy and validate metadata; reject invalid keys, duplicate/empty columns, and incorrect encoder value counts before SQL. Mutating caller-owned metadata arrays must not change an existing table definition. | Pending |

Full storage-class support and the native class API are contract expansions. Do not describe them as accidental violations of requirements the legacy library never promised.

## Public API and behavior

### Values, parameters, and rows

Use attached classes, explicit backing fields, distinct member names, and tupled arguments. Native consumers must not construct F# union tags, records, maps, or option wrappers.

`SqlValue` is immutable, with factories `Null()`, `Text(string)`, `Integer(int64)`, `Real(float)`, and `Blob(byte[])`. Expose `Kind`, `IsNull`, and strict `AsText`, `AsInteger`, `AsReal`, and `AsBlob` readers. Wrong-kind readers throw.

Validate signed int64 bounds before conversion. Reject Boolean numeric inputs and NaN; preserve supported infinities. Decode actual SQLite storage classes without application-level coercion; SQLite column affinity still applies.

BLOB boundaries use F# byte arrays, JavaScript `Uint8Array` with Buffer inputs accepted, and Python bytes with bytearray inputs accepted. Copy mutable inputs and returned buffers.

`SqlParameter(name, value)` accepts simple identifier-style names, optionally prefixed with `$`, and normalizes them. Reject normalized duplicates. Positional parameters are deferred.

`SqlRow(columnNames, values)` copies its inputs and preserves ordered, duplicate columns. Expose `Count`, `GetColumnName`, ordinal `Get`, `GetByName`, and `TryGetByName`. Name matching is exact; ambiguous names throw and missing optional lookup returns ordinary native absence.

### Python representation boundary

Preserve native usability and correct transpiled F# semantics on the same class. Use Python-only `CompiledName` adaptation for numeric and BLOB readers:

- Natural F# calls compile to internal readers returning Fable representations.
- Native `AsInteger`, `AsReal`, and `AsBlob` return Python `int`, `float`, and `bytes`.
- Factories normalize supported native inputs after validation.
- Internal static factory and instance reader names remain distinct.

Do not return native primitives directly from methods whose transpiled F# callers require numeric wrappers. Verify arithmetic and native consumers before expanding the library. Keep adaptation at explicit boundaries. Do not reuse ARCBaseModel's model-specific AST pass or modify the Fable runtime.

### Connections and execution

Expose `Sqlite.OpenFile(path)`, `OpenInMemory()`, and platform-appropriate `WrapConnection(nativeConnection)`, returning `SqliteConnection`.

| Operation | Contract |
|---|---|
| `Execute(sql, ?parameters)` | One statement; returns unit. |
| `Query(sql, ?parameters)` | Materialized ordered rows. |
| `Scalar(sql, ?parameters)` | Optional first-column value from the first row. |
| `ExecuteScript(sql)` | Unparameterized script outside an active transaction. |
| `BeginTransaction()` | Explicit transaction scope. |
| `WithTransaction(action)` | Synchronous callback returning its result. |
| `Close()` | Release wrapper and owned resources. |

Keep provider details behind an internal driver abstraction and preserve provider exceptions. Ordinary execution never commits an enclosing transaction. Enforce single-statement boundaries using SQLite-aware detection, not semicolon splitting. Reject raw transaction-control statements through ordinary execution methods.

### Ownership and transactions

Owned connections enable foreign keys and close on disposal. Closure is idempotent; subsequent operations fail.

Borrowed handles must already be open and idle. Reject active external transactions before changing settings. Require exclusive use during the wrapper's lifetime, restore changed settings after its work ends, and leave the handle open. Unsupported Python converter configurations must not silently alter storage representations.

Use explicit SQL transaction commands. Configure Python to avoid implicit transactions and per-operation commits.

`SqliteTransaction` exposes `Commit`, `Rollback`, and `Close`; disposing unfinished scopes rolls them back. Enforce scope order and implement nested scopes with generated savepoints. Inner completion remains subject to outer rollback.

`WithTransaction` commits on success and rolls back on callback or commit failure. Preserve the original failure if cleanup also fails. Reject awaitable callback results and detect backend-aborted transactions.

`ExecuteScript` requires managed and native transaction state to be idle and makes no atomicity guarantee. If execution fails or leaves a transaction open, roll back that script-owned remainder and report failure. Earlier committed statements remain committed.

### Generic repositories

Provide immutable `Table<'T>` metadata containing the table name, ordered columns, ordered primary key, encoder, and decoder. The encoder returns values in declared column order; the decoder receives `SqlRow`. Native callers can supply ordinary callbacks and objects.

`TableRepository<'T>` exposes `Insert`, `Update`, `Delete`, `Get`, and `List`.

- Validate metadata, key membership, key argument counts, and encoded value counts.
- Quote identifiers and generate placeholders independently.
- Updates exclude primary-key columns and reject tables without updatable columns.
- Missing updates/deletes are no-ops.
- `Get` returns absence when missing and rejects multiple rows.
- `List` orders by the declared primary key.

Do not introduce schema generation, upsert policies, change tracking, or identity handling.

## Delivery and integration

Create one production project with a shared compile list and conditional adapters, plus an independent net10.0 Pyxpecto test project. Register both in the solution and keep release packaging disabled initially.

Stage artifacts under `build/out/polyglot-sqlite`: ESM package `polyglot-sqlite` with curated exports and precise TypeScript declarations; Python package `polyglot_sqlite` with curated exports, `.pyi` declarations, and `py.typed`.

Native and transpiled callers share class definitions. Hide internal compatibility methods from the documented surface and clean only dedicated generated outputs.

Add named FAKE targets `BuildPolyglotSQLite`, `TestPolyglotSQLiteDotNet`, `TestPolyglotSQLiteJS`, `TestPolyglotSQLitePy`, `TestPolyglotSQLiteNative`, `TestPolyglotSQLiteInterop`, and `TestPolyglotSQLite`. Targets depend on their required artifacts. Include the test aggregate in `RunTests`, which existing CI invokes. The new Python suite remains independent of the disabled legacy suite.

Add executable F#, JavaScript, and Python documentation examples and update contributor guidance with the library boundary, commands, and Python representation rules.

## Verification and completion

Use independent generic schemas rather than the legacy ARC schema. Every repair-register entry requires a corresponding regression test and recorded passing evidence before its status becomes Complete.

Also verify strict value readers, Boolean/NaN rejection, infinities, buffer copying, and native return types; F# arithmetic using two returned values (negative division/remainder, integer boundaries, comparisons, formatting, floating-point math, and byte-array mutation); owned/borrowed lifecycle, foreign keys, use after closure, and rejected external transactions; explicit/callback commit and rollback, nested scopes, outer rollback after inner completion, backend aborts, and script cleanup; composite-key CRUD, native codec callbacks, and validation before side effects; strict TypeScript valid/invalid calls, Python declarations, public imports, and shared class identity; and database files written by each runtime and read by all three runtimes.

Completion requires passing `TestPolyglotSQLite`, solution build, `TestBaseModel`, final `RunTests` integration verification, and an fsdocs build or targeted documentation/link check. Record actual results and remaining warnings. Compilation alone, skipped tests, and feasibility probes do not establish completion.
