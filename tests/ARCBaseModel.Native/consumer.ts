// Type-check through TestBaseModelNative. This file is deliberately not executed.
import {
  EntityObject, EntityPropertyBag, EntityCollection, EntityNull, EntityBlob, Agent, Annotation, Data, Dataset, DefinedTerm, DefinedTermSet, Descriptor,
  FormalParameter, Organization, Process, Recipe, Sample, ScholarlyArticle,
} from "arc-base-model";
import type {
  Entity, AnnotationValue, EntityReference, RecipeIntendedUse, DefinedTermSetReference,
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

// Copies return the concrete class and take direct alternative values.
const numbered: Annotation = Annotation.withValue(annotation, numberValue);
const texted: Annotation = Annotation.withValue(numbered, textValue);
const cleared: Annotation = Annotation.withValue(texted, undefined);
const identified: Annotation = EntityObject.withId(annotation, "annotation-1");
const unidentified: Annotation = EntityObject.withId(identified, undefined);
const retyped: Sample = EntityObject.withAdditionalTypes(sample, ["custom", "custom"]);
const extended: Sample = EntityObject.withProperty(sample, "score", 0.5);
const reduced: Sample = EntityObject.withoutProperty(extended, "score");
const tanned: Annotation = Annotation.withValueTAN(Annotation.withNameTAN(annotation, "https://example.org/temperature"), "");
const unTanned: Annotation = Annotation.withUnitTAN(tanned, undefined);
const linkedParameter: FormalParameter = FormalParameter.withDefaultValue(parameter, annotation);
const linkedAnnotation: Annotation = Annotation.withInstanceOf(annotation, linkedParameter);
const forward: Process = Process.withOutput(Process.withInput(process, sampleReference), dataReference);
const emptied: Process = Process.withOutput(Process.withInput(forward, undefined), undefined);
const described: Descriptor = Descriptor.withDescribes(descriptor, dataReference);
const withTextUse: Recipe = Recipe.withIntendedUse(recipe, textUse);
const withTermUse: Recipe = Recipe.withIntendedUse(withTextUse, termUse);
const withoutUse: Recipe = Recipe.withIntendedUse(withTermUse, undefined);
const withUrl: DefinedTerm = DefinedTerm.withInDefinedTermSet(term, urlReference);
const withObject: DefinedTerm = DefinedTerm.withInDefinedTermSet(withUrl, objectReference);
const withoutSet: DefinedTerm = DefinedTerm.withInDefinedTermSet(withObject, undefined);
const withParameters: Recipe = Recipe.withParameters(recipe, [parameter]);
const withComponents: Recipe = Recipe.withComponents(withParameters, new Set([annotation]));
const withAnnotations: Sample = Sample.withAdditionalProperties(sample, [annotation, annotation]);
const withParts: Data = Data.withHasParts(data, [new Data("measurements.csv")]);
const withUrlOrg: Organization = Organization.withUrl(organization, "https://example.org/lab");
const withAffiliation: Agent = Agent.withJobTitles(Agent.withAffiliations(agent, [organization]), [term]);
const withAuthors: ScholarlyArticle = ScholarlyArticle.withCreativeWorkStatus(ScholarlyArticle.withAuthors(article, [agent]), term);
const full: Dataset = Dataset.withHasParts(
  Dataset.withCitations(
    Dataset.withAgents(
      Dataset.withDataFiles(
        Dataset.withDescriptors(Dataset.withProcesses(dataset, [process]), [descriptor]),
        [data]),
      [agent]),
    [article]),
  [new Dataset(["semantic-designation"], ["nested"])]);
const retitled: Dataset = Dataset.withIdentifiers(Dataset.withTitle(full, "Study"), ["another-identifier"]);

// Getters are readonly values and collections are readonly arrays.
const name: string = sample.Name;
const id: string | undefined = sample.Id;
const fixedType: "Sample" = sample.Type;
const members: readonly Annotation[] = withAnnotations.AdditionalProperties;
const profiles: readonly string[] = dataset.ConformsTo;
const parts: readonly Dataset[] = full.HasParts;
const types: readonly string[] = retyped.AdditionalTypes;
const firstPart: Dataset | undefined = parts[0];
const keys: readonly string[] = extended.EntityProperties.Keys;

const valueRoundtrip: AnnotationValue | undefined =
  MappingProbe.ReadAnnotation(MappingProbe.WriteAnnotation(42));
const entityRoundtrip: EntityReference | undefined =
  MappingProbe.ReadEntity(MappingProbe.WriteEntity(sample));
const intendedUseRoundtrip: RecipeIntendedUse | undefined =
  MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse(term));
const termSetRoundtrip: DefinedTermSetReference | undefined =
  MappingProbe.ReadTermSet(MappingProbe.WriteTermSet(termSet));
const assigned: Sample = MappingProbe.AssignSampleId(sample, "sample-1");
const sampleId: string | undefined = MappingProbe.ReadSampleId(assigned);
const numericCase: boolean = MappingProbe.IsNumber(numberValue);
const textualCase: boolean = MappingProbe.IsText(textValue);
const classifiedCase: string = MappingProbe.ClassifyNumberFirst(numberValue);
const generatedAnnotation: Annotation = MappingProbe.CreateNumericAnnotation();

// Invalid calls must fail in TypeScript without knowledge of Fable internals.
// @ts-expect-error Getter-only properties are readonly.
sample.Name = "other";
// @ts-expect-error Fixed entity types cannot be assigned.
sample.Type = "Data";
// @ts-expect-error Readonly collections have no push.
dataset.HasParts.push(dataset);
// @ts-expect-error Readonly collections reject index assignment.
dataset.ConformsTo[0] = "administrative";
// @ts-expect-error The copy takes the property type, not a number.
Sample.withName(sample, 42);
// @ts-expect-error The property bag has no public constructor.
new EntityPropertyBag();
// @ts-expect-error Annotation values do not include booleans.
Annotation.withValue(annotation, false);
// @ts-expect-error Text is not an entity reference.
Process.withInput(process, "sample-1");
// @ts-expect-error Entity references are singular, not arrays.
Process.withOutput(process, [data]);
// @ts-expect-error An entity reference must be a Sample or Data.
Descriptor.withDescribes(descriptor, term);
// @ts-expect-error The descriptor target is required.
Descriptor.withDescribes(descriptor, undefined);
// @ts-expect-error A recipe's intended use cannot be numeric.
Recipe.withIntendedUse(recipe, 42);
// @ts-expect-error A term's term set cannot be a DefinedTerm.
DefinedTerm.withInDefinedTermSet(term, term);
// @ts-expect-error Domain IDs are strings.
EntityObject.withId(sample, 42);
// @ts-expect-error Annotation names are required.
new Annotation();
// @ts-expect-error Data paths are required.
new Data();
// @ts-expect-error Dataset identifiers are required independently of optional Id.
new Dataset(["administrative"]);
// @ts-expect-error Dataset constructors require collections of text.
new Dataset([42], ["study"]);
// @ts-expect-error Process collections contain Process instances.
Dataset.withProcesses(dataset, [sample]);
// @ts-expect-error The mapper must expose the same typed direct-value API.
MappingProbe.WriteAnnotation(false);
// @ts-expect-error Numeric-case detection accepts only the annotation alternatives.
MappingProbe.IsNumber(false);
// @ts-expect-error Required alternative inputs cannot be absent.
MappingProbe.IsText(undefined);

void [numbered, texted, cleared, unidentified, reduced, unTanned, linkedAnnotation, emptied, described,
  withoutUse, withoutSet, withComponents, withParts, withUrlOrg, withAffiliation, withAuthors, retitled,
  name, id, fixedType, members, profiles, parts, types, firstPart, keys,
  valueRoundtrip, entityRoundtrip, intendedUseRoundtrip, termSetRoundtrip, sampleId,
  numericCase, textualCase, classifiedCase, generatedAnnotation];

const customObject = new EntityObject("QualityAssessment");
const customValues: Entity[] = [customObject, 42, false, "", new EntityNull(), new EntityBlob("AA==")];
const customCollection = new EntityCollection(customValues);
const customHolder: EntityObject = EntityObject.withProperty(customObject, "values", customCollection);
const customValue: Entity = customHolder.EntityProperties.Get("values");
const wrapped: EntityObject = MappingProbe.WrapExtensionNumber(customObject);
const longer: EntityCollection = MappingProbe.AppendExtensionNumber(customCollection);
void [customValue, wrapped, longer];
// @ts-expect-error Arbitrary runtime objects are not extension values.
EntityObject.withProperty(customObject, "invalid", { score: 1 });
