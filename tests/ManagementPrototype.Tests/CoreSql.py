"""Core schema checks through the existing staged PolyglotSQLite package.

Use --rebuild to regenerate the committed seed database from the two SQL files.
"""
from pathlib import Path
import sys
import sqlite3
import re

root = Path(__file__).resolve().parents[2]
schema = (root / "schemas/sql/001_core.sql").read_text(encoding="utf-8")
seed = (root / "schemas/sql/seed_example.sql").read_text(encoding="utf-8")

database = root / "schemas/sql/seeded_core.sqlite"
if "--rebuild" in sys.argv:
    assert database.resolve().parent == (root / "schemas/sql").resolve()
    if database.exists():
        database.unlink()
    with sqlite3.connect(database) as connection:
        connection.executescript(schema)
        connection.executescript(seed)
        connection.execute("VACUUM")

with sqlite3.connect(f"file:{database.as_posix()}?mode=ro",uri=True) as connection:
    assert connection.execute("PRAGMA integrity_check").fetchall() == [("ok",)]
    assert connection.execute("PRAGMA foreign_key_check").fetchall() == []
    assert connection.execute("SELECT * FROM core_validation_errors").fetchall() == []

for document in ("schemas/sql/README.md","schemas/sql/design.md","docs/project/implementation.md"):
    file = root / document
    for target in re.findall(r"\[[^]]*\]\(([^)]+)\)",file.read_text(encoding="utf-8")):
        if "://" not in target and not target.startswith("#"):
            assert (file.parent / target.split("#")[0]).exists(), (document,target)

if "--rebuild" in sys.argv:
    print("Seed database rebuilt; integrity, foreign keys, profile validation and documentation links passed.")
    sys.exit(0)

from polyglot_sqlite import Sqlite
cases = (root / "tests/ManagementPrototype.Tests/CoreSql.cases.tsv").read_text(encoding="utf-8").splitlines()
for line in cases:
    kind, statement, expected = line.split("\t")
    sql = Sqlite.OpenInMemory()
    try:
        sql.ExecuteScript(schema)
        sql.ExecuteScript(seed)
        if kind == "crud":
            commands = statement.split(";")
            for command in commands[:-1]:
                sql.Execute(command)
            assert sql.Scalar(commands[-1]).AsText() == expected, statement
            assert len(sql.Query("SELECT * FROM core_validation_errors")) == 0
        elif kind == "reject":
            try:
                sql.Execute(statement)
            except Exception:
                pass
            else:
                raise AssertionError(statement)
        elif kind in ("accept","invalid"):
            sql.Execute(statement)
            errors = sql.Query("SELECT * FROM core_validation_errors")
            assert (len(errors) == 0) == (kind == "accept"), statement
        else:
            value = sql.Scalar(statement)
            if kind == "integer":
                assert value.AsInteger() == int(expected), statement
            elif kind == "real":
                assert type(value.AsReal()) is float
                assert value.AsReal() == float(expected), statement
            elif kind == "text":
                assert value.AsText() == expected, statement
            elif kind == "null":
                assert value.IsNull, statement
            else:
                raise AssertionError(kind)
        assert len(sql.Query("PRAGMA foreign_key_check")) == 0
    finally:
        sql.Close()
print(f"{len(cases)} core SQL cases passed on Python using PolyglotSQLite; artifact and documentation checks passed.")
