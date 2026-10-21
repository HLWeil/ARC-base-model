// Executable native example and consumer checks; run through TestBaseModelNative.
import assert from "node:assert/strict";
import {
  EntityObject, EntityCollection, EntityNull, EntityBlob, Agent, Annotation, Data, Dataset, DefinedTerm, DefinedTermSet, Descriptor,
  FormalParameter, Organization, Process, Recipe, Sample, ScholarlyArticle,
} from "arc-base-model";
import { MappingProbe } from "arc-base-model-probe";

// A generator can be read once, so it shows that inputs are consumed eagerly and exactly once.
function* generated(items, counter = { reads: 0 }) {
  for (const item of items) {
    counter.reads += 1;
    yield item;
  }
}

function assertImmutable(collection, extra) {
  assert.ok(Array.isArray(collection));
  assert.ok(Object.isFrozen(collection));
  assert.throws(() => collection.push(extra), TypeError);
}

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
  assertImmutable(entity.AdditionalTypes, "extra");
  assert.throws(() => { entity.Type = "Changed"; }, TypeError);
  assert.throws(() => { entity.Id = "changed"; }, TypeError);
  assert.throws(() => { entity.AdditionalTypes = []; }, TypeError);
  // The generic copy keeps the concrete class and leaves the source alone. Undefined clears the ID.
  const identified = EntityObject.withId(entity, `local:${entity.Type}`);
  assert.ok(identified instanceof Entity);
  assert.notEqual(identified, entity);
  assert.equal(identified.Id, `local:${entity.Type}`);
  assert.equal(entity.Id, undefined);
  assert.equal(EntityObject.withId(identified, undefined).Id, undefined);
  assert.equal(identified.Id, `local:${entity.Type}`);
}

// Getter-only properties reject assignment on every entity class.
assert.throws(() => { sample.Name = "other"; }, TypeError);
assert.throws(() => { sample.AdditionalProperties = []; }, TypeError);
assert.throws(() => { annotation.Value = 1; }, TypeError);
assert.throws(() => { dataset.Title = "other"; }, TypeError);

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

// Annotation values are copied in and out. Zero and empty text are values, undefined clears.
for (const value of [42, 0, 23.5, "42", "", undefined]) {
  const valued = Annotation.withValue(annotation, value);
  assert.notEqual(valued, annotation);
  assert.equal(valued.Value, value);
  assert.equal(annotation.Value, undefined);
  const row = MappingProbe.WriteAnnotation(valued.Value);
  assert.equal(row.Text, typeof value === "string" ? value : undefined);
  assert.equal(row.Number, typeof value === "number" ? value : undefined);
  assert.equal(MappingProbe.ReadAnnotation(row), value);
}
const united = Annotation.withUnit(annotation, "");
assert.equal(united.Unit, "");
assert.equal(Annotation.withUnit(united, undefined).Unit, undefined);
assert.equal(united.Unit, "");

let endpoints = process;
for (const value of [sample, data, undefined]) {
  endpoints = Process.withOutput(Process.withInput(endpoints, value), value);
  const row = MappingProbe.WriteEntity(endpoints.Input);
  assert.equal(row.Sample, value instanceof Sample ? value : undefined);
  assert.equal(row.Data, value instanceof Data ? value : undefined);
  assert.equal(MappingProbe.ReadEntity(row), value);
  assert.equal(endpoints.Output, value);
  if (value !== undefined) {
    const described = Descriptor.withDescribes(descriptor, value);
    assert.equal(described.Describes, value);
    assert.ok(MappingProbe.ReadEntity(row) instanceof value.constructor);
  }
}
assert.equal(process.Input, undefined);
assert.equal(process.Output, undefined);
assert.equal(descriptor.Describes, sample);

for (const value of ["measurement", "", term, undefined]) {
  const used = Recipe.withIntendedUse(recipe, value);
  assert.equal(used.IntendedUse, value);
  assert.equal(recipe.IntendedUse, undefined);
  const row = MappingProbe.WriteIntendedUse(used.IntendedUse);
  assert.equal(row.Text, typeof value === "string" ? value : undefined);
  assert.equal(row.Term, value instanceof DefinedTerm ? value : undefined);
  assert.equal(MappingProbe.ReadIntendedUse(row), value);
}

for (const value of ["https://example.org/ontology", "", termSet, undefined]) {
  const classified = DefinedTerm.withInDefinedTermSet(term, value);
  assert.equal(classified.InDefinedTermSet, value);
  assert.equal(term.InDefinedTermSet, undefined);
  const row = MappingProbe.WriteTermSet(classified.InDefinedTermSet);
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

// A Layer 2 consumer receives a new object with the domain ID and the source stays as it was.
const assigned = MappingProbe.AssignSampleId(sample, "sample-1");
assert.notEqual(assigned, sample);
assert.ok(assigned instanceof Sample);
assert.equal(MappingProbe.ReadSampleId(assigned), "sample-1");
assert.equal(assigned.Id, "sample-1");
assert.equal(sample.Id, undefined);
const sameId = MappingProbe.AssignSampleId(new Sample("leaf"), "sample-1");
assert.notEqual(assigned, sameId);
assert.equal(MappingProbe.AssignSampleId(assigned, undefined).Id, undefined);
assert.equal(assigned.Id, "sample-1");

// Copies consume native iterables once and publish frozen arrays.
const counter = { reads: 0 };
const fromGenerator = new Sample("generated", generated([annotation, annotation], counter));
assert.equal(counter.reads, 2);
assertImmutable(fromGenerator.AdditionalProperties, annotation);
assert.equal(fromGenerator.AdditionalProperties.length, 2);
assert.equal(fromGenerator.AdditionalProperties[0], annotation);
assert.equal(fromGenerator.AdditionalProperties[1], annotation);
const suppliedAnnotations = [annotation, annotation];
const withAnnotations = Sample.withAdditionalProperties(sample, suppliedAnnotations);
assert.notEqual(withAnnotations, sample);
assert.notEqual(withAnnotations.AdditionalProperties, suppliedAnnotations);
suppliedAnnotations.pop();
assert.equal(withAnnotations.AdditionalProperties.length, 2);
assertImmutable(withAnnotations.AdditionalProperties, annotation);
assert.equal(sample.AdditionalProperties.length, 0);
assert.equal(new Sample("independent").AdditionalProperties.length, 0);
const renamed = Sample.withName(withAnnotations, "renamed");
assert.equal(renamed.Name, "renamed");
assert.equal(withAnnotations.Name, "leaf");
assert.equal(renamed.AdditionalProperties, withAnnotations.AdditionalProperties);
const appended = Sample.withAdditionalProperties(withAnnotations, [...withAnnotations.AdditionalProperties, annotation]);
assert.equal(appended.AdditionalProperties.length, 3);
assert.equal(withAnnotations.AdditionalProperties.length, 2);
const typed = EntityObject.withAdditionalTypes(sample, generated(["custom", "custom"]));
assert.ok(typed instanceof Sample);
assert.deepEqual(typed.AdditionalTypes, ["custom", "custom"]);
assertImmutable(typed.AdditionalTypes, "extra");
assert.equal(sample.AdditionalTypes.length, 0);

// Optional annotation fields and TANs round-trip, including empty text.
const detailed = new Annotation("temperature", 23.5, "degree Celsius", "https://example.org/temperature", "", "https://example.org/celsius");
assert.equal(detailed.Value, 23.5);
assert.equal(detailed.Unit, "degree Celsius");
assert.equal(detailed.ValueTAN, "");
assert.equal(Annotation.withValueTAN(detailed, undefined).ValueTAN, undefined);
assert.equal(detailed.ValueTAN, "");

const profiles = ["administrative", "administrative"];
const identifiers = ["first", "first"];
const copied = new Dataset(profiles, identifiers);
profiles.push("custom-profile");
identifiers.push("second");
assert.deepEqual(copied.ConformsTo, ["administrative", "administrative"]);
assert.deepEqual(copied.Identifiers, ["first", "first"]);
assertImmutable(copied.ConformsTo, "extra");
assertImmutable(copied.Identifiers, "extra");
const replacements = ["replacement", "replacement"];
const reidentified = Dataset.withIdentifiers(copied, replacements);
replacements.pop();
assert.deepEqual(reidentified.Identifiers, ["replacement", "replacement"]);
assert.deepEqual(copied.Identifiers, ["first", "first"]);
assert.equal(reidentified.ConformsTo, copied.ConformsTo);
assert.throws(() => Dataset.withConformsTo(copied, []));
assert.throws(() => Dataset.withConformsTo(copied, ["custom-profile"]));
assert.throws(() => Dataset.withIdentifiers(copied, []));

// The complete graph retains references and duplicates. Copies add no backlinks or IDs.
const namedParameter = FormalParameter.withName(parameter, "temperature");
const linkedParameter = FormalParameter.withDefaultValue(namedParameter, detailed);
const linkedAnnotation = Annotation.withInstanceOf(detailed, linkedParameter);
assert.equal(linkedParameter.DefaultValue, detailed);
assert.equal(linkedAnnotation.InstanceOf, linkedParameter);
assert.equal(detailed.InstanceOf, undefined);
assert.equal(parameter.Name, undefined);
const linkedTerm = DefinedTerm.withTAN(DefinedTerm.withInDefinedTermSet(term, termSet), "OBI:example");
assert.equal(linkedTerm.InDefinedTermSet, termSet);
assert.equal(linkedTerm.TAN, "OBI:example");
const fullRecipe = Recipe.withIntendedUse(Recipe.withComponents(Recipe.withParameters(recipe, [linkedParameter]), [linkedAnnotation]), linkedTerm);
const fullProcess = Process.withParameterValues(
  Process.withExecutesRecipe(Process.withOutput(Process.withInput(process, sample), data), fullRecipe),
  [linkedAnnotation, linkedAnnotation]);
const fullDescriptor = Descriptor.withAnnotations(Descriptor.withDescribes(descriptor, sample), [linkedAnnotation]);
const fullOrganization = Organization.withUrl(organization, "https://example.org/lab");
const fullAgent = Agent.withJobTitles(Agent.withAffiliations(agent, [fullOrganization]), [linkedTerm]);
const fullArticle = ScholarlyArticle.withCreativeWorkStatus(ScholarlyArticle.withAuthors(article, [fullAgent]), linkedTerm);
const nested = new Dataset(["semantic-designation"], ["descriptions"]);
const deepest = new Dataset(["process-provenance"], ["measurement-run"]);
const nestedWithPart = Dataset.withHasParts(nested, [deepest]);
const fragment = Data.withSelector(new Data("measurements.csv"), "row=2");
const fullData = Data.withHasParts(data, [fragment, fragment]);
let fullDataset = Dataset.withTitle(dataset, "Leaf measurements");
fullDataset = Dataset.withDescription(fullDataset, "");
fullDataset = Dataset.withProcesses(fullDataset, [fullProcess]);
fullDataset = Dataset.withDescriptors(fullDataset, [fullDescriptor]);
fullDataset = Dataset.withDataFiles(fullDataset, [fullData]);
fullDataset = Dataset.withAgents(fullDataset, [fullAgent]);
fullDataset = Dataset.withCitations(fullDataset, [fullArticle]);
fullDataset = Dataset.withHasParts(fullDataset, [nestedWithPart, nestedWithPart]);
assert.equal(fullDataset.Description, "");
assert.equal(Dataset.withDescription(fullDataset, undefined).Description, undefined);
assert.equal(fullDataset.Description, "");
assert.equal(dataset.Title, undefined);
assert.equal(dataset.Processes.length, 0);
assert.equal(fullDataset.Processes[0].Input, fullDataset.Descriptors[0].Describes);
assert.equal(fullDataset.Processes[0].Output, data);
assert.equal(fullDataset.Processes[0].ParameterValues[1], linkedAnnotation);
assert.equal(fullDataset.Processes[0].ExecutesRecipe.Parameters[0].DefaultValue, detailed);
assert.equal(fullDataset.Citations[0].Authors[0], fullDataset.Agents[0]);
assert.equal(fullDataset.HasParts[0], fullDataset.HasParts[1]);
assert.equal(fullDataset.HasParts[0].HasParts[0], deepest);
assert.equal(fullDataset.DataFiles[0].HasParts[0], fullDataset.DataFiles[0].HasParts[1]);
assert.equal(fullDataset.DataFiles[0].HasParts[0].Path, "measurements.csv");
assert.equal(data.HasParts.length, 0);
assert.equal(sample.Id, undefined);
assert.equal(data.Id, undefined);
assertImmutable(fullDataset.HasParts, deepest);

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

console.log("ARCBaseModel native JavaScript consumer passed");

const extension = new EntityObject("QualityAssessment");
assert.equal(extension.Id, undefined);
const extensionValues = [0, 42, 0.95, false, "", extension, new EntityCollection([]), new EntityNull(), new EntityBlob("AA==")];
const extensionKinds = ["number", "number", "number", "bool", "text", "object", "collection", "null", "blob"];
let extended = sample;
extensionValues.forEach((value, i) => {
  extended = EntityObject.withProperty(extended, `custom:${i}`, value);
  assert.ok(extended instanceof Sample);
  assert.equal(extended.EntityProperties.Get(`custom:${i}`), value);
  assert.equal(MappingProbe.ClassifyExtension(value), extensionKinds[i]);
});
assert.equal(sample.EntityProperties.Keys.length, 0);
assert.equal(extended.EntityProperties.Keys.length, extensionValues.length);
assertImmutable(extended.EntityProperties.Keys, "extra");
assert.ok(extended.EntityProperties.Contains("custom:3"));
assert.equal(extended.EntityProperties.Contains("missing"), false);
assert.throws(() => extended.EntityProperties.Get("missing"));
assert.equal(MappingProbe.IsExtensionNumber(false), false);
assert.equal(MappingProbe.IsExtensionNumber(0), true);
for (const reserved of ["type", "id", "additionalTypes", "name"]) {
  assert.throws(() => EntityObject.withProperty(sample, reserved, "override"));
}
const replaced = EntityObject.withProperty(extended, "custom:0", "now text");
assert.equal(replaced.EntityProperties.Get("custom:0"), "now text");
assert.equal(extended.EntityProperties.Get("custom:0"), 0);
const removed = EntityObject.withoutProperty(replaced, "custom:0");
assert.ok(removed instanceof Sample);
assert.equal(removed.EntityProperties.Contains("custom:0"), false);
assert.ok(replaced.EntityProperties.Contains("custom:0"));
assert.throws(() => { extended.EntityProperties.Keys = []; }, TypeError);

// Extension graphs are built without mutation, so a copy never alters the object it refers to.
const holder = EntityObject.withProperty(extension, "owner", sample);
assert.equal(holder.EntityProperties.Get("owner"), sample);
assert.equal(extension.EntityProperties.Contains("owner"), false);
const wrapped = MappingProbe.WrapExtensionNumber(extension);
assert.notEqual(wrapped, extension);
assert.equal(wrapped.EntityProperties.Get("wrapped"), 1.25);
assert.equal(typeof wrapped.EntityProperties.Get("wrapped"), "number");
assert.equal(extension.EntityProperties.Contains("wrapped"), false);
const numericCollection = new EntityCollection(generated([extension, extension]));
assert.equal(numericCollection.Count, 2);
assert.throws(() => numericCollection.Get(2));
assert.throws(() => numericCollection.Get(-1));
const longer = MappingProbe.AppendExtensionNumber(numericCollection);
assert.notEqual(longer, numericCollection);
assert.equal(longer.Count, 3);
assert.equal(longer.Get(0), extension);
assert.equal(longer.Get(2), 2.5);
assert.equal(typeof longer.Get(2), "number");
assert.equal(numericCollection.Count, 2);
for (const value of ["A", "!!!!", "AB==", "AA=A"]) assert.throws(() => new EntityBlob(value));
