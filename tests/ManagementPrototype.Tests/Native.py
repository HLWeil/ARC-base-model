from pathlib import Path
from uuid import uuid4
from arc_session import ARC, Session
from arc_base_model import Dataset, Annotation, Sample

folder = str(Path("build/out/arc-session") / ("native-python-" + uuid4().hex))
arc = ARC.create(folder, Dataset(["process-provenance", "semantic-designation", "administrative"], ["native"]))
try:
    sample = arc.Sample.create("sample")
    data = arc.Data.create("results.csv")
    fragment = arc.Data.create("results.csv")
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
    quality = arc.Entity.create("QualityAssessment")
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

def rejects(action):
    try:
        action()
    except Exception:
        return
    raise AssertionError("Expected rejection")

path = str(Path(folder) / "collection.sqlite")
session = Session.create_file(path)
first = session.create_arc(root())
second = session.create_arc(root())
sample = Sample("first")
sample.Id = "same"
first.Sample.upsert(sample)
other = Sample("second")
other.Id = "same"
second.Sample.upsert(other)
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
first.bind_folder(destination)
assert not (Path(destination) / "arc.yml").exists()
first.save()
first_id, second_id = first.ArcId, second.ArcId
first.close()
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
memory = Session.create_in_memory()
imported = ARC.import_folder(memory, destination)
assert imported.Model.Processes[0].Input.Name == "first"
memory.close()
session.close()
rejects(lambda: resumed.save())
print("Native Python ARC session checks passed.")
