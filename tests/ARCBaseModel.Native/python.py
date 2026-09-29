"""Executable native example and consumer checks; run via TestBaseModelNative."""

from arc_base_model import (
    Agent, Annotation, Data, Dataset, DefinedTerm, DefinedTermSet, Descriptor,
    FormalParameter, Organization, Process, Recipe, Sample, ScholarlyArticle,
)
from arc_base_model_probe import MappingProbe


def rejects(action):
    try:
        action()
    except Exception:
        return
    raise AssertionError("Expected invalid construction or mapping to fail")


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

# Every entity is exported with ordinary constructors, lists, and optional IDs.
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
    assert isinstance(entity.AdditionalTypes, list)
    entity.Id = f"local:{entity.Type}"
    assert entity.Id == f"local:{entity.Type}"
    entity.Id = None
    assert entity.Id is None
    rejects(lambda: setattr(entity, "Type", "Changed"))

# Python ints and floats must both be usable as Number values.
for value in [42, 0, 0.0, 23.5, "42", "", None]:
    annotation.Value = value
    assert annotation.Value == value
    row = MappingProbe.WriteAnnotation(annotation.Value)
    assert row.Text == (value if isinstance(value, str) else None)
    assert row.Number == (value if isinstance(value, (int, float)) else None)
    assert MappingProbe.ReadAnnotation(row) == value

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

for value in [sample, data, None]:
    process.Input = value
    process.Output = value
    row = MappingProbe.WriteEntity(process.Input)
    assert row.Sample is (value if isinstance(value, Sample) else None)
    assert row.Data is (value if isinstance(value, Data) else None)
    assert MappingProbe.ReadEntity(row) is value
    assert process.Output is value
    if value is not None:
        descriptor.Describes = value
        assert descriptor.Describes is value
        assert isinstance(MappingProbe.ReadEntity(row), type(value))

for value in ["measurement", "", term, None]:
    recipe.IntendedUse = value
    row = MappingProbe.WriteIntendedUse(recipe.IntendedUse)
    assert row.Text == (value if isinstance(value, str) else None)
    assert row.Term is (value if isinstance(value, DefinedTerm) else None)
    result = MappingProbe.ReadIntendedUse(row)
    assert result is value if isinstance(value, DefinedTerm) else result == value

for value in ["https://example.org/ontology", "", term_set, None]:
    term.InDefinedTermSet = value
    row = MappingProbe.WriteTermSet(term.InDefinedTermSet)
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

# A Layer 2 consumer can assign IDs without replacing the model object.
assert MappingProbe.AssignSampleId(sample, "sample-1") is sample
assert MappingProbe.ReadSampleId(sample) == "sample-1"
assert sample.Id == "sample-1"
same_id = Sample("leaf")
same_id.Id = "sample-1"
assert same_id is not sample
assert same_id != sample
assert MappingProbe.AssignSampleId(sample, None) is sample
assert sample.Id is None

# Assignment copies containers; getters expose live native lists.
annotation.Value = 23.5
annotation.Unit = "degree Celsius"
annotation.NameTAN = "https://example.org/temperature"
annotation.ValueTAN = ""
annotation.UnitTAN = "https://example.org/celsius"
supplied_annotations = [annotation, annotation]
sample.AdditionalProperties = supplied_annotations
assert sample.AdditionalProperties is not supplied_annotations
supplied_annotations.pop()
assert len(sample.AdditionalProperties) == 2
assert sample.AdditionalProperties[0] is annotation
assert sample.AdditionalProperties[1] is annotation
sample.AdditionalProperties.append(annotation)
assert len(sample.AdditionalProperties) == 3
assert len(Sample("independent").AdditionalProperties) == 0
annotation.Value = 24
assert sample.AdditionalProperties[2].Value == 24

profiles = ["administrative", "administrative"]
identifiers = ["first", "first"]
copied = Dataset(profiles, identifiers)
profiles.append("custom-profile")
identifiers.append("second")
assert copied.ConformsTo == ["administrative", "administrative"]
assert copied.Identifiers == ["first", "first"]
replacements = ["replacement", "replacement"]
copied.Identifiers = replacements
replacements.pop()
assert copied.Identifiers == ["replacement", "replacement"]

# Mixed profiles, recursive parts, and cyclic parameter links use shared objects.
parameter.Name = "temperature"
parameter.DefaultValue = annotation
annotation.InstanceOf = parameter
recipe.Parameters = [parameter]
recipe.Components = [annotation]
recipe.IntendedUse = term
term.InDefinedTermSet = term_set
term.TAN = "OBI:example"
process.Input = sample
process.Output = data
process.ExecutesRecipe = recipe
process.ParameterValues = [annotation, annotation]
descriptor.Describes = sample
descriptor.Annotations = [annotation]
organization.Url = "https://example.org/lab"
agent.Affiliations = [organization]
agent.JobTitles = [term]
article.Authors = [agent]
article.CreativeWorkStatus = term
dataset.Processes = [process]
dataset.Descriptors = [descriptor]
dataset.DataFiles = [data]
dataset.Agents = [agent]
dataset.Citations = [article]
dataset.Title = "Leaf measurements"
dataset.Description = ""
assert dataset.Description == ""
dataset.Description = None
assert dataset.Description is None
nested = Dataset(["semantic-designation"], ["descriptions"])
deepest = Dataset(["process-provenance"], ["measurement-run"])
dataset.HasParts = [nested, nested]
nested.HasParts.append(deepest)
fragment = Data("measurements.csv")
fragment.Selector = "row=2"
data.HasParts = [fragment, fragment]
assert dataset.Processes[0].Input is dataset.Descriptors[0].Describes
assert dataset.Processes[0].Output is dataset.DataFiles[0]
assert dataset.Processes[0].ParameterValues[1] is annotation
assert annotation.InstanceOf.DefaultValue is annotation
assert dataset.Citations[0].Authors[0] is dataset.Agents[0]
assert dataset.HasParts[0] is dataset.HasParts[1]
assert dataset.HasParts[0].HasParts[0] is deepest
assert data.HasParts[0] is data.HasParts[1]
assert fragment.Path == "measurements.csv"
assert sample.Id is None
assert data.Id is None

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
copied.ConformsTo.clear()  # Continuous conformance checking is outside Layer 1.
copied.Identifiers.clear()
assert copied.ConformsTo == []
assert copied.Identifiers == []

print("ARCBaseModel native Python consumer passed")
