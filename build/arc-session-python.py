"""Stage the ARC toolbox with canonical model/SQLite classes and checked stubs.

The model's existing numeric compatibility pass applies to management consumers
only. SQLite retains its own explicit representation boundaries.
"""
from __future__ import annotations

import ast
import importlib.util
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
OUTPUT = REPOSITORY / "build/out/arc-session"
GROUPS = ("entity", "dataset", "process", "sample", "data", "recipe", "annotation",
          "formal_parameter", "defined_term", "defined_term_set", "descriptor", "agent",
          "organization", "scholarly_article", "history")
PUBLIC = {"ARC": "arc.py", "AppliedOperation": "applied_operation.py",
          "Session": "ARCSession/session.py", "ArcInfo": "ARCSession/session.py",
          **{''.join(word.title() for word in group.split('_')) + "Operations":
             f"Operations/{group}_operations.py" for group in GROUPS}}
HIDDEN = {"__init__", "Wrap", "Dispose", "Repository"}

spec = importlib.util.spec_from_file_location("model_staging", REPOSITORY / "build/base-model-python.py")
compat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compat)
MODEL_NAMES = tuple(compat.CLASS_MODULES) + tuple(compat.ALIASES)

def clean(directory: Path) -> None:
    directory = directory.resolve()
    if not directory.is_relative_to(OUTPUT.resolve()):
        raise ValueError(f"Refusing to clean outside {OUTPUT}")
    if directory.exists():
        shutil.rmtree(directory)
    directory.mkdir(parents=True)

def rewrite_imports(source: str, relative: Path, test: bool = False) -> str:
    class Imports(ast.NodeTransformer):
        def visit_ImportFrom(self, node: ast.ImportFrom) -> ast.ImportFrom:
            parts = (node.module or "").split(".")
            if node.level:
                parent = list(relative.parent.parts)
                parts = parent[:len(parent) - node.level + 1] + parts
            replacements = ((["src", "ARCBaseModel"], "arc_base_model"),
                            (["src", "PolyglotSQLite"], "polyglot_sqlite"),
                            (["src", "ARCtrl"], "arc_session._impl")) if test else (
                                (["ARCBaseModel"], "arc_base_model"),
                                (["PolyglotSQLite"], "polyglot_sqlite"))
            for prefix, package in replacements:
                if parts[:len(prefix)] == prefix:
                    node.module = ".".join([package, *parts[len(prefix):]])
                    node.level = 0
                    break
            return node
    parsed = Imports().visit(ast.parse(source, filename=str(relative)))
    rewritten = ast.unparse(ast.fix_missing_locations(parsed)) + "\n"
    return rewritten if "fable_modules" in relative.parts else compat.numeric_compatibility(rewritten, str(relative))

def public_annotation(node: ast.expr | None, owner: str, method: str, field: str) -> str:
    if node is None:
        raise ValueError(f"Missing annotation: {owner}.{method}.{field}")
    # Restore erased alternatives, matching the corresponding F# declarations.
    erased = "Entity" if owner == "EntityOperations" else {
        ("ARC", "import_folder"): "Session",
        ("ProcessOperations", "set_input"): "EntityReference",
        ("ProcessOperations", "set_output"): "EntityReference",
        ("DescriptorOperations", "create"): "EntityReference",
        ("DescriptorOperations", "set_describes"): "EntityReference",
        ("AnnotationOperations", "set_value"): "AnnotationValue",
        ("RecipeOperations", "set_intended_use"): "RecipeIntendedUse",
        ("DefinedTermOperations", "set_in_defined_term_set"): "DefinedTermSetReference",
    }.get((owner, method))
    class Types(ast.NodeTransformer):
        def visit_Name(self, name: ast.Name) -> ast.expr:
            if name.id == "Any":
                if erased is None:
                    raise ValueError(f"Unmapped erased type: {owner}.{method}.{field}")
                return ast.Name(id=erased, ctx=ast.Load())
            return ast.Name(id={"IEnumerable_1": "Iterable", "int32": "int", "float64": "float"}.get(name.id, name.id), ctx=ast.Load())
    return ast.unparse(ast.fix_missing_locations(Types().visit(ast.parse(ast.unparse(node), mode="eval").body)))

def declarations(source: Path) -> str:
    classes = []
    for name, module in PUBLIC.items():
        parsed = ast.parse((source / module).read_text(encoding="utf-8"))
        node = next((item for item in parsed.body if isinstance(item, ast.ClassDef) and item.name == name), None)
        if node is None:
            raise ValueError(f"Missing generated class {name}")
        lines = [f"class {name}:", "    def __init__(self, _internal: Never) -> None: ..."]
        for member in node.body:
            if not isinstance(member, ast.FunctionDef) or member.name in HIDDEN:
                continue
            decorators = [ast.unparse(decorator) for decorator in member.decorator_list]
            if any(decorator not in ("staticmethod", "property") for decorator in decorators):
                raise ValueError(f"Unexpected decorator on {name}.{member.name}")
            if member.args.vararg or member.args.kwarg or member.args.kwonlyargs:
                raise ValueError(f"Unexpected call shape on {name}.{member.name}")
            parameters = []
            defaults = [None] * (len(member.args.args) - len(member.args.defaults)) + member.args.defaults
            for argument, default in zip(member.args.args, defaults):
                if argument.arg == "__unit":
                    continue
                if argument.arg == "self":
                    parameters.append("self")
                    continue
                annotation = public_annotation(argument.annotation, name, member.name, argument.arg)
                parameters.append(f"{argument.arg}: {annotation}" + (" = ..." if default else ""))
            result = public_annotation(member.returns, name, member.name, "return")
            lines.extend(f"    @{decorator}" for decorator in decorators)
            lines.append(f"    def {member.name}({', '.join(parameters)}) -> {result}: ...")
        classes.append("\n".join(lines))
    result = ("from __future__ import annotations\nfrom collections.abc import Iterable\nfrom typing import Never\n"
              f"from arc_base_model import {', '.join(MODEL_NAMES)}\n\n" + "\n\n".join(classes) + "\n")
    ast.parse(result)
    if "Any" in result or "__unit" in result:
        raise ValueError("Implementation types leaked into native declarations")
    return result

def stage() -> None:
    source = OUTPUT / "raw-python"
    stub = declarations(source)
    destination = OUTPUT / "python"
    clean(destination)
    for name, root in (("arc_base_model", "base-model"), ("polyglot_sqlite", "polyglot-sqlite")):
        shutil.copytree(REPOSITORY / f"build/out/{root}/python/{name}", destination / name)
    package = destination / "arc_session"
    implementation = package / "_impl"
    for filename in source.rglob("*.py"):
        relative = filename.relative_to(source)
        if relative.parts[0] in ("ARCBaseModel", "PolyglotSQLite"):
            continue
        target = implementation / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(rewrite_imports(filename.read_text(encoding="utf-8"), relative), encoding="utf-8")
    entry = "\n".join(f"from ._impl.{module[:-3].replace('/', '.')} import {name} as {name}" for name, module in PUBLIC.items()) + "\n"
    (package / "__init__.py").write_text(entry, encoding="utf-8")
    (package / "__init__.pyi").write_text(stub, encoding="utf-8")
    (package / "py.typed").touch()
    print(f"Staged {len(PUBLIC)} ARC toolbox classes with checked Python declarations.")

def stage_tests() -> None:
    source = OUTPUT / "tests-py"
    for filename in source.rglob("*.py"):
        relative = filename.relative_to(source)
        if relative.parts[0] == "src":
            continue
        filename.write_text(rewrite_imports(filename.read_text(encoding="utf-8"), relative, test=True), encoding="utf-8")

def pack_test() -> None:
    packed = OUTPUT / "packed-python"
    clean(packed)
    tarballs = packed / "wheels"
    tarballs.mkdir()
    installed = packed / "installed"
    installed.mkdir()
    for name in ("arc_session", "arc_base_model", "polyglot_sqlite"):
        contents = {}
        for filename in (OUTPUT / "python" / name).rglob("*"):
            if filename.is_file() and "__pycache__" not in filename.parts:
                contents[filename.relative_to(OUTPUT / "python").as_posix()] = filename.read_bytes()
        metadata = f"{name}-0.0.0.dist-info"
        project = tomllib.loads((REPOSITORY / "pyproject.toml").read_text(encoding="utf-8"))
        runtime = next(item for item in project["project"]["dependencies"] if item.startswith("fable-library"))
        dependencies = f"Requires-Dist: {runtime}\n"
        if name == "arc_session":
            dependencies += "Requires-Dist: arc-base-model==0.0.0\nRequires-Dist: polyglot-sqlite==0.0.0\n"
        contents[f"{metadata}/METADATA"] = (f"Metadata-Version: 2.1\nName: {name.replace('_', '-')}\nVersion: 0.0.0\nRequires-Python: >=3.12\n{dependencies}").encode()
        contents[f"{metadata}/WHEEL"] = b"Wheel-Version: 1.0\nGenerator: ARC toolbox local tests\nRoot-Is-Purelib: true\nTag: py3-none-any\n"
        records = io.StringIO()
        writer = csv.writer(records)
        for filename, data in contents.items():
            digest = base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode()
            writer.writerow([filename, "sha256=" + digest, len(data)])
        writer.writerow([f"{metadata}/RECORD", "", ""])
        contents[f"{metadata}/RECORD"] = records.getvalue().encode()
        wheel = tarballs / f"{name}-0.0.0-py3-none-any.whl"
        with zipfile.ZipFile(wheel, "w", zipfile.ZIP_DEFLATED) as archive:
            for filename, data in contents.items():
                archive.writestr(filename, data)
        with zipfile.ZipFile(wheel) as archive:
            archive.extractall(installed)
    environment = dict(os.environ, PYTHONPATH=str(installed))
    subprocess.run([sys.executable, str(REPOSITORY / "tests/ManagementPrototype.Tests/Native.py")],
                   cwd=REPOSITORY, env=environment, check=True)
    print("ARC toolbox native consumers passed against locally packed Python wheels.")

if __name__ == "__main__":
    if sys.argv[1:] == ["build"]:
        stage()
    elif sys.argv[1:] == ["tests"]:
        stage_tests()
    elif sys.argv[1:] == ["pack-test"]:
        pack_test()
    else:
        raise SystemExit("Usage: python build/arc-session-python.py build|tests|pack-test")
