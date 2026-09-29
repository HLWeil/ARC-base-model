"""Stage and locally pack generated classes without changing their behavior.

Only test import paths are rewritten, so F# consumers and native callers use
the identical public classes. Numeric representation adaptation lives in F#
CompiledName boundaries, not in an AST compatibility pass or runtime patch.
"""

from __future__ import annotations

import argparse
import ast
import base64
import csv
import hashlib
import io
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tomllib
import zipfile


REPOSITORY = Path(__file__).resolve().parent.parent
OUTPUT = REPOSITORY / "build/out/polyglot-sqlite"
HEADER = """from __future__ import annotations
from collections.abc import Callable, Iterable
from typing import Generic, Literal, Never, TypeVar
import sqlite3

T = TypeVar("T")
"""
CONTRACTS = {
    "SqlValue": '''class SqlValue:
    def __init__(self, _internal: Never) -> None: ...
    @staticmethod
    def Null() -> SqlValue: ...
    @staticmethod
    def Text(value: str) -> SqlValue: ...
    @staticmethod
    def Integer(value: int) -> SqlValue: ...
    @staticmethod
    def Real(value: float) -> SqlValue: ...
    @staticmethod
    def Blob(value: bytes | bytearray) -> SqlValue: ...
    @property
    def Kind(self) -> Literal["null", "text", "integer", "real", "blob"]: ...
    @property
    def IsNull(self) -> bool: ...
    def AsText(self) -> str: ...
    def AsInteger(self) -> int: ...
    def AsReal(self) -> float: ...
    def AsBlob(self) -> bytes: ...''',
    "SqlParameter": '''class SqlParameter:
    def __init__(self, name: str, value: SqlValue) -> None: ...
    @property
    def Name(self) -> str: ...
    @property
    def Value(self) -> SqlValue: ...''',
    "SqlRow": '''class SqlRow:
    def __init__(self, column_names: Iterable[str], values: Iterable[SqlValue]) -> None: ...
    @property
    def Count(self) -> int: ...
    def GetColumnName(self, index: int) -> str: ...
    def Get(self, index: int) -> SqlValue: ...
    def GetByName(self, name: str) -> SqlValue: ...
    def TryGetByName(self, name: str) -> SqlValue | None: ...''',
    "Sqlite": '''class Sqlite:
    def __init__(self, _internal: Never) -> None: ...
    @staticmethod
    def OpenFile(path: str) -> SqliteConnection: ...
    @staticmethod
    def OpenInMemory() -> SqliteConnection: ...
    @staticmethod
    def WrapConnection(native_connection: sqlite3.Connection) -> SqliteConnection: ...''',
    "SqliteConnection": '''class SqliteConnection:
    def __init__(self, _internal: Never) -> None: ...
    def Execute(self, sql: str, parameters: Iterable[SqlParameter] | None = None) -> None: ...
    def Query(self, sql: str, parameters: Iterable[SqlParameter] | None = None) -> list[SqlRow]: ...
    def Scalar(self, sql: str, parameters: Iterable[SqlParameter] | None = None) -> SqlValue | None: ...
    def ExecuteScript(self, sql: str) -> None: ...
    def BeginTransaction(self) -> SqliteTransaction: ...
    def WithTransaction(self, action: Callable[[], T]) -> T: ...
    def Close(self) -> None: ...''',
    "SqliteTransaction": '''class SqliteTransaction:
    def __init__(self, _internal: Never) -> None: ...
    def Commit(self) -> None: ...
    def Rollback(self) -> None: ...
    def Close(self) -> None: ...''',
    "Table": '''class Table(Generic[T]):
    def __init__(self, name: str, columns: Iterable[str], primary_key: Iterable[str], encode: Callable[[T], list[SqlValue]], decode: Callable[[SqlRow], T]) -> None: ...
    @property
    def Name(self) -> str: ...
    @property
    def Columns(self) -> list[str]: ...
    @property
    def PrimaryKey(self) -> list[str]: ...''',
    "TableRepository": '''class TableRepository(Generic[T]):
    def __init__(self, connection: SqliteConnection, table: Table[T]) -> None: ...
    def Insert(self, row: T) -> None: ...
    def Update(self, row: T) -> None: ...
    def Delete(self, key_values: Iterable[SqlValue]) -> None: ...
    def Get(self, key_values: Iterable[SqlValue]) -> T | None: ...
    def List(self) -> list[T]: ...''',
}


def validate_stub(source: str, filename: str) -> None:
    parsed = ast.parse(source, filename=filename)
    compile(parsed, filename, "exec")
    if any(isinstance(node, ast.Name) and node.id == "Any" for node in ast.walk(parsed)):
        raise ValueError(f"Unresolved Any annotation in {filename}")
    if "fable_library" in source:
        raise ValueError(f"Fable implementation types leaked into {filename}")


def clean_generated(directory: Path) -> None:
    directory = directory.resolve()
    if not directory.is_relative_to(OUTPUT.resolve()) or directory == OUTPUT.resolve():
        raise ValueError(f"Refusing to clean outside {OUTPUT}")
    if directory.exists():
        shutil.rmtree(directory)
    directory.mkdir(parents=True)


def checked_contract(name: str, generated: ast.ClassDef) -> str:
    declared = ast.parse(CONTRACTS[name]).body[0]
    actual_members = {node.name: node for node in generated.body if isinstance(node, ast.FunctionDef)}
    for method in declared.body:
        if not isinstance(method, ast.FunctionDef):
            continue
        if method.name == "__init__" and any(argument.arg == "_internal" for argument in method.args.args):
            continue
        if method.name not in actual_members:
            raise ValueError(f"Generated {name} is missing declared member {method.name}")
        actual = actual_members[method.name]
        arguments = [argument for argument in actual.args.args if argument.arg != "__unit"]
        if len(arguments) != len(method.args.args):
            raise ValueError(f"Generated {name}.{method.name} calling convention changed")
        if [argument.arg for argument in arguments] != [argument.arg for argument in method.args.args]:
            raise ValueError(f"Generated {name}.{method.name} keyword names differ from the public contract")
        for decorator in ("property", "staticmethod"):
            contains = lambda node: any(isinstance(value, ast.Name) and value.id == decorator for value in node.decorator_list)
            if contains(method) != contains(actual):
                raise ValueError(f"Generated {name}.{method.name} changed its {decorator} contract")
    return CONTRACTS[name]


def stage(source: Path, package_root: Path) -> None:
    source = source.resolve(strict=True)
    destination = package_root.resolve() / "polyglot_sqlite"
    modules = {}
    classes = {}
    generic_aliases = {"Table_1": "Table", "TableRepository_1": "TableRepository"}
    for filename in sorted(source.glob("*.py")):
        parsed = ast.parse(filename.read_text(encoding="utf-8-sig"), filename=str(filename))
        for node in parsed.body:
            if isinstance(node, ast.ClassDef):
                public_name = generic_aliases.get(node.name, node.name)
                if public_name not in CONTRACTS:
                    continue
                if public_name in classes:
                    raise ValueError(f"Duplicate public class {public_name}")
                classes[public_name] = node
                modules[public_name] = (filename.stem, node.name)
    for required in CONTRACTS:
        if required not in classes:
            raise ValueError(f"Missing generated class {required}")
    stub = HEADER + "\n\n" + "\n\n".join(checked_contract(name, node) for name, node in classes.items()) + "\n"
    validate_stub(stub, "polyglot_sqlite/__init__.pyi")
    destination.mkdir(parents=True, exist_ok=True)
    for filename in source.glob("*.py"):
        if filename.name != "__init__.py":
            shutil.copy2(filename, destination / filename.name)
    entry = "\n".join(f"from .{module} import {generated_name} as {name}" for name, (module, generated_name) in modules.items())
    entry += f"\n\n__all__ = {tuple(classes)!r}\n"
    (destination / "__init__.py").write_text(entry, encoding="utf-8")
    (destination / "__init__.pyi").write_text(stub, encoding="utf-8")
    (destination / "py.typed").write_text("", encoding="utf-8")
    print(f"Staged {len(classes)} PolyglotSQLite public classes and checked Python declarations.")


class SharedLibraryImports(ast.NodeTransformer):
    def visit_ImportFrom(self, node: ast.ImportFrom) -> ast.ImportFrom:
        prefix = "src.PolyglotSQLite"
        if node.module == prefix or (node.module and node.module.startswith(prefix + ".")):
            node.module = "polyglot_sqlite" + node.module[len(prefix):]
        return node


def probe_stub(parsed: ast.Module) -> str:
    probe = next(node for node in parsed.body if isinstance(node, ast.ClassDef) and node.name == "NumericProbe")
    methods = []
    allowed = {"SqlValue", "SqlRow", "str", "bool", "int", "float", "bytes"}
    for method in probe.body:
        if not isinstance(method, ast.FunctionDef) or method.name.startswith("_"):
            continue
        if not any(isinstance(node, ast.Name) and node.id == "staticmethod" for node in method.decorator_list):
            raise ValueError(f"Expected static NumericProbe.{method.name}")
        def annotation(node: ast.expr) -> str:
            result = ast.unparse(node)
            if result not in allowed:
                raise ValueError(f"Unexpected NumericProbe annotation {result}")
            return result
        args = ", ".join(f"{argument.arg}: {annotation(argument.annotation)}" for argument in method.args.args if argument.arg != "__unit")
        methods.append(f"    @staticmethod\n    def {method.name}({args}) -> {annotation(method.returns)}: ...")
    result = "from polyglot_sqlite import SqlValue, SqlRow\n\nclass NumericProbe:\n" + "\n".join(methods) + "\n"
    validate_stub(result, "polyglot_sqlite_probe/__init__.pyi")
    return result


def stage_tests(source: Path, package_root: Path) -> None:
    source = source.resolve(strict=True)
    # Repoint all top-level shared test modules; duplicate generated library
    # files remain unused and cannot introduce a second public class identity.
    for filename in source.glob("*.py"):
        parsed = ast.parse(filename.read_text(encoding="utf-8-sig"), filename=str(filename))
        parsed = ast.fix_missing_locations(SharedLibraryImports().visit(parsed))
        text = ast.unparse(parsed) + "\n"
        compile(text, str(filename), "exec")
        filename.write_text(text, encoding="utf-8")
    filename = source / "numeric_probe.py"
    parsed = ast.parse(filename.read_text(encoding="utf-8"), filename=str(filename))
    destination = package_root.resolve() / "polyglot_sqlite_probe"
    destination.mkdir(parents=True, exist_ok=True)
    shutil.copy2(filename, destination / "__init__.py")
    (destination / "__init__.pyi").write_text(probe_stub(parsed), encoding="utf-8")
    (destination / "py.typed").write_text("", encoding="utf-8")
    print("Staged Python shared tests and NumericProbe with canonical library imports.")


def pack_test(package_root: Path) -> None:
    package_root = package_root.resolve(strict=True)
    packed = OUTPUT / "packed-python"
    clean_generated(packed)
    wheel = packed / "polyglot_sqlite-0.0.0-py3-none-any.whl"
    dist_info = "polyglot_sqlite-0.0.0.dist-info"
    dependency = next(value for value in tomllib.loads((REPOSITORY / "pyproject.toml").read_text(encoding="utf-8"))["project"]["dependencies"] if value.startswith("fable-library=="))
    files = {}
    for filename in (package_root / "polyglot_sqlite").rglob("*"):
        if filename.is_file() and "__pycache__" not in filename.parts:
            files[filename.relative_to(package_root).as_posix()] = filename.read_bytes()
    files[f"{dist_info}/METADATA"] = ("Metadata-Version: 2.3\nName: polyglot-sqlite\nVersion: 0.0.0\nRequires-Python: >=3.12\n" + f"Requires-Dist: {dependency}\n").encode()
    files[f"{dist_info}/WHEEL"] = b"Wheel-Version: 1.0\nGenerator: PolyglotSQLite local verification\nRoot-Is-Purelib: true\nTag: py3-none-any\n"
    records = io.StringIO(newline="")
    writer = csv.writer(records)
    for filename, data in files.items():
        digest = base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode()
        writer.writerow([filename, "sha256=" + digest, len(data)])
    writer.writerow([f"{dist_info}/RECORD", "", ""])
    files[f"{dist_info}/RECORD"] = records.getvalue().encode()
    with zipfile.ZipFile(wheel, "w", zipfile.ZIP_DEFLATED) as archive:
        for filename, data in files.items():
            archive.writestr(filename, data)
    installed = packed / "installed"
    with zipfile.ZipFile(wheel) as archive:
        archive.extractall(installed)
    shutil.copytree(package_root / "polyglot_sqlite_probe", installed / "polyglot_sqlite_probe", ignore=shutil.ignore_patterns("__pycache__"))
    for filename in installed.rglob("*.pyi"):
        validate_stub(filename.read_text(encoding="utf-8"), str(filename))
    environment = dict(os.environ, PYTHONPATH=str(installed))
    subprocess.run([sys.executable, str(REPOSITORY / "tests/PolyglotSQLite.Native/python.py")], check=True, env=environment)
    print(f"PolyglotSQLite native consumer passed against the locally packed wheel: {wheel}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("build", "tests", "pack-test"))
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path, nargs="?")
    args = parser.parse_args()
    if args.command == "pack-test":
        pack_test(args.source)
    else:
        if args.destination is None:
            parser.error("build and tests require a destination package root")
        (stage if args.command == "build" else stage_tests)(args.source, args.destination)


if __name__ == "__main__":
    main()
