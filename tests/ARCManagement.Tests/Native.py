from pathlib import Path
from uuid import uuid4
import arc_management
from arc_management import DatasetYaml, ManagedGraph, Session
from arc_management.arctrl import ARC
from arc_base_model import Dataset, Annotation, Sample, Data, EntityObject, EntityCollection


def rejects(action):
    try:
        action()
    except Exception:
        return
    raise AssertionError("Expected rejection")


assert not hasattr(arc_management, "ARC")
detached = Dataset(["process-provenance"], ["detached"])
shared = Dataset(["administrative"], ["shared"])
same_id = Dataset(["semantic-designation"], ["distinct"])
shared.Id = same_id.Id = "same"
shared.Title = ""
detached.HasParts = [shared, shared, same_id, detached]
zero = Annotation("zero")
zero.Value = 0
detached.AdditionalProperties = [zero, zero]
detached.SetEntityProperty("self", detached)
detached_yaml = DatasetYaml.encode(detached)
assert detached.Id is None and zero.Id is None
decoded = DatasetYaml.decode(detached_yaml)
assert isinstance(decoded, Dataset) and decoded.Id is None
assert decoded.HasParts[0] is decoded.HasParts[1]
assert decoded.HasParts[0] is not decoded.HasParts[2] and decoded.HasParts[2].Id == "same"
assert decoded.HasParts[3] is decoded and decoded.EntityProperties.Get("self") is decoded
assert decoded.HasParts[0].Title == ""
assert decoded.AdditionalProperties[0] is decoded.AdditionalProperties[1]
assert decoded.AdditionalProperties[0].Value == 0 and type(decoded.AdditionalProperties[0].Value) is float
rejects(lambda: DatasetYaml.decode("not: [valid YAML"))
rejects(lambda: DatasetYaml.decode("type: Sample\nname: invalid root\n"))
recursive_collection = EntityCollection([])
recursive_collection.Add(recursive_collection)
detached.SetEntityProperty("bad", recursive_collection)
rejects(lambda: DatasetYaml.encode(detached))
detached.RemoveEntityProperty("bad")

folder = str(Path("build/out/arc-management") / ("native-python-" + uuid4().hex))
arc = ARC.create(folder, Dataset(["process-provenance", "semantic-designation", "administrative"], ["native"]))
try:
    sample = arc.Sample.add(Sample("sample"))
    data = arc.Data.add(Data("results.csv"))
    fragment = arc.Data.add(Data("results.csv"))
    arc.Data.set_selector(fragment, "row=1")
    arc.Data.set_has_parts(data, [fragment, fragment])
    value = arc.Annotation.create("value")
    arc.Annotation.set_value_number(value, 0)
    assert value.Value == 0 and type(value.Value) is float
    parameter = arc.FormalParameter.create("parameter")
    arc.FormalParameter.set_default_value(parameter, value)
    arc.Annotation.set_instance_of(value, parameter)
    recipe = arc.Recipe.create("recipe")
    arc.Recipe.set_parameters(recipe, [parameter, parameter])
    arc.Recipe.set_components(recipe, [value, value])
    termset = arc.DefinedTermSet.create("ontology")
    term = arc.DefinedTerm.create("term")
    arc.DefinedTerm.set_in_defined_term_set_entity(term, termset)
    arc.Recipe.set_intended_use_term(recipe, term)
    proc = arc.Process.create("process")
    arc.Process.set_input_sample(proc, sample)
    arc.Process.set_output_data(proc, data)
    arc.Process.set_executes_recipe(proc, recipe)
    descriptor = arc.Descriptor.create(sample)
    arc.Descriptor.set_annotations(descriptor, [value, value])
    organization = arc.Organization.create("organization")
    agent = arc.Agent.create("agent")
    arc.Agent.set_affiliations(agent, [organization, organization])
    arc.Agent.set_job_titles(agent, [term])
    article = arc.ScholarlyArticle.create("article")
    arc.ScholarlyArticle.set_authors(article, [agent, agent])
    child = arc.Dataset.create("child")
    arc.Dataset.set_has_parts(arc.Model, [child, child])
    arc.Dataset.set_processes(arc.Model, [proc])
    arc.Dataset.set_data_files(arc.Model, [data, fragment, data])
    arc.Dataset.set_descriptors(arc.Model, [descriptor])
    arc.Dataset.set_agents(arc.Model, [agent])
    arc.Dataset.set_citations(arc.Model, [article])
    assert type(arc.Data.list()) is list
    assert data.HasParts[0] is data.HasParts[1]
    quality = arc.Entity.add(EntityObject("QualityAssessment"))
    arc.Entity.set_number_property(quality, "score", 0)
    arc.Entity.set_bool_property(quality, "accepted", False)
    arc.Entity.set_null_property(quality, "missing")
    arc.Entity.set_blob_property(quality, "bytes", "AA==")
    arc.Entity.set_collection_property(quality, "values", [0, False, ""])
    arc.Entity.set_object_property(arc.Model, "quality", quality)
    assert arc.Entity.get_property(quality, "score") == 0
    assert arc.Entity.get_property(quality, "accepted") is False
    assert arc.Entity.has_property(quality, "missing")
    replacement = Annotation("replaced")
    replacement.Id = value.Id
    replacement.Value = ""
    arc.Annotation.upsert(replacement)
    assert value.Value == "" and value is not replacement
    arc.save()
    arc.close()
    resumed = ARC.open_folder(folder, "sql")
    try:
        assert resumed.Model.HasParts[0] is resumed.Model.HasParts[1]
        recovered_quality = resumed.Entity.get(quality.Id)
        assert resumed.Entity.get_property(resumed.Model, "quality") is recovered_quality
        assert resumed.Entity.get_property(recovered_quality, "score") == 0
        resumed.History.undo()
        restored = resumed.Annotation.get(value.Id)
        assert restored.Value == 0 and type(restored.Value) is float
        assert restored.InstanceOf.DefaultValue is restored
    finally:
        resumed.close()
finally:
    arc.close()
print("Native Python management checks passed.")

def root():
    value = Dataset(["process-provenance"], ["native"])
    value.Id = "root"
    return value

path = str(Path(folder) / "collection.sqlite")
session = Session.create_file(path)
first = session.create_arc(root())
second = session.create_arc(root())
assert isinstance(first, ManagedGraph)
assert not hasattr(first, "save") and not hasattr(first, "bind_folder")
rejects(lambda: session.import_yaml(detached_yaml))
sample = Sample("first")
sample.Id = "same"
assert first.Sample.add(sample) is sample
other = Sample("second")
other.Id = "same"
assert second.Sample.add(other) is other
numeric = first.Annotation.create("numeric boundary")
first.Annotation.set_value_number(numeric, 0)
assert type(numeric.Value) is float
rejects(lambda: first.Annotation.set_value_number(numeric, False))
rejects(lambda: first.Entity.set_number_property(first.Model, "bad", False))
process = first.Process.create("process")
rejects(lambda: first.Process.set_input_sample(process, other))
first.Process.set_input_sample(process, sample)
first.Dataset.add_process(first.Model, process)
first.Sample.set_name(sample, "changed")
first.History.undo()
assert sample.Name == "first" and other.Name == "second"
assert session.open_arc(first.ArcId) is first
assert len(session.list_arcs()) == 2
assert session.list_arcs()[0].RootEntityId == "root"
assert session.list_arcs()[0].Folder is None
destination = str(Path(folder) / "export")
workspace = ARC.bind(first, destination)
assert workspace.ManagedGraph is first and workspace.Sample is first.Sample
assert ARC.from_graph(first).ManagedGraph is first
assert not (Path(destination) / "arc.yml").exists()
workspace.save()
first_id, second_id = first.ArcId, second.ArcId
workspace.close()
rejects(lambda: first.Sample.create("closed"))
second.Sample.set_name(other, "independent")
session.close()
session = Session.open_file(path)
resumed = session.open_arc(first_id)
assert resumed.Model.Processes[0].Input is resumed.Sample.get("same")
resumed.History.redo()
assert resumed.Sample.get("same").Name == "changed"
assert session.open_arc(second_id).Sample.get("same").Name == "independent"
rejects(lambda: Session.create_file(path))
import_root = Dataset(["process-provenance"], ["import"])
import_child = Dataset(["administrative"], ["child"])
import_root.HasParts = [import_child, import_child]
imported_graph = session.import_yaml(DatasetYaml.encode(import_root))
assert import_root.Id is None and import_child.Id is None
assert imported_graph.Model.Id is not None
assert imported_graph.Model.HasParts[0] is imported_graph.Model.HasParts[1]
exported = DatasetYaml.decode(session.open_arc(imported_graph.ArcId).to_yaml())
assert exported.HasParts[0] is exported.HasParts[1]
imported_graph.close()
memory = Session.create_in_memory()
imported = ARC.import_folder(memory, destination)
assert imported.Model.Processes[0].Input.Name == "first"
memory.close()
session.close()
rejects(lambda: resumed.Sample.list())
print("Native Python management and workspace checks passed.")
