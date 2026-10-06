from pathlib import Path
from uuid import uuid4
from arc_management import ARC, Dataset, Annotation

folder = str(Path("build/out/management") / ("native-python-" + uuid4().hex))
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
    replacement = Annotation("replaced")
    replacement.Id = value.Id
    replacement.Value = ""
    arc.Annotation.set(replacement)
    assert value.Value == "" and value is not replacement
    arc.save()
    arc.close()
    resumed = ARC.open_folder(folder, "sql")
    try:
        assert resumed.Model.HasParts[0] is resumed.Model.HasParts[1]
        resumed.History.undo()
        restored = resumed.Annotation.get(value.Id)
        assert restored.Value == 0 and type(restored.Value) is float
        assert restored.InstanceOf.DefaultValue is restored
    finally:
        resumed.close()
finally:
    arc.close()
print("Native Python management checks passed.")
