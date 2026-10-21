from arc_management import Session, DatasetYaml, ManagedGraph, SnapshotConflictException
from arc_management.arctrl import ARC
from arc_management import SampleOperations
from arc_base_model import Dataset, Sample, Data, Process, ModelBuilder
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
added = graph.Sample.add(source)
check(source.Id is None and added.Value.Id is not None, "Assigned copies only")
check(isinstance(added.Value, Sample), "Canonical model class")
rejects(lambda: setattr(added.Value, "Name", "bad"))
rejects(lambda: setattr(added.Value, "_name", "bad"))
rejects(lambda: setattr(added, "Changed", False))
changed = graph.Sample.set_name(added.Value, "B")
check(changed.Value.Name == "B" and added.Value.Name == "A", "Old snapshots unchanged")
try:
    graph.Sample.upsert(added.Value, baseline=added.Value)
except SnapshotConflictException as error:
    check(error.EntityId == added.Value.Id, "Structured conflicting entity")
else:
    raise AssertionError("Expected snapshot conflict")
check(not hasattr(graph, "Revision") and not hasattr(changed, "Revision"), "No public revision plumbing")
types = graph.Sample.set_additional_types(added.Value, ["new type"])
check(types.Value.Name == "B", "Older subjects preserve current fields")
unchanged = graph.Sample.upsert(types.Value, baseline=types.Value)
check(not unchanged.Changed and unchanged.Operation is None, "Explicit no-op")
proc = graph.Process.add(Process("p"))
linked = graph.Process.set_input_sample(proc.Value, added.Value)
check(isinstance(linked.Value.Input, Sample), "Native erased alternative")
rejects(lambda: graph.Process.set_input_sample(proc.Value, added.Value.Id))
rejects(lambda: graph.Process.set_input(proc.Value, graph.Model))
rejects(lambda: graph.Dataset.add_process(graph.Model, proc.Value.Id))
rejects(lambda: graph.Dataset.add_process(graph.Model, added.Value))
rejects(lambda: graph.Dataset.add_process(graph.Model, None))
rejects(lambda: graph.Dataset.set_processes(graph.Model, [proc.Value.Id]))
rejects(lambda: graph.Entity.set_object_property(graph.Model, "process", proc.Value.Id))
rejects(lambda: graph.Descriptor.create(added.Value.Id))
relation_graph = session.create_arc(Dataset(["process-provenance"], ["relations"]))
relation_process = relation_graph.Process.create("process").Value
relation_sample = relation_graph.Sample.create("sample").Value
relation_data = relation_graph.Data.create("data").Value
enumerations = 0


def relationship_iterable():
    global enumerations
    enumerations += 1
    yield relation_process
    yield relation_process


relations = relation_graph.Dataset.set_processes(relation_graph.Model, relationship_iterable())
check(enumerations == 1 and relations.Value.Processes[0] is relations.Value.Processes[1], "Iterable consumed once with sharing")
relation_graph.Dataset.remove_process(relation_graph.Model, relation_process)
check(len(relation_graph.Model.Processes) == 1 and len(relations.Value.Processes) == 2, "Remove preserves earlier snapshot")
relation_graph.Dataset.add_process(relation_graph.Model, relation_process)
relation_graph.Process.set_input(relation_process, relation_sample)
relation_graph.Process.set_output(relation_process, relation_data)
check(isinstance(relation_graph.Process.get(relation_process.Id).Output, Data), "Native Data alternative")
relation_graph.Descriptor.create(relation_sample)
relation_descriptor = relation_graph.Descriptor.create(relation_data).Value
relation_graph.Descriptor.set_describes(relation_descriptor, relation_sample)
relation_graph.Entity.set_object_property(relation_graph.Model, "process", relation_process)
relation_graph.Entity.add_object_property(relation_graph.Model, "sample", relation_sample)
rejects(lambda: relation_graph.Dataset.add_process(relation_graph.Model, proc.Value))
rejects(lambda: relation_graph.Entity.set_object_property(relation_graph.Model, "foreign", proc.Value))
read = graph.read_model()
root = graph.Dataset.upsert(Dataset.with_processes(read, [linked.Value, linked.Value]), baseline=read)
check(isinstance(root.Value.Processes, tuple), "Immutable native tuple")
check(root.Value.Processes[0] is root.Value.Processes[1], "Duplicate sharing")
rejects(lambda: root.Value.Processes.append(linked.Value))
rejects(lambda: graph.Dataset.upsert(root.Value, True))
rejects(lambda: graph.Dataset.upsert(root.Value, recursive=1))
rejects(lambda: graph.Dataset.upsert(root.Value, recursively=True))
nested_sample = Sample.with_name(types.Value, "C")
nested_process = Process.with_input(linked.Value, nested_sample)
replacement = Dataset.with_processes(root.Value, [nested_process, nested_process])
rejects(lambda: graph.Dataset.upsert(replacement, baseline=root.Value))
recursive = graph.Dataset.upsert(replacement, baseline=root.Value, recursive=True)
check(graph.Sample.get(added.Value.Id).Name == "C", "Recursive update")
builder = ModelBuilder()
token = builder.clone_graph(recursive.Value)
builder.Dataset.set_title(token, "builder edit")
built = builder.finish().get(token)
saved = graph.Dataset.upsert(built, baseline=recursive.Value, recursive=True)
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
rejects(lambda: graph.Entity.set_bool_property(graph.Model, "flag", 1))
rejects(lambda: graph.Entity.set_number_property(graph.Model, "number", False))
held = graph.Sample.get(added.Value.Id)
arc.close()
session.close()
check(held.Name == "C", "Snapshot lifetime")
print("Immutable Python management consumer passed")
