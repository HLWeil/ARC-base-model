// Executable native example and consumer checks; run through TestBaseModelNative.
import assert from "node:assert/strict";
import {
  Agent, Annotation, Data, Dataset, DefinedTerm, DefinedTermSet, Descriptor,
  FormalParameter, Organization, Process, Recipe, Sample, ScholarlyArticle,
} from "arc-base-model";
import { MappingProbe } from "arc-base-model-probe";

const annotation = new Annotation("temperature");
const parameter = new FormalParameter();
const termSet = new DefinedTermSet("Ontology for Biomedical Investigations");
const term = new DefinedTerm("measurement");
const sample = new Sample("leaf");
const data = new Data("measurements.csv");
const recipe = new Recipe();
const process = new Process("measure leaf");
const descriptor = new Descriptor(sample);
const organization = new Organization("Plant laboratory");
const agent = new Agent("Researcher");
const article = new ScholarlyArticle("Leaf measurements");
const dataset = new Dataset(
  ["administrative", "process-provenance", "semantic-designation", "custom-profile"],
  ["example-study"],
);

// Every entity is available from the package entrypoint with ordinary constructors.
const entities = [
  [Annotation, annotation], [FormalParameter, parameter], [DefinedTermSet, termSet],
  [DefinedTerm, term], [Sample, sample], [Data, data], [Recipe, recipe],
  [Process, process], [Descriptor, descriptor], [Organization, organization],
  [Agent, agent], [ScholarlyArticle, article], [Dataset, dataset],
];
for (const [Entity, entity] of entities) {
  assert.ok(entity instanceof Entity);
  assert.equal(entity.Type, Entity.name);
  assert.equal(entity.Id, undefined);
  assert.ok(Array.isArray(entity.AdditionalTypes));
  entity.Id = `local:${entity.Type}`;
  assert.equal(entity.Id, `local:${entity.Type}`);
  entity.Id = undefined;
  assert.equal(entity.Id, undefined);
  assert.throws(() => { entity.Type = "Changed"; });
}

// Direct native values must be understood by transpiled F# pattern matching.
for (const value of [42, 0, 23.5, "42", ""]) {
  assert.equal(MappingProbe.IsNumber(value), typeof value === "number");
  assert.equal(MappingProbe.IsText(value), typeof value === "string");
  assert.equal(MappingProbe.ClassifyNumberFirst(value), typeof value === "number" ? "number" : "text");
}
const generatedAnnotation = MappingProbe.CreateNumericAnnotation();
assert.ok(generatedAnnotation instanceof Annotation);
assert.equal(generatedAnnotation.Value, 23.5);
assert.equal(typeof generatedAnnotation.Value, "number");

for (const value of [42, 0, 23.5, "42", "", undefined]) {
  annotation.Value = value;
  assert.equal(annotation.Value, value);
  const row = MappingProbe.WriteAnnotation(annotation.Value);
  assert.equal(row.Text, typeof value === "string" ? value : undefined);
  assert.equal(row.Number, typeof value === "number" ? value : undefined);
  assert.equal(MappingProbe.ReadAnnotation(row), value);
}

for (const value of [sample, data, undefined]) {
  process.Input = value;
  process.Output = value;
  const row = MappingProbe.WriteEntity(process.Input);
  assert.equal(row.Sample, value instanceof Sample ? value : undefined);
  assert.equal(row.Data, value instanceof Data ? value : undefined);
  assert.equal(MappingProbe.ReadEntity(row), value);
  assert.equal(process.Output, value);
  if (value !== undefined) {
    descriptor.Describes = value;
    assert.equal(descriptor.Describes, value);
    assert.ok(MappingProbe.ReadEntity(row) instanceof value.constructor);
  }
}

for (const value of ["measurement", "", term, undefined]) {
  recipe.IntendedUse = value;
  const row = MappingProbe.WriteIntendedUse(recipe.IntendedUse);
  assert.equal(row.Text, typeof value === "string" ? value : undefined);
  assert.equal(row.Term, value instanceof DefinedTerm ? value : undefined);
  assert.equal(MappingProbe.ReadIntendedUse(row), value);
}

for (const value of ["https://example.org/ontology", "", termSet, undefined]) {
  term.InDefinedTermSet = value;
  const row = MappingProbe.WriteTermSet(term.InDefinedTermSet);
  assert.equal(row.Url, typeof value === "string" ? value : undefined);
  assert.equal(row.TermSet, value instanceof DefinedTermSet ? value : undefined);
  assert.equal(MappingProbe.ReadTermSet(row), value);
}

// Both populated columns would be an invalid union representation.
const invalidAnnotation = MappingProbe.WriteAnnotation("42");
invalidAnnotation.Number = 42;
assert.throws(() => MappingProbe.ReadAnnotation(invalidAnnotation));
const invalidEntity = MappingProbe.WriteEntity(sample);
invalidEntity.Data = data;
assert.throws(() => MappingProbe.ReadEntity(invalidEntity));
const invalidIntendedUse = MappingProbe.WriteIntendedUse("measurement");
invalidIntendedUse.Term = term;
assert.throws(() => MappingProbe.ReadIntendedUse(invalidIntendedUse));
const invalidTermSet = MappingProbe.WriteTermSet("https://example.org/ontology");
invalidTermSet.TermSet = termSet;
assert.throws(() => MappingProbe.ReadTermSet(invalidTermSet));

// A Layer 2 consumer can assign and read domain IDs without changing identity.
assert.equal(MappingProbe.AssignSampleId(sample, "sample-1"), sample);
assert.equal(MappingProbe.ReadSampleId(sample), "sample-1");
assert.equal(sample.Id, "sample-1");
const sameId = new Sample("leaf");
sameId.Id = "sample-1";
assert.notEqual(sample, sameId);
assert.equal(MappingProbe.AssignSampleId(sample, undefined), sample);
assert.equal(sample.Id, undefined);

// Assignment copies containers; getters expose native mutable arrays.
annotation.Value = 23.5;
annotation.Unit = "degree Celsius";
annotation.NameTAN = "https://example.org/temperature";
annotation.ValueTAN = "";
annotation.UnitTAN = "https://example.org/celsius";
const suppliedAnnotations = [annotation, annotation];
sample.AdditionalProperties = suppliedAnnotations;
assert.notEqual(sample.AdditionalProperties, suppliedAnnotations);
suppliedAnnotations.pop();
assert.equal(sample.AdditionalProperties.length, 2);
assert.equal(sample.AdditionalProperties[0], annotation);
assert.equal(sample.AdditionalProperties[1], annotation);
sample.AdditionalProperties.push(annotation);
assert.equal(sample.AdditionalProperties.length, 3);
assert.equal(new Sample("independent").AdditionalProperties.length, 0);
annotation.Value = 24;
assert.equal(sample.AdditionalProperties[2].Value, 24);

const profiles = ["administrative", "administrative"];
const identifiers = ["first", "first"];
const copied = new Dataset(profiles, identifiers);
profiles.push("custom-profile");
identifiers.push("second");
assert.deepEqual(copied.ConformsTo, ["administrative", "administrative"]);
assert.deepEqual(copied.Identifiers, ["first", "first"]);
const replacements = ["replacement", "replacement"];
copied.Identifiers = replacements;
replacements.pop();
assert.deepEqual(copied.Identifiers, ["replacement", "replacement"]);

// The complete graph retains references; construction adds no backlinks or IDs.
parameter.Name = "temperature";
parameter.DefaultValue = annotation;
annotation.InstanceOf = parameter;
recipe.Parameters = [parameter];
recipe.Components = [annotation];
recipe.IntendedUse = term;
term.InDefinedTermSet = termSet;
term.TAN = "OBI:example";
process.Input = sample;
process.Output = data;
process.ExecutesRecipe = recipe;
process.ParameterValues = [annotation, annotation];
descriptor.Describes = sample;
descriptor.Annotations = [annotation];
organization.Url = "https://example.org/lab";
agent.Affiliations = [organization];
agent.JobTitles = [term];
article.Authors = [agent];
article.CreativeWorkStatus = term;
dataset.Processes = [process];
dataset.Descriptors = [descriptor];
dataset.DataFiles = [data];
dataset.Agents = [agent];
dataset.Citations = [article];
dataset.Title = "Leaf measurements";
dataset.Description = "";
assert.equal(dataset.Description, "");
dataset.Description = undefined;
assert.equal(dataset.Description, undefined);
const nested = new Dataset(["semantic-designation"], ["descriptions"]);
const deepest = new Dataset(["process-provenance"], ["measurement-run"]);
dataset.HasParts = [nested, nested];
nested.HasParts.push(deepest);
const fragment = new Data("measurements.csv");
fragment.Selector = "row=2";
data.HasParts = [fragment, fragment];
assert.equal(dataset.Processes[0].Input, dataset.Descriptors[0].Describes);
assert.equal(dataset.Processes[0].Output, dataset.DataFiles[0]);
assert.equal(dataset.Processes[0].ParameterValues[1], annotation);
assert.equal(annotation.InstanceOf.DefaultValue, annotation);
assert.equal(dataset.Citations[0].Authors[0], dataset.Agents[0]);
assert.equal(dataset.HasParts[0], dataset.HasParts[1]);
assert.equal(dataset.HasParts[0].HasParts[0], deepest);
assert.equal(data.HasParts[0], data.HasParts[1]);
assert.equal(fragment.Path, "measurements.csv");
assert.equal(sample.Id, undefined);
assert.equal(data.Id, undefined);

// Constructor restrictions come from required spec data, not inferred identity.
for (const Entity of [Annotation, DefinedTerm, DefinedTermSet, Sample, Agent, Organization, Process]) {
  assert.throws(() => new Entity(null));
  assert.throws(() => new Entity());
  assert.equal(new Entity("").Name, "");
  assert.equal(new Entity("  literal  ").Name, "  literal  ");
}
assert.throws(() => new Data(null));
assert.throws(() => new ScholarlyArticle(null));
assert.throws(() => new Descriptor(null));
assert.throws(() => new Dataset([], ["study"]));
assert.throws(() => new Dataset(["administrative"], []));
assert.throws(() => new Dataset(["Administrative"], ["study"]));
assert.throws(() => new Dataset(["custom-profile"], ["study"]));
assert.throws(() => new Dataset(null, ["study"]));
assert.throws(() => new Dataset(["administrative"], null));
assert.equal(new Data("").Path, "");
assert.equal(new ScholarlyArticle("").Headline, "");
copied.ConformsTo.length = 0; // Continuous conformance checking is outside Layer 1.
copied.Identifiers.length = 0;
assert.equal(copied.ConformsTo.length, 0);
assert.equal(copied.Identifiers.length, 0);

console.log("ARCBaseModel native JavaScript consumer passed");
