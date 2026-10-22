import * as Management from "arc-management";
import { DatasetYaml, ManagedGraph, Session } from "arc-management";
import { ARC } from "arc-management/arctrl";
import { Dataset, Annotation, Sample, Data, EntityObject, EntityCollection, EntityNull, EntityBlob } from "arc-base-model";

check(!("ARC" in Management), "Workspace ARC belongs to the arctrl entrypoint");
// Immutable values cannot form cycles, so the test decodes the cyclic fixture from YAML.
const sourceYaml = [
  "type: Dataset",
  "id: detached-root",
  "conformsTo: [process-provenance]",
  "identifier: detached",
  "additionalProperty:",
  "  - type: Annotation",
  "    name: zero",
  "    value: 0",
  "  - type: Annotation",
  "    name: zero",
  "    value: 0",
  "hasPart:",
  "  - type: Dataset",
  "    id: same",
  "    conformsTo: [administrative]",
  "    identifier: shared",
  "    title: \"\"",
  "  - type: Dataset",
  "    id: same",
  "    conformsTo: [administrative]",
  "    identifier: shared",
  "    title: \"\"",
  "  - \"@id\": detached-root",
  "self:",
  "  \"@id\": detached-root",
  ""
].join("\n");
const detached = DatasetYaml.decode(sourceYaml);
const zero = detached.AdditionalProperties[0];
const detachedYaml = DatasetYaml.encode(detached);
check(detached.Id === "detached-root" && zero.Id === undefined, "Decoding keeps supplied IDs and leaves omitted IDs absent");
const decoded = DatasetYaml.decode(detachedYaml);
check(decoded instanceof Dataset && decoded.Id === "detached-root", "Detached decoding uses canonical domain classes");
check(decoded.HasParts[0] === decoded.HasParts[1] && decoded.HasParts[0].Id === "same", "Equal IDs denote one entity");
check(decoded.HasParts[2] === decoded && decoded.EntityProperties.Get("self") === decoded, "Cycles through @id preserved");
check(decoded.HasParts[0].Title === "", "Detached empty text preserved");
check(decoded.AdditionalProperties[0] !== decoded.AdditionalProperties[1] && decoded.AdditionalProperties[0].Value === 0, "Entities without IDs decode as distinct objects with numeric values");
const indexed = DatasetYaml.decode(DatasetYaml.encodeIndexed(detached));
check(indexed.HasParts[2] === indexed && DatasetYaml.encode(indexed) === detachedYaml, "Indexed output describes the same graph");
rejects(() => DatasetYaml.decode("not: [valid YAML"));
rejects(() => DatasetYaml.decode("type: Sample\nname: invalid root\n"));
const conflictingYaml = "type: Dataset\nidentifier: x\nhasPart:\n  - {type: Dataset, id: same, identifier: a}\n  - {type: Dataset, id: same, identifier: b}\n";
rejects(() => DatasetYaml.decode(conflictingYaml));

const session = Session.createInMemory();
const firstRoot = EntityObject.withId(new Dataset(["process-provenance"], ["first"]), "root");
const secondRoot = EntityObject.withId(new Dataset(["process-provenance"], ["second"]), "root");
const first = session.createArc(firstRoot);
const second = session.createArc(secondRoot);
check(first instanceof ManagedGraph, "Session returns the root entrypoint's graph class");
check(!("save" in first) && !("bindFolder" in first), "ManagedGraph has no workspace operations");
check(first.Model.Id === "root" && first.Model !== firstRoot, "Supplied root adopted as a stored copy");
check(first.ArcId !== second.ArcId, "Generated ARC identity");
check(session.openArc(first.ArcId) === first, "Active facade cached");
check(session.listArcs()[0].RootEntityId === "root", "Lightweight metadata");
check(session.listArcs()[0].Folder === undefined, "Unbound metadata");
const workspace = ARC.bind(first, `build/out/arc-management/native-binding-${Date.now()}`);
check(workspace.ManagedGraph === first && workspace.Dataset === first.Dataset, "Workspace delegates to the same graph and operation objects");
check(ARC.fromGraph(first).ManagedGraph === first, "Bound graphs can be wrapped without hydration");
workspace.close();
rejects(() => first.Sample.create("closed"));
second.Sample.create("still open");
rejects(() => session.importYaml(conflictingYaml));
const importChild = EntityObject.withId(new Dataset(["administrative"], ["child"]), "child-id");
const importRoot = Dataset.withHasParts(new Dataset(["process-provenance"], ["import"]), [importChild, importChild]);
const imported = session.importYaml(DatasetYaml.encode(importRoot));
check(importRoot.Id === undefined, "Import leaves detached source values unchanged");
check(imported.Model.Id !== undefined && imported.Model.HasParts[0].Id === "child-id" && imported.Model.HasParts[0] === imported.Model.HasParts[1], "Management import assigns missing IDs while preserving references");
const exported = DatasetYaml.decode(session.openArc(imported.ArcId).toYaml());
check(exported.HasParts[0] === exported.HasParts[1], "Generic graph exports root YAML without folder IO");
imported.close();
session.close();

// @ts-expect-error importing a folder requires a Session
if (false) ARC.importFolder({}, "folder");
// @ts-expect-error sessions are created by factories
if (false) new Session();
// @ts-expect-error generic graphs have no folder policy
if (false) first.bindFolder("folder");
// @ts-expect-error ARC is not exported by the generic entrypoint
if (false) Management.ARC;

function check(value: boolean, message: string): void { if (!value) throw new Error(message); }
function rejects(action: () => unknown): void {
  try { action(); } catch { return; }
  throw new Error("Expected rejection");
}
const folder = `build/out/arc-management/native-ts-${Date.now()}`;
const arc = ARC.create(folder,new Dataset(["process-provenance","semantic-designation","administrative"],["native"]));
try {
  const sample = arc.Sample.add(new Sample("Sample"));
  const data0 = arc.Data.add(new Data("results.csv"));
  const fragment0 = arc.Data.add(new Data("results.csv"));
  const fragment = arc.Data.setSelector(fragment0,"row=1");
  check(fragment0.Selector === undefined && fragment.Selector === "row=1","Setters return updated values and keep held ones");
  const data = arc.Data.setHasParts(data0,[fragment,fragment]);
  let value = arc.Annotation.create("value");
  value = arc.Annotation.setValue(value,0);
  check(value.Value === 0,"Native zero is present");
  let parameter = arc.FormalParameter.create("parameter");
  parameter = arc.FormalParameter.setDefaultValue(parameter,value);
  value = arc.Annotation.setInstanceOf(value,parameter);
  const recipe0 = arc.Recipe.create("recipe");
  arc.Recipe.setParameters(recipe0,[parameter,parameter]);
  arc.Recipe.setComponents(recipe0,[value,value]);
  const termset = arc.DefinedTermSet.create("ontology");
  const term0 = arc.DefinedTerm.create("term");
  const term = arc.DefinedTerm.setInDefinedTermSet(term0,termset);
  const recipe = arc.Recipe.setIntendedUse(recipe0,term);
  const proc0 = arc.Process.create("process");
  arc.Process.setInput(proc0,sample);
  arc.Process.setOutput(proc0,data);
  const proc = arc.Process.setExecutesRecipe(proc0,recipe);
  check(proc.Input?.Id === sample.Id && proc0.Input === undefined,"Endpoint alternatives are native values");
  const descriptor0 = arc.Descriptor.create(sample);
  const descriptor = arc.Descriptor.setAnnotations(descriptor0,[value,value]);
  const organization = arc.Organization.create("organization");
  const agent0 = arc.Agent.create("agent");
  arc.Agent.setAffiliations(agent0,[organization,organization]);
  const agent = arc.Agent.setJobTitles(agent0,[term]);
  const article0 = arc.ScholarlyArticle.create("article");
  const article = arc.ScholarlyArticle.setAuthors(article0,[agent,agent]);
  const child = arc.Dataset.create("child");
  arc.Dataset.setHasParts(arc.Model,[child,child]);
  arc.Dataset.setProcesses(arc.Model,[proc]);
  arc.Dataset.setDataFiles(arc.Model,[data,fragment,data]);
  arc.Dataset.setDescriptors(arc.Model,[descriptor]);
  arc.Dataset.setAgents(arc.Model,[agent]);
  const model = arc.Dataset.setCitations(arc.Model,[article]);
  check(Array.isArray(arc.Data.list()) && Object.isFrozen(arc.Data.list()),"Native frozen arrays");
  check(model.HasParts[0] === model.HasParts[1],"Shared duplicate target");
  check(model.DataFiles[0].HasParts[0] === model.DataFiles[0].HasParts[1],"Shared nested target");
  let quality = arc.Entity.add(new EntityObject("QualityAssessment"));
  quality = arc.Entity.setProperty(quality,"score",0);
  quality = arc.Entity.setProperty(quality,"accepted",false);
  quality = arc.Entity.setProperty(quality,"missing",new EntityNull());
  quality = arc.Entity.setProperty(quality,"bytes",new EntityBlob("AA=="));
  quality = arc.Entity.setProperty(quality,"values",new EntityCollection([0,false,""]));
  arc.Entity.setProperty(arc.Model,"quality",quality);
  check(quality.EntityProperties.Get("score") === 0,"Extension zero");
  check(quality.EntityProperties.Get("accepted") === false,"Extension false");
  check(quality.EntityProperties.Contains("missing"),"Explicit null is present");
  const replacement = EntityObject.withId(new Annotation("replaced",""),value.Id);
  const replaced = arc.Annotation.upsert(replacement);
  check(replaced.Value === "","Native empty text is present");
  check(value.Value === 0 && value !== replaced,"The held value is unchanged");
  check(arc.Annotation.upsert(replacement,true).Value === "","Recursive upsert accepts a positional flag");
  arc.save();
  arc.close();
  const resumed = ARC.openFolder(folder,"sql");
  try {
    check(resumed.Model.HasParts[0] === resumed.Model.HasParts[1],"Shared recovery");
    const recoveredQuality = resumed.Entity.get(quality.Id!);
    check(resumed.Model.EntityProperties.Get("quality") === recoveredQuality,"Extension identity recovered");
    check(recoveredQuality.EntityProperties.Get("score") === 0,"Extension number recovered");
    resumed.History.undo();
    const restored = resumed.Annotation.get(value.Id!);
    check(restored.Value === 0,"Numeric before-image restored");
    check(restored.InstanceOf?.DefaultValue === restored,"Circular identity restored");
  } finally { resumed.close(); }
} finally { arc.close(); }
console.log("Native JavaScript/TypeScript management checks passed.");
