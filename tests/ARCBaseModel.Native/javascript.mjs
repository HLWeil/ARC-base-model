import assert from 'node:assert/strict';
import {Agent,Annotation,Data,Dataset,DefinedTerm,DefinedTermSet,Descriptor,EntityObject,EntityCollection,EntityNull,EntityBlob,FormalParameter,Organization,Process,Recipe,Sample,ScholarlyArticle,ModelBuilder,ModelValue} from 'arc-base-model';
import {MappingProbe} from 'arc-base-model-probe';

const sample=new Sample('A');
const kinds=[new Annotation('a'),new DefinedTerm('t'),new DefinedTermSet('set'),new FormalParameter(),sample,new Data('file'),new Recipe(),new Process('p'),new Descriptor(sample),new Organization('lab'),new Agent('Ada'),new ScholarlyArticle('paper'),new Dataset(['administrative'],['id'])];
for(const value of kinds){
 assert.equal(value.Id,undefined);assert.ok(Object.isFrozen(value));assert.ok(Object.isFrozen(value.AdditionalTypes));
 for(const key of [Object.keys(value).find(k=>k.startsWith('_')&&k!=='_sealed'),'_sealed']){
  assert.throws(()=>{value[key]='changed';});assert.throws(()=>{delete value[key];});
 }
 assert.throws(()=>{value.Id='assigned';});assert.throws(()=>value.AdditionalTypes.push('new'));
}
const owned=[new Annotation('x')];const original=new Sample('old',owned);owned.length=0;
assert.equal(original.AdditionalProperties.length,1);
const edited=Sample.withName(original,'new');assert.equal(original.Name,'old');assert.equal(edited.Name,'new');assert.equal(edited.AdditionalProperties,original.AdditionalProperties);
assert.equal(Sample.withId(sample,'s1').Id,'s1');assert.equal(sample.Id,undefined);assert.equal(Sample.clearId(Sample.withId(sample,'s1')).Id,undefined);
for(const number of [0,1,1.25]){const a=Annotation.withValue(new Annotation('n'),number);assert.equal(a.Value,number);assert.equal(MappingProbe.IsNumber(number),true);assert.equal(MappingProbe.IsText(number),false);assert.equal(MappingProbe.ClassifyNumberFirst(number),'number');}
assert.equal(Annotation.withValue(new Annotation('n'),'').Value,'');assert.equal(Annotation.clearValue(Annotation.withValue(new Annotation('n'),0)).Value,undefined);
assert.throws(()=>Annotation.withValue(new Annotation('n'),false));assert.throws(()=>new Annotation('n',false));
assert.equal(MappingProbe.ReadAnnotation(MappingProbe.WriteAnnotation(0)),0);
assert.equal(MappingProbe.ReadEntity(MappingProbe.WriteEntity(sample)),sample);
const file=new Data('file');assert.equal(MappingProbe.ReadEntity(MappingProbe.WriteEntity(file)),file);
const term=new DefinedTerm('t');assert.equal(MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse(term)),term);
assert.equal(MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse('')),'');
const termset=new DefinedTermSet('set');assert.equal(MappingProbe.ReadTermSet(MappingProbe.WriteTermSet(termset)),termset);
assert.equal(MappingProbe.ReadTermSet(MappingProbe.WriteTermSet('url')),'url');
for(const call of [()=>new Sample(),()=>new Dataset([],['id']),()=>Dataset.withIdentifiers(new Dataset(['administrative'],['id']),[]),()=>new Descriptor(term),()=>new Sample('x',[term])])assert.throws(call);
const collection=new EntityCollection([0,false,'',sample,new EntityNull(),new EntityBlob('AA==')]);assert.equal(collection.Count,6);assert.equal(collection.Get(3),sample);assert.ok(Object.isFrozen(collection));assert.throws(()=>collection._values.push(1));
assert.throws(()=>collection.Get(0.5));assert.throws(()=>collection.Get(false));
const custom=EntityObject.withEntityProperty(new EntityObject('Quality'),'values',collection);assert.equal(custom.EntityProperties.Get('values'),collection);assert.ok(Object.isFrozen(custom.EntityProperties));assert.throws(()=>{custom.EntityProperties._values.values='bad';});assert.equal(custom.EntityProperties.Contains('id'),false);assert.throws(()=>EntityObject.withEntityProperty(custom,'id','bad'));
assert.equal(EntityObject.withoutEntityProperty(custom,'values').EntityProperties.Contains('values'),false);
for(const text of ['','AA==','AQI=','AQID'])assert.equal(new EntityBlob(text).Base64,text);
for(const text of ['A','A===','AB==','AAB=','===='])assert.throws(()=>new EntityBlob(text));
assert.throws(()=>ModelValue.number(false));assert.throws(()=>ModelValue.boolean(1));
const builder=new ModelBuilder();const root=builder.draft(new Dataset(['process-provenance'],['root']));const child=builder.draft(new Dataset(['administrative'],['child']));builder.Dataset.setHasParts(root,[child,child]);builder.Dataset.setHasParts(child,[root]);builder.Dataset.setEntityProperty(root,'self',ModelValue.objectValue(root));const built=builder.finish();const dataset=built.get(root);
assert.throws(()=>{root.state.writable=false;});
assert.throws(()=>{built._values['0']=sample;});
assert.equal(dataset.HasParts[0],dataset.HasParts[1]);assert.equal(dataset.HasParts[0].HasParts[0],dataset);assert.equal(dataset.EntityProperties.Get('self'),dataset);assert.ok(Object.isFrozen(root));assert.equal(MappingProbe.DatasetCount(dataset),2);assert.equal(MappingProbe.DatasetFirst(dataset),dataset.HasParts[0]);assert.throws(()=>builder.finish());
const cloner=new ModelBuilder();const cloned=cloner.cloneGraph(dataset);cloner.Dataset.setTitle(cloner.draftFor(dataset),'changed');const changed=cloner.finish().get(cloned);assert.equal(changed.HasParts[0].HasParts[0],changed);assert.equal(changed.EntityProperties.Get('self'),changed);assert.equal(dataset.Title,undefined);
const fix=new ModelBuilder();const token=fix.draft(new Dataset(['administrative'],['x']));fix.Dataset.setIdentifiers(token,[]);assert.throws(()=>fix.finish());fix.Dataset.setIdentifiers(token,['fixed']);assert.equal(fix.finish().get(token).Identifiers[0],'fixed');
const owner=new ModelBuilder();const reference=owner.reference(sample);assert.throws(()=>owner.Sample.setName(reference,'bad'));assert.throws(()=>new ModelBuilder().Sample.setName(reference,'bad'));assert.throws(()=>owner.Data.setPath(reference,'bad'));
const stepToken=owner.draft(new Process('step'));const dataToken=owner.reference(file);const descriptorToken=owner.draft(new Descriptor(sample));
for(const method of ['setInputSample','setOutputSample'])assert.throws(()=>owner.Process[method](stepToken,dataToken));
for(const method of ['setInputData','setOutputData'])assert.throws(()=>owner.Process[method](stepToken,reference));
assert.throws(()=>owner.Descriptor.setDescribesSample(descriptorToken,dataToken));assert.throws(()=>owner.Descriptor.setDescribesData(descriptorToken,reference));
assert.equal(MappingProbe.CreateNumericExtension().EntityProperties.Get('wrapped'),1.25);assert.equal(MappingProbe.CreateNumericCollection().Get(0),2.5);
console.log('ARCBaseModel native JavaScript immutable contract passed.');
