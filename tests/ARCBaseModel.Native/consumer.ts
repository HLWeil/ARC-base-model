// Type-check through TestBaseModelNative. This file is deliberately not executed.
import {
  Agent, Annotation, Data, Dataset, DefinedTerm, DefinedTermSet, Descriptor,
  FormalParameter, Organization, Process, Recipe, Sample, ScholarlyArticle,
} from "arc-base-model";
import type {
  AnnotationValue, EntityReference, RecipeIntendedUse, DefinedTermSetReference,
} from "arc-base-model";
import { MappingProbe } from "arc-base-model-probe";

const annotation = new Annotation("temperature");
const parameter = new FormalParameter();
const termSet = new DefinedTermSet("OBI");
const term = new DefinedTerm("measurement");
const sample = new Sample("leaf");
const data = new Data("measurements.csv");
const recipe = new Recipe();
const process = new Process("measure leaf");
const descriptor = new Descriptor(sample);
const organization = new Organization("Plant laboratory");
const agent = new Agent("Researcher");
const article = new ScholarlyArticle("Leaf measurements");
const dataset = new Dataset(["administrative", "process-provenance"], ["study"]);

const numberValue: AnnotationValue = 0;
const textValue: AnnotationValue = "";
const sampleReference: EntityReference = sample;
const dataReference: EntityReference = data;
const textUse: RecipeIntendedUse = "measurement";
const termUse: RecipeIntendedUse = term;
const urlReference: DefinedTermSetReference = "https://example.org/ontology";
const objectReference: DefinedTermSetReference = termSet;
annotation.Value = numberValue;
annotation.Value = textValue;
annotation.Value = undefined;
annotation.Id = "annotation-1";
annotation.Id = undefined;
annotation.NameTAN = "https://example.org/temperature";
annotation.ValueTAN = "";
annotation.UnitTAN = undefined;
parameter.DefaultValue = annotation;
annotation.InstanceOf = parameter;
process.Input = sampleReference;
process.Output = dataReference;
process.Input = undefined;
process.Output = undefined;
descriptor.Describes = dataReference;
recipe.IntendedUse = textUse;
recipe.IntendedUse = termUse;
recipe.IntendedUse = undefined;
term.InDefinedTermSet = urlReference;
term.InDefinedTermSet = objectReference;
term.InDefinedTermSet = undefined;
recipe.Parameters = [parameter];
recipe.Components.push(annotation);
sample.AdditionalProperties = [annotation, annotation];
data.HasParts = [new Data("measurements.csv")];
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
dataset.HasParts.push(new Dataset(["semantic-designation"], ["nested"]));
dataset.AdditionalTypes = ["custom", "custom"];
dataset.Identifiers.push("another-identifier");

const valueRoundtrip: AnnotationValue | undefined =
  MappingProbe.ReadAnnotation(MappingProbe.WriteAnnotation(42));
const entityRoundtrip: EntityReference | undefined =
  MappingProbe.ReadEntity(MappingProbe.WriteEntity(sample));
const intendedUseRoundtrip: RecipeIntendedUse | undefined =
  MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse(term));
const termSetRoundtrip: DefinedTermSetReference | undefined =
  MappingProbe.ReadTermSet(MappingProbe.WriteTermSet(termSet));
const sameSample: Sample = MappingProbe.AssignSampleId(sample, "sample-1");
const sampleId: string | undefined = MappingProbe.ReadSampleId(sameSample);
const numericCase: boolean = MappingProbe.IsNumber(numberValue);
const textualCase: boolean = MappingProbe.IsText(textValue);
const classifiedCase: string = MappingProbe.ClassifyNumberFirst(numberValue);
const generatedAnnotation: Annotation = MappingProbe.CreateNumericAnnotation();

// Invalid calls must fail in TypeScript without knowledge of Fable internals.
// @ts-expect-error Annotation values do not include booleans.
annotation.Value = false;
// @ts-expect-error Text is not an entity reference.
process.Input = "sample-1";
// @ts-expect-error Entity references are singular, not arrays.
process.Output = [data];
// @ts-expect-error An entity reference must be a Sample or Data.
descriptor.Describes = term;
// @ts-expect-error The descriptor target is required.
descriptor.Describes = undefined;
// @ts-expect-error A recipe's intended use cannot be numeric.
recipe.IntendedUse = 42;
// @ts-expect-error A term's term set cannot be a DefinedTerm.
term.InDefinedTermSet = term;
// @ts-expect-error Fixed entity types cannot be assigned.
sample.Type = "Data";
// @ts-expect-error Domain IDs are strings.
sample.Id = 42;
// @ts-expect-error Annotation names are required.
new Annotation();
// @ts-expect-error Data paths are required.
new Data();
// @ts-expect-error Dataset identifiers are required independently of optional Id.
new Dataset(["administrative"]);
// @ts-expect-error Dataset constructors require collections of text.
new Dataset([42], ["study"]);
// @ts-expect-error Process collections contain Process instances.
dataset.Processes = [sample];
// @ts-expect-error The mapper must expose the same typed direct-value API.
MappingProbe.WriteAnnotation(false);
// @ts-expect-error Numeric-case detection accepts only the annotation alternatives.
MappingProbe.IsNumber(false);
// @ts-expect-error Required alternative inputs cannot be absent.
MappingProbe.IsText(undefined);

void [valueRoundtrip, entityRoundtrip, intendedUseRoundtrip, termSetRoundtrip, sampleId,
  numericCase, textualCase, classifiedCase, generatedAnnotation];
