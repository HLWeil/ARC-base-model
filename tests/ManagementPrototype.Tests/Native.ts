import { ARC, Dataset, Annotation } from "../../build/out/management/js/index.js";

function check(value: boolean, message: string): void { if (!value) throw new Error(message); }
const folder = `build/out/management/native-js-${Date.now()}`;
const arc = ARC.create(folder,new Dataset(["process-provenance","semantic-designation","administrative"],["native"]));
try {
  const sample=arc.Sample.create("Sample");
  const data=arc.Data.create("results.csv");
  const fragment=arc.Data.create("results.csv");
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
  const replacement=new Annotation("replaced");
  replacement.Id=value.Id;
  replacement.Value="";
  arc.Annotation.set(replacement);
  check(value.Value==="","Native empty text is present");
  check(value!==replacement,"Canonical instance retained");
  arc.save();
  arc.close();
  const resumed=ARC.openFolder(folder,"sql");
  try {
    check(resumed.Model.HasParts[0]===resumed.Model.HasParts[1],"Shared recovery");
    resumed.History.undo();
    const restored=resumed.Annotation.get(value.Id!);
    check(restored.Value===0,"Numeric before-image restored");
    check(restored.InstanceOf?.DefaultValue===restored,"Circular identity restored");
  } finally { resumed.close(); }
} finally { arc.close(); }
console.log("Native JavaScript/TypeScript management checks passed.");
