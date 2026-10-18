from arc_base_model import Agent,Annotation,Data,Dataset,DefinedTerm,DefinedTermSet,Descriptor,EntityObject,EntityCollection,EntityNull,EntityBlob,FormalParameter,Organization,Process,Recipe,Sample,ScholarlyArticle,ModelBuilder,ModelValue
from arc_base_model_probe import MappingProbe

def rejects(action):
    try: action()
    except Exception: return
    raise AssertionError('Expected rejection')

sample=Sample('A')
kinds=[Annotation('a'),DefinedTerm('t'),DefinedTermSet('set'),FormalParameter(),sample,Data('file'),Recipe(),Process('p'),Descriptor(sample),Organization('lab'),Agent('Ada'),ScholarlyArticle('paper'),Dataset(['administrative'],['id'])]
for value in kinds:
    assert value.Id is None
    assert isinstance(value.AdditionalTypes,tuple)
    rejects(lambda:setattr(value,'Id','assigned'))
    rejects(lambda:setattr(value,'_sealed',False))
    rejects(lambda:delattr(value,'_sealed'))
    for key in vars(value): rejects(lambda:setattr(value,key,'changed'))
    rejects(lambda:vars(value).__setitem__('_sealed',False))
    rejects(lambda:value.AdditionalTypes.append('new'))
owned=[Annotation('x')]
original=Sample('old',owned);owned.clear();assert len(original.AdditionalProperties)==1
edited=Sample.with_name(original,'new');assert original.Name=='old' and edited.Name=='new';assert edited.AdditionalProperties is original.AdditionalProperties
assert Sample.with_id(sample,'s1').Id=='s1' and sample.Id is None
assert Sample.clear_id(Sample.with_id(sample,'s1')).Id is None
for number in [0,1,1.25]:
    a=Annotation.with_value(Annotation('n'),number)
    assert a.Value==number and type(a.Value) in (int,float)
    assert MappingProbe.IsNumber(number) and not MappingProbe.IsText(number)
    assert MappingProbe.ClassifyNumberFirst(number)=='number'
assert Annotation.with_value(Annotation('n'),'').Value==''
assert Annotation.clear_value(Annotation.with_value(Annotation('n'),0)).Value is None
rejects(lambda:Annotation.with_value(Annotation('n'),False))
rejects(lambda:Annotation('n',False))
assert not MappingProbe.IsNumber(False)
assert MappingProbe.ReadAnnotation(MappingProbe.WriteAnnotation(0))==0
assert MappingProbe.ReadEntity(MappingProbe.WriteEntity(sample)) is sample
file=Data('file');assert MappingProbe.ReadEntity(MappingProbe.WriteEntity(file)) is file
term=DefinedTerm('t');assert MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse(term)) is term
assert MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse(''))==''
termset=DefinedTermSet('set');assert MappingProbe.ReadTermSet(MappingProbe.WriteTermSet(termset)) is termset
assert MappingProbe.ReadTermSet(MappingProbe.WriteTermSet('url'))=='url'
for call in [lambda:Sample(),lambda:Dataset([],['id']),lambda:Dataset.with_identifiers(Dataset(['administrative'],['id']),[]),lambda:Descriptor(term),lambda:Sample('x',[term])]:rejects(call)
collection=EntityCollection([0,False,'',sample,EntityNull(),EntityBlob('AA==')]);assert collection.Count==6 and collection.Get(3) is sample
rejects(lambda: collection.Get(0.5))
rejects(lambda: collection.Get(False))
rejects(lambda:collection._values.append(1))
custom=EntityObject.with_entity_property(EntityObject('Quality'),'values',collection)
assert custom.EntityProperties.Get('values') is collection
rejects(lambda:custom.EntityProperties._values.__setitem__('values','bad'))
rejects(lambda:EntityObject.with_entity_property(custom,'id','bad'))
assert not EntityObject.without_entity_property(custom,'values').EntityProperties.Contains('values')
for text in ['','AA==','AQI=','AQID']:assert EntityBlob(text).Base64==text
for text in ['A','A===','AB==','AAB=','====']:rejects(lambda:EntityBlob(text))
builder=ModelBuilder();root=builder.draft(Dataset(['process-provenance'],['root']));child=builder.draft(Dataset(['administrative'],['child']))
rejects(lambda: ModelValue.number(False))
rejects(lambda: ModelValue.boolean(1))
builder.Dataset.set_has_parts(root,[child,child]);builder.Dataset.set_has_parts(child,[root]);builder.Dataset.set_entity_property(root,'self',ModelValue.object_value(root));built=builder.finish();dataset=built.get(root)
assert dataset.HasParts[0] is dataset.HasParts[1] and dataset.HasParts[0].HasParts[0] is dataset
assert dataset.EntityProperties.Get('self') is dataset
rejects(lambda: built._values.__setitem__('0', sample))
assert MappingProbe.DatasetCount(dataset)==2 and MappingProbe.DatasetFirst(dataset) is dataset.HasParts[0]
rejects(lambda:builder.finish());rejects(lambda:setattr(root,'state',None))
rejects(lambda:setattr(root.state,'writable',False))
rejects(lambda:vars(root.state).__setitem__('writable',False))
cloner=ModelBuilder();cloned=cloner.clone_graph(dataset);cloner.Dataset.set_title(cloner.draft_for(dataset),'changed');changed=cloner.finish().get(cloned)
assert changed.HasParts[0].HasParts[0] is changed and changed.EntityProperties.Get('self') is changed and dataset.Title is None
fix=ModelBuilder();token=fix.draft(Dataset(['administrative'],['x']));fix.Dataset.set_identifiers(token,[]);rejects(lambda:fix.finish());fix.Dataset.set_identifiers(token,['fixed']);assert fix.finish().get(token).Identifiers[0]=='fixed'
owner=ModelBuilder();reference=owner.reference(sample);rejects(lambda:owner.Sample.set_name(reference,'bad'));rejects(lambda:ModelBuilder().Sample.set_name(reference,'bad'));rejects(lambda:owner.Data.set_path(reference,'bad'))
step_token=owner.draft(Process('step'));data_token=owner.reference(file);descriptor_token=owner.draft(Descriptor(sample))
for method in ['set_input_sample','set_output_sample']:rejects(lambda:getattr(owner.Process,method)(step_token,data_token))
for method in ['set_input_data','set_output_data']:rejects(lambda:getattr(owner.Process,method)(step_token,reference))
rejects(lambda:owner.Descriptor.set_describes_sample(descriptor_token,data_token));rejects(lambda:owner.Descriptor.set_describes_data(descriptor_token,reference))
assert MappingProbe.CreateNumericExtension().EntityProperties.Get('wrapped')==1.25
assert type(MappingProbe.CreateNumericExtension().EntityProperties.Get('wrapped')) is float
assert MappingProbe.CreateNumericCollection().Get(0)==2.5
print('ARCBaseModel native Python immutable contract passed.')
