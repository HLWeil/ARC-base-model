from pathlib import Path
from uuid import uuid4
import arc_management
from arc_management import DatasetYaml, ManagedGraph, Session
from arc_management.arctrl import ARC
from arc_base_model import Dataset, Annotation, Sample, Data, EntityObject, EntityCollection, EntityNull, EntityBlob


def rejects(action):
    try:
        action()
    except Exception:
        return
    raise AssertionError("Expected rejection")


assert not hasattr(arc_management, "ARC")
# Immutable values cannot form cycles, so the test decodes the cyclic fixture from anchored YAML.
source_yaml = "\n".join([
    "&o1",
    "type: Dataset",
    "additionalProperties:",
    "    -",
    "        &o4",
    "        type: Annotation",
    "        name: zero",
    "        value: 0",
    "    -",
    "        *o4",
    "conformsTo: [process-provenance]",
    "hasParts:",
    "    -",
    "        &o2",
    "        type: Dataset",
    "        id: same",
    "        conformsTo: [administrative]",
    "        identifiers: [shared]",
    "        title: \"\"",
    "    -",
    "        *o2",
    "    -",
    "        &o3",
    "        type: Dataset",
    "        id: same",
    "        conformsTo: [semantic-designation]",
    "        identifiers: [distinct]",
    "    -",
    "        *o1",
    "identifiers: [detached]",
    "self:",
    "    *o1",
    "",
])
detached = DatasetYaml.decode(source_yaml)
zero = detached.AdditionalProperties[0]
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
# A Python int supplied natively must encode as a number and decode as a float.
native_zero = Annotation("zero", 0)
native_numeric = Dataset.with_additional_properties(Dataset(["administrative"], ["native-number"]), [native_zero, native_zero])
native_decoded = DatasetYaml.decode(DatasetYaml.encode(native_numeric))
assert native_decoded.AdditionalProperties[0] is native_decoded.AdditionalProperties[1]
assert native_decoded.AdditionalProperties[0].Value == 0 and type(native_decoded.AdditionalProperties[0].Value) is float
rejects(lambda: DatasetYaml.decode("not: [valid YAML"))
rejects(lambda: DatasetYaml.decode("type: Sample\nname: invalid root\n"))

folder = str(Path("build/out/arc-management") / ("native-python-" + uuid4().hex))
arc = ARC.create(folder, Dataset(["process-provenance", "semantic-designation", "administrative"], ["native"]))
try:
    sample = arc.Sample.add(Sample("sample"))
    data0 = arc.Data.add(Data("results.csv"))
    fragment0 = arc.Data.add(Data("results.csv"))
    fragment = arc.Data.set_selector(fragment0, "row=1")
    assert fragment0.Selector is None and fragment.Selector == "row=1"
    data = arc.Data.set_has_parts(data0, [fragment, fragment])
    value = arc.Annotation.create("value")
    value = arc.Annotation.set_value(value, 0)
    assert value.Value == 0 and type(value.Value) is float
    parameter = arc.FormalParameter.create("parameter")
    parameter = arc.FormalParameter.set_default_value(parameter, value)
    value = arc.Annotation.set_instance_of(value, parameter)
    recipe0 = arc.Recipe.create("recipe")
    arc.Recipe.set_parameters(recipe0, [parameter, parameter])
    arc.Recipe.set_components(recipe0, [value, value])
    termset = arc.DefinedTermSet.create("ontology")
    term = arc.DefinedTerm.set_in_defined_term_set(arc.DefinedTerm.create("term"), termset)
    recipe = arc.Recipe.set_intended_use(recipe0, term)
    proc0 = arc.Process.create("process")
    arc.Process.set_input(proc0, sample)
    arc.Process.set_output(proc0, data)
    proc = arc.Process.set_executes_recipe(proc0, recipe)
    assert proc.Input.Id == sample.Id and proc0.Input is None
    descriptor = arc.Descriptor.set_annotations(arc.Descriptor.create(sample), [value, value])
    organization = arc.Organization.create("organization")
    agent0 = arc.Agent.create("agent")
    arc.Agent.set_affiliations(agent0, [organization, organization])
    agent = arc.Agent.set_job_titles(agent0, [term])
    article = arc.ScholarlyArticle.set_authors(arc.ScholarlyArticle.create("article"), [agent, agent])
    child = arc.Dataset.create("child")
    arc.Dataset.set_has_parts(arc.Model, [child, child])
    arc.Dataset.set_processes(arc.Model, [proc])
    arc.Dataset.set_data_files(arc.Model, [data, fragment, data])
    arc.Dataset.set_descriptors(arc.Model, [descriptor])
    arc.Dataset.set_agents(arc.Model, [agent])
    model = arc.Dataset.set_citations(arc.Model, [article])
    assert type(arc.Data.list()) is list
    assert model.HasParts[0] is model.HasParts[1]
    assert model.DataFiles[0].HasParts[0] is model.DataFiles[0].HasParts[1]
    quality = arc.Entity.add(EntityObject("QualityAssessment"))
    quality = arc.Entity.set_property(quality, "score", 0)
    quality = arc.Entity.set_property(quality, "accepted", False)
    quality = arc.Entity.set_property(quality, "missing", EntityNull())
    quality = arc.Entity.set_property(quality, "bytes", EntityBlob("AA=="))
    quality = arc.Entity.set_property(quality, "values", EntityCollection([0, False, ""]))
    arc.Entity.set_property(arc.Model, "quality", quality)
    assert quality.EntityProperties.Get("score") == 0
    assert quality.EntityProperties.Get("accepted") is False
    assert quality.EntityProperties.Contains("missing")
    replacement = EntityObject.with_id(Annotation("replaced", value=""), value.Id)
    replaced = arc.Annotation.upsert(replacement)
    assert replaced.Value == "" and value.Value == 0 and value is not replaced
    assert arc.Annotation.upsert(replacement, recursive=True).Value == ""
    arc.save()
    arc.close()
    resumed = ARC.open_folder(folder, "sql")
    try:
        assert resumed.Model.HasParts[0] is resumed.Model.HasParts[1]
        recovered_quality = resumed.Entity.get(quality.Id)
        assert resumed.Model.EntityProperties.Get("quality") is recovered_quality
        assert recovered_quality.EntityProperties.Get("score") == 0
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
    return EntityObject.with_id(Dataset(["process-provenance"], ["native"]), "root")

path = str(Path(folder) / "collection.sqlite")
session = Session.create_file(path)
first = session.create_arc(root())
second = session.create_arc(root())
assert isinstance(first, ManagedGraph)
assert not hasattr(first, "save") and not hasattr(first, "bind_folder")
rejects(lambda: session.import_yaml(detached_yaml))
sample = Sample("first", id="same")
stored = first.Sample.add(sample)
assert stored.Id == "same" and stored is not sample
other = Sample("second", id="same")
second.Sample.add(other)
numeric = first.Annotation.create("numeric boundary")
numeric = first.Annotation.set_value(numeric, 0)
assert type(numeric.Value) is float
rejects(lambda: first.Annotation.set_value(numeric, False))
process = first.Process.create("process")
rejects(lambda: first.Process.set_input(process, Sample("unknown", id="unknown-id")))
# A value from another ARC resolves by ID in the destination.
assert first.Process.set_input(process, other).Input.Name == "first"
first.Dataset.add_process(first.Model, process)
renamed = first.Sample.set_name(stored, "changed")
assert renamed.Name == "changed" and stored.Name == "first"
first.History.undo()
assert first.Sample.get("same").Name == "first" and renamed.Name == "changed"
assert second.Sample.get("same").Name == "second"
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
second.Sample.set_name(second.Sample.get("same"), "independent")
session.close()
session = Session.open_file(path)
resumed = session.open_arc(first_id)
assert resumed.Model.Processes[0].Input.Id == resumed.Sample.get("same").Id
resumed.History.redo()
assert resumed.Sample.get("same").Name == "changed"
assert session.open_arc(second_id).Sample.get("same").Name == "independent"
rejects(lambda: Session.create_file(path))
import_child = Dataset(["administrative"], ["child"])
import_root = Dataset.with_has_parts(Dataset(["process-provenance"], ["import"]), [import_child, import_child])
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
