from arc_management import Session, DatasetYaml, ManagedGraph, RevisionConflictException
from arc_management.arctrl import ARC
from arc_management import SampleOperations
from arc_base_model import Dataset, Sample, Process, ModelBuilder
from pathlib import Path
from time import time_ns


def check(value, message):
    if not value:
        raise AssertionError(message)


def rejects(action):
    try:
        action()
    except Exception:
        return
    raise AssertionError("Expected rejection")


session = Session.create_in_memory()
graph = session.create_arc(Dataset(["process-provenance"], ["native-python"]))
check(isinstance(graph, ManagedGraph), "Public graph wrapper")
check(session.open_arc(graph.ArcId) is graph, "Cached facade")
check(isinstance(graph.Sample, SampleOperations), "Operation facade preserves instance checks")
source = Sample("A")
added = graph.Sample.add(graph.read_model().Revision, source)
check(source.Id is None and added.Value.Id is not None, "Assigned copies only")
check(isinstance(added.Value, Sample), "Canonical model class")
rejects(lambda: setattr(added.Value, "Name", "bad"))
rejects(lambda: setattr(added.Value, "_name", "bad"))
rejects(lambda: setattr(added, "Changed", False))
rejects(lambda: setattr(added.Revision, "_revision", 0))
changed = graph.Sample.set_name(added.Revision, added.Value.Id, "B")
check(changed.Value.Name == "B" and added.Value.Name == "A", "Old snapshots unchanged")
try:
    graph.Sample.set_name(added.Revision, added.Value.Id, "stale")
except RevisionConflictException as error:
    check(error.Expected.Value == added.Revision.Value, "Structured expected revision")
else:
    raise AssertionError("Expected revision conflict")
unchanged = graph.Sample.upsert(changed.Revision, changed.Value)
check(not unchanged.Changed and unchanged.Operation is None, "Explicit no-op")
check(unchanged.Revision.Value == changed.Revision.Value, "No-op revision")
proc = graph.Process.add(unchanged.Revision, Process("p"))
linked = graph.Process.set_input_sample(proc.Revision, proc.Value.Id, added.Value.Id)
check(isinstance(linked.Value.Input, Sample), "Native erased alternative")
read = graph.read_model()
root = graph.Dataset.upsert(read.Revision, Dataset.with_processes(read.Value, [linked.Value, linked.Value]))
check(isinstance(root.Value.Processes, tuple), "Immutable native tuple")
check(root.Value.Processes[0] is root.Value.Processes[1], "Duplicate sharing")
rejects(lambda: root.Value.Processes.append(linked.Value))
rejects(lambda: graph.Dataset.upsert(root.Revision, root.Value, True))
rejects(lambda: graph.Dataset.upsert(root.Revision, root.Value, recursive=1))
rejects(lambda: graph.Dataset.upsert(root.Revision, root.Value, recursively=True))
nested_sample = Sample.with_name(changed.Value, "C")
nested_process = Process.with_input(linked.Value, nested_sample)
replacement = Dataset.with_processes(root.Value, [nested_process, nested_process])
rejects(lambda: graph.Dataset.upsert(root.Revision, replacement))
recursive = graph.Dataset.upsert(root.Revision, replacement, recursive=True)
check(graph.Sample.get(added.Value.Id).Name == "C", "Recursive update")
builder = ModelBuilder()
token = builder.clone_graph(recursive.Value)
builder.Dataset.set_title(token, "builder edit")
built = builder.finish().get(token)
saved = graph.Dataset.upsert(recursive.Revision, built, recursive=True)
check(saved.Value.Title == "builder edit", "Native builder")
yaml = graph.to_yaml()
decoded = DatasetYaml.decode(yaml)
check(isinstance(decoded, Dataset), "Codec canonical class")
check(decoded.Processes[0] is decoded.Processes[1], "Codec sharing")
folder = str(Path("build/out/arc-management") / f"native-python-{time_ns()}")
arc = ARC.bind(graph, folder)
check(arc.ManagedGraph is graph and arc.Dataset is graph.Dataset, "Forwarded native identity")
arc.save()
check(not arc.IsDirty, "Saved checkpoint")
rejects(lambda: graph.Entity.set_bool_property(graph.read_model().Revision, graph.Model.Id, "flag", 1))
rejects(lambda: graph.Entity.set_number_property(graph.read_model().Revision, graph.Model.Id, "number", False))
held = graph.Sample.get(added.Value.Id)
arc.close()
session.close()
check(held.Name == "C", "Snapshot lifetime")
print("Immutable Python management consumer passed")
