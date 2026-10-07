"""Stage Fable's model without replacing its classes, and add native typing.

Usage: python build/base-model-python.py GENERATED_DIR PACKAGE_OUTPUT_DIR
       python build/base-model-python.py tests GENERATED_TEST_DIR PACKAGE_ROOT
       python build/base-model-python.py compat GENERATED_DIR

Fable 5 emits erased unions as Any in Python. Public stubs retain the emitted
constructor, property, and method shapes while restoring domain alternatives.
The runtime package exports the original generated classes so consumers and
transpiled mappers share class identity.

The pinned Fable 5.6 Python backend also tests float64 with a runtime wrapper
class, excluding ordinary Python int/float inputs. The explicit compatibility
pass admits those native numeric types (never bool) and unwraps numeric values
at the public model/probe boundary. It does not change the F# model or IDs.
"""

from __future__ import annotations

import argparse
import ast
from pathlib import Path
import shutil
import sys


MODULE_CLASSES = {
    "entity": ("EntityObject", "EntityPropertyBag", "EntityCollection", "EntityNull", "EntityBlob"),
    "defined_term": ("DefinedTermSet", "DefinedTerm"),
    "annotation": ("Annotation", "FormalParameter"),
    "entities": ("Sample", "Data"),
    "recipe": ("Recipe",),
    "process": ("Process",),
    "descriptor": ("Descriptor",),
    "administrative": ("Organization", "Agent", "ScholarlyArticle"),
    "dataset": ("Dataset",),
}

# These are the only type facts lost by the pinned Python compiler. Everything
# else in the public signatures is read from the generated source.
ALIASES = {
    "Entity": ("entity", "EntityObject | EntityCollection | float | str | bool | EntityNull | EntityBlob"),
    "AnnotationValue": ("annotation", "str | float"),
    "EntityReference": ("entities", "Sample | Data"),
    "RecipeIntendedUse": ("recipe", "str | DefinedTerm"),
    "DefinedTermSetReference": ("defined_term", "str | DefinedTermSet"),
}
ERASED_FIELDS = {
    ("MappingProbe", "ClassifyExtension"): "Entity",
    ("MappingProbe", "IsExtensionNumber"): "Entity",
    **{(owner, field): "Entity" for owner, fields in {
        "EntityObject": ("AddEntityProperty", "SetEntityProperty"),
        "EntityPropertyBag": ("Get", "Add", "Set"),
        "EntityCollection": ("values", "Get", "Set", "Add"),
    }.items() for field in fields},
    ("Annotation", "value"): "AnnotationValue",
    ("DefinedTerm", "in_defined_term_set"): "DefinedTermSetReference",
    ("Recipe", "intended_use"): "RecipeIntendedUse",
    ("Process", "input"): "EntityReference",
    ("Process", "output"): "EntityReference",
    ("Descriptor", "describes"): "EntityReference",
    ("MappingProbe", "WriteAnnotation"): "AnnotationValue",
    ("MappingProbe", "ReadAnnotation"): "AnnotationValue",
    ("MappingProbe", "WriteEntity"): "EntityReference",
    ("MappingProbe", "ReadEntity"): "EntityReference",
    ("MappingProbe", "WriteIntendedUse"): "RecipeIntendedUse",
    ("MappingProbe", "ReadIntendedUse"): "RecipeIntendedUse",
    ("MappingProbe", "WriteTermSet"): "DefinedTermSetReference",
    ("MappingProbe", "ReadTermSet"): "DefinedTermSetReference",
    ("MappingProbe", "IsNumber"): "AnnotationValue",
    ("MappingProbe", "IsText"): "AnnotationValue",
    ("MappingProbe", "ClassifyNumberFirst"): "AnnotationValue",
}
CLASS_MODULES = {
    name: module for module, names in MODULE_CLASSES.items() for name in names
}
COMPAT_MARKER = "_ARC_BASE_MODEL_PYTHON_COMPAT"
COMPAT_VERSION = "fable-5.6-numeric-v1"


def dotted_name(node: ast.expr) -> str | None:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        prefix = dotted_name(node.value)
        return f"{prefix}.{node.attr}" if prefix else None
    return None


def numeric_compatibility(source: str, filename: str) -> str:
    """Adapt pinned-compiler numeric checks without replacing domain classes.

    Keep the compiler's wrapper check, additionally accepting exact built-in int
    and float. Using type rather than isinstance for these branches excludes
    bool, which is an int subclass but is not an AnnotationValue alternative.
    """
    parsed = ast.parse(source, filename=filename)
    for node in parsed.body:
        if isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id == COMPAT_MARKER for target in node.targets
        ):
            if not isinstance(node.value, ast.Constant) or node.value.value != COMPAT_VERSION:
                raise ValueError(f"Unknown Python compatibility version in {filename}")
            return source
    float_types = set()
    for node in parsed.body:
        if isinstance(node, ast.ImportFrom):
            for name in node.names:
                local = name.asname or name.name
                if node.module == "fable_library.core" and name.name == "float64":
                    float_types.add(local)
                elif node.module == "fable_library" and name.name == "core":
                    float_types.add(local + ".float64")
        elif isinstance(node, ast.Import):
            for name in node.names:
                if name.name == "fable_library.core":
                    float_types.add((name.asname or name.name) + ".float64")
    changes = 0
    boundaries = 0

    class NumericChecks(ast.NodeTransformer):
        def visit_Call(self, node: ast.Call) -> ast.expr:
            nonlocal changes
            node = self.generic_visit(node)
            if not (
                isinstance(node.func, ast.Name) and node.func.id == "isinstance"
                and len(node.args) == 2 and not node.keywords
            ):
                return node
            checked_types = node.args[1].elts if isinstance(node.args[1], ast.Tuple) else [node.args[1]]
            if not any(dotted_name(checked) in float_types for checked in checked_types):
                return node
            # Evaluate the checked expression once, including nontrivial emitted
            # expressions. The local lambda is internal to this generated check.
            parameter = ast.Name(id="_arc_numeric_candidate", ctx=ast.Load())
            original_check = ast.Call(func=node.func, args=[parameter, node.args[1]], keywords=[])
            native_check = ast.Compare(
                left=ast.Call(func=ast.Name(id="type", ctx=ast.Load()), args=[parameter], keywords=[]),
                ops=[ast.In()],
                comparators=[ast.Tuple(elts=[ast.Name(id="int", ctx=ast.Load()), ast.Name(id="float", ctx=ast.Load())], ctx=ast.Load())],
            )
            predicate = ast.Lambda(
                args=ast.arguments(posonlyargs=[], args=[ast.arg(arg="_arc_numeric_candidate")], kwonlyargs=[], kw_defaults=[], defaults=[]),
                body=ast.BoolOp(op=ast.Or(), values=[original_check, native_check]),
            )
            changes += 1
            return ast.Call(func=predicate, args=[node.args[0]], keywords=[])

    class NativeReturns(ast.NodeTransformer):
        def visit_FunctionDef(self, node: ast.FunctionDef) -> ast.FunctionDef:
            # Wrap the public method's returns, not its internal local functions.
            return node

        def visit_Return(self, node: ast.Return) -> ast.Return:
            if node.value is not None:
                node.value = ast.Call(
                    func=ast.Name(id="_arc_native_number", ctx=ast.Load()),
                    args=[node.value], keywords=[],
                )
            return node

    parsed = NumericChecks().visit(parsed)
    for class_node in (node for node in parsed.body if isinstance(node, ast.ClassDef)):
        for method in (node for node in class_node.body if isinstance(node, ast.FunctionDef)):
            is_getter = any(
                isinstance(decorator, ast.Name) and decorator.id == "property"
                for decorator in method.decorator_list
            )
            is_numeric_getter = is_getter and (
                (class_node.name == "Annotation" and method.name == "Value")
                or (class_node.name == "AnnotationColumns" and method.name == "Number")
            )
            is_numeric_reader = (class_node.name == "MappingProbe" and method.name == "ReadAnnotation") or (class_node.name in {"EntityPropertyBag", "EntityCollection"} and method.name == "Get")
            if is_numeric_getter or is_numeric_reader:
                method.body = [NativeReturns().visit(statement) for statement in method.body]
                boundaries += 1
    if not changes and not boundaries:
        return source
    additions = ast.parse(f'{COMPAT_MARKER} = "{COMPAT_VERSION}"\n').body
    if boundaries:
        additions += ast.parse(
            "from fable_library.core import float64 as _arc_fable_float64\n"
            "def _arc_native_number(value):\n"
            "    return float(value) if isinstance(value, _arc_fable_float64) else value\n"
        ).body
    insertion = 0
    for index, node in enumerate(parsed.body):
        if isinstance(node, ast.ImportFrom) and node.module == "__future__":
            insertion = index + 1
    parsed.body[insertion:insertion] = additions
    result = ast.unparse(ast.fix_missing_locations(parsed)) + "\n"
    compile(result, filename, "exec")
    return result


def compatibility_tree(directory: Path) -> None:
    directory = directory.resolve(strict=True)
    if not directory.is_dir():
        raise ValueError(f"Generated input is not a directory: {directory}")
    count = 0
    for path in directory.rglob("*.py"):
        if any(
            part == "fable_modules" or part.startswith("fable_library") or part.startswith("fable-library")
            or part in {"site-packages", "__pycache__"}
            for part in path.relative_to(directory).parts
        ):
            continue
        source = path.read_text(encoding="utf-8-sig")
        transformed = numeric_compatibility(source, str(path))
        if transformed != source:
            path.write_text(transformed, encoding="utf-8")
            count += 1
    print(f"Applied Fable 5.6 native numeric compatibility to {count} generated Python modules")


def field_key(name: str) -> str:
    """Match PascalCase properties with emitted snake_case arguments."""
    return name.replace("_", "").lower()


def annotation_text(annotation: ast.expr | None, class_name: str, field: str) -> str:
    if annotation is None:
        raise ValueError(f"Missing annotation for {class_name}.{field}")
    replacement = next(
        (
            alias for (owner, property_name), alias in ERASED_FIELDS.items()
            if owner == class_name and field_key(property_name) == field_key(field)
        ),
        None,
    )

    class NativeAnnotations(ast.NodeTransformer):
        def visit_Name(self, node: ast.Name) -> ast.expr:
            if node.id == "Any":
                if replacement is None:
                    raise ValueError(f"Unmapped erased type in {class_name}.{field}")
                return ast.Name(id=replacement, ctx=ast.Load())
            if node.id == "IEnumerable_1":
                return ast.Name(id="Iterable", ctx=ast.Load())
            if node.id == "int32":
                return ast.Name(id="int", ctx=ast.Load())
            if node.id == "float64":
                return ast.Name(id="float", ctx=ast.Load())
            return node

    # Work on a copy; the parsed runtime module is the source of truth throughout.
    expression = ast.parse(ast.unparse(annotation), mode="eval").body
    expression = NativeAnnotations().visit(expression)
    return ast.unparse(ast.fix_missing_locations(expression))


def constructor_stub(class_node: ast.ClassDef, constructor: ast.FunctionDef) -> str:
    arguments = constructor.args
    if arguments.posonlyargs or arguments.kwonlyargs or arguments.vararg or arguments.kwarg:
        raise ValueError(f"Unexpected constructor convention for {class_node.name}")
    default_start = len(arguments.args) - len(arguments.defaults)
    parameters = []
    for index, argument in enumerate(arguments.args):
        if index == 0 and argument.arg == "self":
            parameters.append("self")
            continue
        if argument.arg == "__unit":
            continue
        annotation = annotation_text(argument.annotation, class_node.name, argument.arg)
        parameter = f"{argument.arg}: {annotation}"
        if index >= default_start:
            default = arguments.defaults[index - default_start]
            if not isinstance(default, ast.Constant) or default.value is not None:
                raise ValueError(f"Unexpected default for {class_node.name}.{argument.arg}")
            parameter += " = None"
        parameters.append(parameter)
    return f"    def __init__({', '.join(parameters)}) -> None: ..."


def class_stub(class_node: ast.ClassDef) -> str:
    base = "(EntityObject)" if any(dotted_name(b) == "EntityObject" for b in class_node.bases) else ""
    lines = [f"class {class_node.name}{base}:"]
    if base:
        lines.extend(("    @property", f'    def Type(self) -> Literal["{class_node.name}"]: ...'))
    for member in class_node.body:
        if not isinstance(member, ast.FunctionDef):
            continue
        if member.name == "__init__":
            lines.append(constructor_stub(class_node, member))
            continue
        is_getter = any(
            isinstance(decorator, ast.Name) and decorator.id == "property"
            for decorator in member.decorator_list
        )
        is_setter = any(
            isinstance(decorator, ast.Attribute) and decorator.attr == "setter"
            for decorator in member.decorator_list
        )
        if is_getter:
            if member.name == "Type" and class_node.name != "EntityObject":
                if not any(
                    isinstance(statement, ast.Return)
                    and isinstance(statement.value, ast.Constant)
                    and statement.value.value == class_node.name
                    for statement in member.body
                ):
                    raise ValueError(f"Unexpected fixed Type for {class_node.name}")
                annotation = f'Literal["{class_node.name}"]'
            else:
                annotation = annotation_text(member.returns, class_node.name, member.name)
            lines.extend(("    @property", f"    def {member.name}(self) -> {annotation}: ..."))
        elif is_setter:
            if member.name == "Type":
                raise ValueError(f"Type must be read-only on {class_node.name}")
            if len(member.args.args) != 2:
                raise ValueError(f"Unexpected setter shape for {class_node.name}.{member.name}")
            annotation = annotation_text(
                member.args.args[1].annotation, class_node.name, member.name
            )
            lines.extend((
                f"    @{member.name}.setter",
                f"    def {member.name}(self, value: {annotation}) -> None: ...",
            ))
        elif not member.name.startswith("_") and member.name not in {"Reserve", "check"}:
            parameters = ["self"]
            for argument in member.args.args[1:]:
                parameters.append(f"{argument.arg}: {annotation_text(argument.annotation, class_node.name, member.name)}")
            result = annotation_text(member.returns, class_node.name, member.name)
            lines.append(f"    def {member.name}({', '.join(parameters)}) -> {result}: ...")
    if not any(isinstance(node, ast.FunctionDef) and node.name == "__init__" for node in class_node.body):
        raise ValueError(f"Missing constructor for {class_node.name}")
    return "\n".join(lines)


def module_stub(module: str, parsed: ast.Module) -> str:
    classes = {
        node.name: node for node in parsed.body if isinstance(node, ast.ClassDef)
    }
    expected = MODULE_CLASSES[module]
    missing = set(expected) - classes.keys()
    if missing:
        raise ValueError(f"Missing generated classes in {module}: {sorted(missing)}")
    body = []
    for alias, (owner, expression) in ALIASES.items():
        if owner == module:
            body.append(f"{alias}: TypeAlias = {expression}")
    body.extend(class_stub(classes[name]) for name in expected)
    body_text = "\n\n".join(body)
    names = {
        node.id for node in ast.walk(ast.parse(body_text)) if isinstance(node, ast.Name)
    }
    imports = ["from __future__ import annotations"]
    if "Iterable" in names:
        imports.append("from collections.abc import Iterable")
    typing_names = sorted(names & {"Literal", "TypeAlias"})
    if typing_names:
        imports.append(f"from typing import {', '.join(typing_names)}")
    for other_module, other_classes in MODULE_CLASSES.items():
        if other_module == module:
            continue
        public_names = set(other_classes)
        public_names.update(name for name, (owner, _) in ALIASES.items() if owner == other_module)
        required = sorted(names & public_names)
        if required:
            imports.append(f"from .{other_module} import {', '.join(required)}")
    result = "\n".join(imports) + "\n\n" + body_text + "\n"
    validate_stub(result, f"{module}.pyi")
    return result


def validate_stub(source: str, name: str) -> None:
    parsed = ast.parse(source, filename=name)
    compile(parsed, name, "exec")
    if any(isinstance(node, ast.Name) and node.id == "Any" for node in ast.walk(parsed)):
        raise ValueError(f"Unresolved Any annotation in {name}")
    if "fable_library" in source:
        raise ValueError(f"Fable implementation types leaked into {name}")


def package_entrypoint(stub: bool) -> str:
    lines = ["from __future__ import annotations", "from typing import TypeAlias", ""]
    for module, classes in MODULE_CLASSES.items():
        # Explicit same-name aliases make re-exports visible to static checkers.
        exports = ", ".join(f"{name} as {name}" for name in classes)
        lines.append(f"from .{module} import {exports}")
    lines.append("")
    for alias, (_, expression) in ALIASES.items():
        lines.append(f"{alias}: TypeAlias = {expression}")
    names = [name for classes in MODULE_CLASSES.values() for name in classes] + list(ALIASES)
    lines.extend(("", f"__all__ = {tuple(names)!r}", ""))
    result = "\n".join(lines)
    validate_stub(result, "__init__.pyi" if stub else "__init__.py")
    return result


def stage(source: Path, destination: Path) -> None:
    source = source.resolve(strict=True)
    destination = destination.resolve()
    if not source.is_dir():
        raise ValueError(f"Generated input is not a directory: {source}")
    # Validate the entire public surface before writing a partial package.
    stubs = {}
    for module in MODULE_CLASSES:
        path = source / f"{module}.py"
        parsed = ast.parse(path.read_text(encoding="utf-8-sig"), filename=str(path))
        stubs[f"{module}.pyi"] = module_stub(module, parsed)
    destination.mkdir(parents=True, exist_ok=True)
    if source != destination:
        for runtime_file in source.glob("*.py"):
            if runtime_file.name != "__init__.py":
                shutil.copy2(runtime_file, destination / runtime_file.name)
    compatibility_tree(destination)
    for filename, contents in stubs.items():
        (destination / filename).write_text(contents, encoding="utf-8")
    (destination / "__init__.py").write_text(package_entrypoint(stub=False), encoding="utf-8")
    (destination / "__init__.pyi").write_text(package_entrypoint(stub=True), encoding="utf-8")
    (destination / "py.typed").write_text("", encoding="utf-8")
    print(f"Staged {len(CLASS_MODULES)} ARCBaseModel classes and {len(ALIASES)} typed alternatives at {destination}")


def probe_stub(parsed: ast.Module) -> str:
    row_names = ("AnnotationColumns", "EntityColumns", "IntendedUseColumns", "TermSetColumns")
    classes = {node.name: node for node in parsed.body if isinstance(node, ast.ClassDef)}
    bodies = [class_stub(classes[name]) for name in row_names]
    methods = ["class MappingProbe:"]
    for member in classes["MappingProbe"].body:
        if not isinstance(member, ast.FunctionDef) or member.name.startswith("_"):
            continue
        if not any(isinstance(d, ast.Name) and d.id == "staticmethod" for d in member.decorator_list):
            raise ValueError(f"Expected static MappingProbe.{member.name}")
        arguments = member.args
        if arguments.posonlyargs or arguments.kwonlyargs or arguments.vararg or arguments.kwarg:
            raise ValueError(f"Unexpected argument convention for MappingProbe.{member.name}")
        default_start = len(arguments.args) - len(arguments.defaults)
        parameters = []
        for index, argument in enumerate(arguments.args):
            default = arguments.defaults[index - default_start] if index >= default_start else None
            if (
                argument.arg == "__unit"
                and isinstance(argument.annotation, ast.Name) and argument.annotation.id == "Unit"
                and isinstance(default, ast.Name) and default.id == "UNIT"
            ):
                # F# unit-taking static members are ordinary no-argument calls.
                continue
            annotation = annotation_text(argument.annotation, "MappingProbe", member.name)
            parameter = f"{argument.arg}: {annotation}"
            if index >= default_start:
                if not isinstance(default, ast.Constant) or default.value is not None:
                    raise ValueError(f"Unexpected default for MappingProbe.{member.name}")
                parameter += " = None"
            parameters.append(parameter)
        returns = annotation_text(member.returns, "MappingProbe", member.name)
        methods.extend(("    @staticmethod", f"    def {member.name}({', '.join(parameters)}) -> {returns}: ..."))
    bodies.append("\n".join(methods))
    body = "\n\n".join(bodies)

    class QualifiedModelTypes(ast.NodeTransformer):
        def visit_Name(self, node: ast.Name) -> ast.expr:
            # EntityColumns.Sample/Data properties must not shadow their types.
            if node.id in CLASS_MODULES or node.id in ALIASES:
                return ast.Attribute(value=ast.Name(id="_model", ctx=ast.Load()), attr=node.id, ctx=ast.Load())
            return node

    body = ast.unparse(ast.fix_missing_locations(QualifiedModelTypes().visit(ast.parse(body))))
    result = (
        "from __future__ import annotations\n"
        "import arc_base_model as _model\n\n"
        + body + "\n"
    )
    validate_stub(result, "arc_base_model_probe/__init__.pyi")
    return result


def stage_tests(source: Path, package_root: Path) -> None:
    source = source.resolve(strict=True)
    destination = package_root.resolve() / "arc_base_model_probe"
    path = source / "mapping_probe.py"
    parsed = ast.parse(path.read_text(encoding="utf-8-sig"), filename=str(path))

    class SharedModelImports(ast.NodeTransformer):
        def visit_ImportFrom(self, node: ast.ImportFrom) -> ast.ImportFrom:
            prefix = "src.ARCBaseModel"
            if node.module == prefix or (node.module and node.module.startswith(prefix + ".")):
                node.module = "arc_base_model" + node.module[len(prefix):]
            return node

    parsed = ast.fix_missing_locations(SharedModelImports().visit(parsed))
    runtime = numeric_compatibility(ast.unparse(parsed) + "\n", "arc_base_model_probe/__init__.py")
    compile(runtime, "arc_base_model_probe/__init__.py", "exec")
    if "src.ARCBaseModel" in runtime:
        raise ValueError("Probe still references a duplicate generated model")
    stub = probe_stub(parsed)
    destination.mkdir(parents=True, exist_ok=True)
    (destination / "__init__.py").write_text(runtime, encoding="utf-8")
    (destination / "__init__.pyi").write_text(stub, encoding="utf-8")
    (destination / "py.typed").write_text("", encoding="utf-8")
    print(f"Staged test-only mapping consumer with shared model imports at {destination}")


def main() -> None:
    if sys.argv[1:2] == ["compat"]:
        parser = argparse.ArgumentParser(description="Adapt generated Fable 5.6 Python numeric boundaries")
        parser.add_argument("generated_dir", type=Path)
        arguments = parser.parse_args(sys.argv[2:])
        compatibility_tree(arguments.generated_dir)
        return
    if sys.argv[1:2] == ["tests"]:
        parser = argparse.ArgumentParser(description="Stage the test-only mapper against shared model classes")
        parser.add_argument("generated_test_dir", type=Path)
        parser.add_argument("package_root", type=Path)
        arguments = parser.parse_args(sys.argv[2:])
        stage_tests(arguments.generated_test_dir, arguments.package_root)
        return
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("generated_dir", type=Path)
    parser.add_argument("package_output_dir", type=Path)
    arguments = parser.parse_args()
    stage(arguments.generated_dir, arguments.package_output_dir)


if __name__ == "__main__":
    main()
