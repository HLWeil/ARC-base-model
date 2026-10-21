import * as Management from "arc-management";
import { DatasetYaml, ManagedGraph, Session } from "arc-management";
import { ARC } from "arc-management/arctrl";
import { Dataset, Annotation, Sample, Data, EntityObject } from "arc-base-model";

check(!("ARC" in Management), "Workspace ARC belongs to the arctrl entrypoint");
// Cycles cannot be assembled from immutable values, so the detached fixture is decoded from anchored YAML.
const sourceYaml = [
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
  ""
].join("\n");
const detached = DatasetYaml.decode(sourceYaml);
const zero = detached.AdditionalProperties[0];
const detachedYaml = DatasetYaml.encode(detached);
check(detached.Id === undefined && zero.Id === undefined, "Encoding preserves omitted IDs");
const decoded = DatasetYaml.decode(detachedYaml);
check(decoded instanceof Dataset && decoded.Id === undefined, "Detached decoding uses canonical domain classes");
check(decoded.HasParts[0] === decoded.HasParts[1], "Detached duplicate references preserved");
check(decoded.HasParts[0] !== decoded.HasParts[2] && decoded.HasParts[2].Id === "same", "Equal IDs do not merge detached objects");
check(decoded.HasParts[3] === decoded && decoded.EntityProperties.Get("self") === decoded, "Detached object cycles preserved");
check(decoded.HasParts[0].Title === "", "Detached empty text preserved");
check(decoded.AdditionalProperties[0] === decoded.AdditionalProperties[1] && decoded.AdditionalProperties[0].Value === 0, "Detached numeric values and references preserved");
rejects(() => DatasetYaml.decode("not: [valid YAML"));
rejects(() => DatasetYaml.decode("type: Sample\nname: invalid root\n"));

const session = Session.createInMemory();
const firstRoot = EntityObject.withId(new Dataset(["process-provenance"], ["first"]), "root");
const secondRoot = EntityObject.withId(new Dataset(["process-provenance"], ["second"]), "root");
const first = session.createArc(firstRoot);
const second = session.createArc(secondRoot);
check(first instanceof ManagedGraph, "Session returns the root entrypoint's graph class");
check(!("save" in first) && !("bindFolder" in first), "ManagedGraph has no workspace operations");
check(first.Model === firstRoot, "Supplied root adopted");
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
rejects(() => session.importYaml(detachedYaml));
const importChild = new Dataset(["administrative"], ["child"]);
const importRoot = Dataset.withHasParts(new Dataset(["process-provenance"], ["import"]), [importChild, importChild]);
const imported = session.importYaml(DatasetYaml.encode(importRoot));
check(importRoot.Id === undefined && importChild.Id === undefined, "Import leaves detached source values unchanged");
check(imported.Model.Id !== undefined && imported.Model.HasParts[0] === imported.Model.HasParts[1], "Management import assigns identity while preserving references");
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
  const sample=arc.Sample.add(new Sample("Sample"));
  const data=arc.Data.add(new Data("results.csv"));
  const fragment=arc.Data.add(new Data("results.csv"));
  arc.Data.setSelector(fragment,"row=1");
  arc.Data.setHasParts(data,[fragment,fragment]);
  const value=arc.Annotation.create("value");
  arc.Annotation.setValueNumber(value,0);
  check(value.Value===0,"Native zero is present");
  const parameter=arc.FormalParameter.create("parameter");
  arc.FormalParameter.setDefaultValue(parameter,value);
  arc.Annotation.setInstanceOf(value,parameter);
  const recipe=arc.Recipe.create("recipe");
  arc.Recipe.setParameters(recipe,[parameter,parameter]);
  arc.Recipe.setComponents(recipe,[value,value]);
  const termset=arc.DefinedTermSet.create("ontology");
  const term=arc.DefinedTerm.create("term");
  arc.DefinedTerm.setInDefinedTermSetEntity(term,termset);
  arc.Recipe.setIntendedUseTerm(recipe,term);
  const proc=arc.Process.create("process");
  arc.Process.setInputSample(proc,sample);
  arc.Process.setOutputData(proc,data);
  arc.Process.setExecutesRecipe(proc,recipe);
  const descriptor=arc.Descriptor.create(sample);
  arc.Descriptor.setAnnotations(descriptor,[value,value]);
  const organization=arc.Organization.create("organization");
  const agent=arc.Agent.create("agent");
  arc.Agent.setAffiliations(agent,[organization,organization]);
  arc.Agent.setJobTitles(agent,[term]);
  const article=arc.ScholarlyArticle.create("article");
  arc.ScholarlyArticle.setAuthors(article,[agent,agent]);
  const child=arc.Dataset.create("child");
  arc.Dataset.setHasParts(arc.Model,[child,child]);
  arc.Dataset.setProcesses(arc.Model,[proc]);
  arc.Dataset.setDataFiles(arc.Model,[data,fragment,data]);
  arc.Dataset.setDescriptors(arc.Model,[descriptor]);
  arc.Dataset.setAgents(arc.Model,[agent]);
  arc.Dataset.setCitations(arc.Model,[article]);
  check(Array.isArray(arc.Data.list()),"Native arrays");
  check(data.HasParts[0]===data.HasParts[1],"Shared duplicate target");
  const quality=arc.Entity.add(new EntityObject("QualityAssessment"));
  arc.Entity.setNumberProperty(quality,"score",0);
  arc.Entity.setBoolProperty(quality,"accepted",false);
  arc.Entity.setNullProperty(quality,"missing");
  arc.Entity.setBlobProperty(quality,"bytes","AA==");
  arc.Entity.setCollectionProperty(quality,"values",[0,false,""]);
  arc.Entity.setObjectProperty(arc.Model,"quality",quality);
  check(arc.Entity.getProperty(quality,"score")===0,"Extension zero");
  check(arc.Entity.getProperty(quality,"accepted")===false,"Extension false");
  check(arc.Entity.hasProperty(quality,"missing"),"Explicit null is present");
  const replacement=EntityObject.withId(new Annotation("replaced",""),value.Id);
  arc.Annotation.upsert(replacement);
  check(value.Value==="","Native empty text is present");
  check(value!==replacement,"Canonical instance retained");
  arc.save();
  arc.close();
  const resumed=ARC.openFolder(folder,"sql");
  try {
    check(resumed.Model.HasParts[0]===resumed.Model.HasParts[1],"Shared recovery");
    const recoveredQuality=resumed.Entity.get(quality.Id!);
    check(resumed.Entity.getProperty(resumed.Model,"quality")===recoveredQuality,"Extension identity recovered");
    check(resumed.Entity.getProperty(recoveredQuality,"score")===0,"Extension number recovered");
    resumed.History.undo();
    const restored=resumed.Annotation.get(value.Id!);
    check(restored.Value===0,"Numeric before-image restored");
    check(restored.InstanceOf?.DefaultValue===restored,"Circular identity restored");
  } finally { resumed.close(); }
} finally { arc.close(); }
console.log("Native JavaScript/TypeScript management checks passed.");
