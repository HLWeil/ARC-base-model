"""Verify native consumers from isolated local archives, without registry access."""
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tarfile
import tempfile
import zipfile

repository = Path(__file__).resolve().parent.parent
output = repository / "build/out/base-model"
archives = output / "archives"
archives.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix="packed-", dir=output) as temporary:
    isolated = Path(temporary)
    js = isolated / "js"
    for name in ("arc-base-model", "arc-base-model-probe"):
        archive = archives / f"{name}.tgz"
        with tarfile.open(archive, "w:gz") as packed:
            packed.add(output / "js/node_modules" / name, arcname="package")
        destination = js / "node_modules" / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        with tarfile.open(archive) as packed:
            packed.extractall(destination.parent, filter="data")
        (destination.parent / "package").rename(destination)
    (js / "package.json").write_text('{"type":"module"}')
    shutil.copytree(output / "js/consumer", js / "consumer")
    subprocess.run(["node", str(repository / "build/base-model.mjs"), "typecheck", str(js / "consumer/consumer.ts")], check=True)
    subprocess.run(["node", str(js / "consumer/javascript.mjs")], check=True)

    archive = archives / "arc_base_model.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as packed:
        for package in ("arc_base_model", "arc_base_model_probe"):
            for source in (output / "python" / package).rglob("*"):
                if source.is_file() and "__pycache__" not in source.parts:
                    packed.write(source, source.relative_to(output / "python"))
    python = isolated / "python"
    with zipfile.ZipFile(archive) as packed:
        packed.extractall(python)
    environment = dict(os.environ, PYTHONPATH=str(python))
    subprocess.run([sys.executable, str(repository / "tests/ARCBaseModel.Native/python.py")], env=environment, check=True)
print("Packed native consumers and declarations passed.")
