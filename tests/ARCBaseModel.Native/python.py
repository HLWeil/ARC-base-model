"""Executable native example and consumer checks; run via TestBaseModelNative."""

from arc_base_model import (
    EntityObject, EntityCollection, EntityNull, EntityBlob, Agent, Annotation, Data, Dataset, DefinedTerm, DefinedTermSet, Descriptor,
    FormalParameter, Organization, Process, Recipe, Sample, ScholarlyArticle,
)
from arc_base_model_probe import MappingProbe


def rejects(action, error=Exception):
    try:
        action()
    except error:
        return
    raise AssertionError("Expected invalid construction or mapping to fail")


def generated(items, counter=None):
    # A generator can be read once, so it shows that inputs are consumed eagerly and exactly once.
    for item in items:
        if counter is not None:
            counter["reads"] += 1
        yield item


def assert_immutable(collection):
    assert isinstance(collection, tuple)
    assert not hasattr(collection, "append")
    rejects(lambda: collection.append("extra"), AttributeError)


def assign(target, name, value):
    setattr(target, name, value)


annotation = Annotation("temperature")
parameter = FormalParameter()
term_set = DefinedTermSet("Ontology for Biomedical Investigations")
term = DefinedTerm("measurement")
sample = Sample("leaf")
data = Data("measurements.csv")
recipe = Recipe()
process = Process("measure leaf")
descriptor = Descriptor(sample)
organization = Organization("Plant laboratory")
agent = Agent("Researcher")
article = ScholarlyArticle("Leaf measurements")
dataset = Dataset(
    ["administrative", "process-provenance", "semantic-designation", "custom-profile"],
    ["example-study"],
)

# Every entity is exported with ordinary constructors that accept iterables and optional IDs.
entities = [
    (Annotation, annotation), (FormalParameter, parameter), (DefinedTermSet, term_set),
    (DefinedTerm, term), (Sample, sample), (Data, data), (Recipe, recipe),
    (Process, process), (Descriptor, descriptor), (Organization, organization),
    (Agent, agent), (ScholarlyArticle, article), (Dataset, dataset),
]
for entity_class, entity in entities:
    assert isinstance(entity, entity_class)
    assert entity.Type == entity_class.__name__
    assert entity.Id is None
    assert_immutable(entity.AdditionalTypes)
    rejects(lambda: assign(entity, "Type", "Changed"), AttributeError)
    rejects(lambda: assign(entity, "Id", "changed"), AttributeError)
    rejects(lambda: assign(entity, "AdditionalTypes", ()), AttributeError)
    # The generic copy keeps the concrete class and leaves the source alone. None clears the ID.
    identified = EntityObject.with_id(entity, f"local:{entity.Type}")
    assert isinstance(identified, entity_class)
    assert identified is not entity
    assert identified.Id == f"local:{entity.Type}"
    assert entity.Id is None
    assert EntityObject.with_id(identified, None).Id is None
    assert identified.Id == f"local:{entity.Type}"

# Getter-only properties reject assignment.
rejects(lambda: assign(sample, "Name", "other"), AttributeError)
rejects(lambda: assign(sample, "AdditionalProperties", ()), AttributeError)
rejects(lambda: assign(annotation, "Value", 1), AttributeError)
rejects(lambda: assign(dataset, "Title", "other"), AttributeError)

# Both exhaustive and wildcard F# matches accept native numbers. Python bool is
# an int subclass, but it must not satisfy the numeric compatibility predicate.
for value in [42, 0, 0.0, 23.5, "42", ""]:
    is_number = type(value) in (int, float)
    assert MappingProbe.IsNumber(value) is is_number
    assert MappingProbe.IsText(value) is isinstance(value, str)
    assert MappingProbe.ClassifyNumberFirst(value) == ("number" if is_number else "text")
assert MappingProbe.IsNumber(True) is False
assert MappingProbe.IsNumber(False) is False

# Values constructed by compiled F# are ordinary numbers at the public boundary.
generated_annotation = MappingProbe.CreateNumericAnnotation()
assert isinstance(generated_annotation, Annotation)
assert type(generated_annotation.Value) is float
assert generated_annotation.Value == 23.5
generated_row = MappingProbe.WriteAnnotation(generated_annotation.Value)
assert type(generated_row.Number) is float
assert type(MappingProbe.ReadAnnotation(generated_row)) is float
assert MappingProbe.ReadAnnotation(generated_row) == 23.5

# Python ints and floats must both be usable as Number values, and None clears.
for value in [42, 0, 0.0, 23.5, "42", "", None]:
    valued = Annotation.with_value(annotation, value)
    assert valued is not annotation
    assert valued.Value == value
    assert type(valued.Value) is type(value)
    assert annotation.Value is None
    row = MappingProbe.WriteAnnotation(valued.Value)
    assert row.Text == (value if isinstance(value, str) else None)
    assert row.Number == (value if isinstance(value, (int, float)) else None)
    assert MappingProbe.ReadAnnotation(row) == value
assert Annotation.with_value(annotation, 1).Value == 1
assert type(Annotation.with_value(annotation, 1).Value) in (int, float)
united = Annotation.with_unit(annotation, "")
assert united.Unit == ""
assert Annotation.with_unit(united, None).Unit is None
assert united.Unit == ""

endpoints = process
for value in [sample, data, None]:
    endpoints = Process.with_output(Process.with_input(endpoints, value), value)
    row = MappingProbe.WriteEntity(endpoints.Input)
    assert row.Sample is (value if isinstance(value, Sample) else None)
    assert row.Data is (value if isinstance(value, Data) else None)
    assert MappingProbe.ReadEntity(row) is value
    assert endpoints.Output is value
    if value is not None:
        described = Descriptor.with_describes(descriptor, value)
        assert described.Describes is value
        assert isinstance(MappingProbe.ReadEntity(row), type(value))
assert process.Input is None
assert process.Output is None
assert descriptor.Describes is sample

for value in ["measurement", "", term, None]:
    used = Recipe.with_intended_use(recipe, value)
    assert used.IntendedUse is value if isinstance(value, DefinedTerm) else used.IntendedUse == value
    assert recipe.IntendedUse is None
    row = MappingProbe.WriteIntendedUse(used.IntendedUse)
    assert row.Text == (value if isinstance(value, str) else None)
    assert row.Term is (value if isinstance(value, DefinedTerm) else None)
    result = MappingProbe.ReadIntendedUse(row)
    assert result is value if isinstance(value, DefinedTerm) else result == value

for value in ["https://example.org/ontology", "", term_set, None]:
    classified = DefinedTerm.with_in_defined_term_set(term, value)
    assert classified.InDefinedTermSet is value if isinstance(value, DefinedTermSet) else classified.InDefinedTermSet == value
    assert term.InDefinedTermSet is None
    row = MappingProbe.WriteTermSet(classified.InDefinedTermSet)
    assert row.Url == (value if isinstance(value, str) else None)
    assert row.TermSet is (value if isinstance(value, DefinedTermSet) else None)
    result = MappingProbe.ReadTermSet(row)
    assert result is value if isinstance(value, DefinedTermSet) else result == value

invalid_annotation = MappingProbe.WriteAnnotation("42")
invalid_annotation.Number = 42
rejects(lambda: MappingProbe.ReadAnnotation(invalid_annotation))
invalid_entity = MappingProbe.WriteEntity(sample)
invalid_entity.Data = data
rejects(lambda: MappingProbe.ReadEntity(invalid_entity))
invalid_intended_use = MappingProbe.WriteIntendedUse("measurement")
invalid_intended_use.Term = term
rejects(lambda: MappingProbe.ReadIntendedUse(invalid_intended_use))
invalid_term_set = MappingProbe.WriteTermSet("https://example.org/ontology")
invalid_term_set.TermSet = term_set
rejects(lambda: MappingProbe.ReadTermSet(invalid_term_set))

# A Layer 2 consumer receives a new object with the domain ID and the source stays as it was.
assigned = MappingProbe.AssignSampleId(sample, "sample-1")
assert assigned is not sample
assert isinstance(assigned, Sample)
assert MappingProbe.ReadSampleId(assigned) == "sample-1"
assert assigned.Id == "sample-1"
assert sample.Id is None
same_id = MappingProbe.AssignSampleId(Sample("leaf"), "sample-1")
assert same_id is not assigned
assert same_id != assigned
assert MappingProbe.AssignSampleId(assigned, None).Id is None
assert assigned.Id == "sample-1"

# Copies consume native iterables once and publish tuples.
counter = {"reads": 0}
from_generator = Sample("generated", generated([annotation, annotation], counter))
assert counter["reads"] == 2
assert_immutable(from_generator.AdditionalProperties)
assert len(from_generator.AdditionalProperties) == 2
assert from_generator.AdditionalProperties[0] is annotation
assert from_generator.AdditionalProperties[1] is annotation
supplied_annotations = [annotation, annotation]
with_annotations = Sample.with_additional_properties(sample, supplied_annotations)
assert with_annotations is not sample
assert with_annotations.AdditionalProperties is not supplied_annotations
supplied_annotations.pop()
assert len(with_annotations.AdditionalProperties) == 2
assert_immutable(with_annotations.AdditionalProperties)
assert len(sample.AdditionalProperties) == 0
assert len(Sample("independent").AdditionalProperties) == 0
renamed = Sample.with_name(with_annotations, "renamed")
assert renamed.Name == "renamed"
assert with_annotations.Name == "leaf"
assert renamed.AdditionalProperties is with_annotations.AdditionalProperties
rejects(lambda: Sample.with_name(Data("p"), "n"))
rejects(lambda: Sample.with_name(None, "n"))
appended = Sample.with_additional_properties(with_annotations, (*with_annotations.AdditionalProperties, annotation))
assert len(appended.AdditionalProperties) == 3
assert len(with_annotations.AdditionalProperties) == 2
typed = EntityObject.with_additional_types(sample, generated(["custom", "custom"]))
assert isinstance(typed, Sample)
assert typed.AdditionalTypes == ("custom", "custom")
assert_immutable(typed.AdditionalTypes)
assert len(sample.AdditionalTypes) == 0

# Optional annotation fields and TANs round-trip, including empty text.
detailed = Annotation("temperature", 23.5, "degree Celsius", "https://example.org/temperature", "", "https://example.org/celsius")
assert detailed.Value == 23.5
assert detailed.Unit == "degree Celsius"
assert detailed.ValueTAN == ""
assert Annotation.with_value_tan(detailed, None).ValueTAN is None
assert detailed.ValueTAN == ""

profiles = ["administrative", "administrative"]
identifiers = ["first", "first"]
copied = Dataset(profiles, identifiers)
profiles.append("custom-profile")
identifiers.append("second")
assert copied.ConformsTo == ("administrative", "administrative")
assert copied.Identifiers == ("first", "first")
assert_immutable(copied.ConformsTo)
assert_immutable(copied.Identifiers)
replacements = ["replacement", "replacement"]
reidentified = Dataset.with_identifiers(copied, replacements)
replacements.pop()
assert reidentified.Identifiers == ("replacement", "replacement")
assert copied.Identifiers == ("first", "first")
assert reidentified.ConformsTo is copied.ConformsTo
rejects(lambda: Dataset.with_conforms_to(copied, []))
rejects(lambda: Dataset.with_conforms_to(copied, ["custom-profile"]))
rejects(lambda: Dataset.with_identifiers(copied, []))

# The complete graph retains references and duplicates. Copies add no backlinks or IDs.
named_parameter = FormalParameter.with_name(parameter, "temperature")
linked_parameter = FormalParameter.with_default_value(named_parameter, detailed)
linked_annotation = Annotation.with_instance_of(detailed, linked_parameter)
assert linked_parameter.DefaultValue is detailed
assert linked_annotation.InstanceOf is linked_parameter
assert detailed.InstanceOf is None
assert parameter.Name is None
linked_term = DefinedTerm.with_tan(DefinedTerm.with_in_defined_term_set(term, term_set), "OBI:example")
assert linked_term.InDefinedTermSet is term_set
assert linked_term.TAN == "OBI:example"
full_recipe = Recipe.with_intended_use(
    Recipe.with_components(Recipe.with_parameters(recipe, [linked_parameter]), [linked_annotation]), linked_term)
full_process = Process.with_parameter_values(
    Process.with_executes_recipe(Process.with_output(Process.with_input(process, sample), data), full_recipe),
    [linked_annotation, linked_annotation])
full_descriptor = Descriptor.with_annotations(Descriptor.with_describes(descriptor, sample), [linked_annotation])
full_organization = Organization.with_url(organization, "https://example.org/lab")
full_agent = Agent.with_job_titles(Agent.with_affiliations(agent, [full_organization]), [linked_term])
full_article = ScholarlyArticle.with_creative_work_status(ScholarlyArticle.with_authors(article, [full_agent]), linked_term)
nested = Dataset(["semantic-designation"], ["descriptions"])
deepest = Dataset(["process-provenance"], ["measurement-run"])
nested_with_part = Dataset.with_has_parts(nested, [deepest])
fragment = Data.with_selector(Data("measurements.csv"), "row=2")
full_data = Data.with_has_parts(data, [fragment, fragment])
full_dataset = Dataset.with_title(dataset, "Leaf measurements")
full_dataset = Dataset.with_description(full_dataset, "")
full_dataset = Dataset.with_processes(full_dataset, [full_process])
full_dataset = Dataset.with_descriptors(full_dataset, [full_descriptor])
full_dataset = Dataset.with_data_files(full_dataset, [full_data])
full_dataset = Dataset.with_agents(full_dataset, [full_agent])
full_dataset = Dataset.with_citations(full_dataset, [full_article])
full_dataset = Dataset.with_has_parts(full_dataset, [nested_with_part, nested_with_part])
assert full_dataset.Description == ""
assert Dataset.with_description(full_dataset, None).Description is None
assert full_dataset.Description == ""
assert dataset.Title is None
assert len(dataset.Processes) == 0
assert full_dataset.Processes[0].Input is full_dataset.Descriptors[0].Describes
assert full_dataset.Processes[0].Output is data
assert full_dataset.Processes[0].ParameterValues[1] is linked_annotation
assert full_dataset.Processes[0].ExecutesRecipe.Parameters[0].DefaultValue is detailed
assert full_dataset.Citations[0].Authors[0] is full_dataset.Agents[0]
assert full_dataset.HasParts[0] is full_dataset.HasParts[1]
assert full_dataset.HasParts[0].HasParts[0] is deepest
assert full_dataset.DataFiles[0].HasParts[0] is full_dataset.DataFiles[0].HasParts[1]
assert full_dataset.DataFiles[0].HasParts[0].Path == "measurements.csv"
assert len(data.HasParts) == 0
assert sample.Id is None
assert data.Id is None
assert_immutable(full_dataset.HasParts)

for entity_class in [Annotation, DefinedTerm, DefinedTermSet, Sample, Agent, Organization, Process]:
    rejects(lambda: entity_class(None))
    rejects(lambda: entity_class())
    assert entity_class("").Name == ""
    assert entity_class("  literal  ").Name == "  literal  "
rejects(lambda: Data(None))
rejects(lambda: ScholarlyArticle(None))
rejects(lambda: Descriptor(None))
rejects(lambda: Dataset([], ["study"]))
rejects(lambda: Dataset(["administrative"], []))
rejects(lambda: Dataset(["Administrative"], ["study"]))
rejects(lambda: Dataset(["custom-profile"], ["study"]))
rejects(lambda: Dataset(None, ["study"]))
rejects(lambda: Dataset(["administrative"], None))
assert Data("").Path == ""
assert ScholarlyArticle("").Headline == ""

print("ARCBaseModel native Python consumer passed")

extension = EntityObject("QualityAssessment")
assert extension.Id is None
extension_values = [0, 42, 0.95, False, "", extension, EntityCollection([]), EntityNull(), EntityBlob("AA==")]
extension_kinds = ["number", "number", "number", "bool", "text", "object", "collection", "null", "blob"]
extended = sample
for index, (value, kind) in enumerate(zip(extension_values, extension_kinds)):
    extended = EntityObject.with_property(extended, f"custom:{index}", value)
    assert isinstance(extended, Sample)
    assert extended.EntityProperties.Get(f"custom:{index}") is value
    assert MappingProbe.ClassifyExtension(value) == kind
assert len(sample.EntityProperties.Keys) == 0
assert len(extended.EntityProperties.Keys) == len(extension_values)
assert_immutable(extended.EntityProperties.Keys)
assert extended.EntityProperties.Contains("custom:3")
assert not extended.EntityProperties.Contains("missing")
rejects(lambda: extended.EntityProperties.Get("missing"))
assert not MappingProbe.IsExtensionNumber(False)
assert MappingProbe.IsExtensionNumber(0)
for reserved in ["type", "id", "additionalTypes", "name"]:
    rejects(lambda: EntityObject.with_property(sample, reserved, "override"))
replaced = EntityObject.with_property(extended, "custom:0", "now text")
assert replaced.EntityProperties.Get("custom:0") == "now text"
assert extended.EntityProperties.Get("custom:0") == 0
removed = EntityObject.without_property(replaced, "custom:0")
assert isinstance(removed, Sample)
assert not removed.EntityProperties.Contains("custom:0")
assert replaced.EntityProperties.Contains("custom:0")
rejects(lambda: assign(extended.EntityProperties, "Keys", ()), AttributeError)

# Extension graphs are built without mutation, so a copy never alters the object it refers to.
holder = EntityObject.with_property(extension, "owner", sample)
assert holder.EntityProperties.Get("owner") is sample
assert not extension.EntityProperties.Contains("owner")
wrapped = MappingProbe.WrapExtensionNumber(extension)
assert wrapped is not extension
assert type(wrapped.EntityProperties.Get("wrapped")) is float
assert wrapped.EntityProperties.Get("wrapped") == 1.25
assert not extension.EntityProperties.Contains("wrapped")
numeric_collection = EntityCollection(generated([extension, extension]))
assert numeric_collection.Count == 2
rejects(lambda: numeric_collection.Get(2))
rejects(lambda: numeric_collection.Get(-1))
longer = MappingProbe.AppendExtensionNumber(numeric_collection)
assert longer is not numeric_collection
assert longer.Count == 3
assert longer.Get(0) is extension
assert type(longer.Get(2)) is float
assert longer.Get(2) == 2.5
assert numeric_collection.Count == 2
for invalid in ["A", "!!!!", "AB==", "AA=A"]:
    rejects(lambda: EntityBlob(invalid))
