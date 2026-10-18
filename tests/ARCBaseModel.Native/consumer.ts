import {Annotation,Dataset,Sample,Data,ModelBuilder,ModelValue,EntityObject,EntityCollection,ModelToken} from 'arc-base-model';
import type {AnnotationValue,EntityReference} from 'arc-base-model';
const sample=new Sample('A');const edited=Sample.withName(sample,'B');
const value:AnnotationValue=0;const ref:EntityReference=sample;
const annotation=Annotation.withValue(new Annotation('value'),value);
const b=new ModelBuilder();const root=b.draft(new Dataset(['process-provenance'],['root']));
b.Dataset.setHasParts(root,[root]);b.Dataset.setEntityProperty(root,'self',ModelValue.objectValue(root));
const result:Dataset=b.finish().get(root);const children:readonly Dataset[]=result.HasParts;
const token:ModelToken<Sample>=new ModelBuilder().draft(sample);
const custom=EntityObject.withEntityProperty(new EntityObject('Custom'),'values',new EntityCollection([0,false,'',sample]));
// @ts-expect-error Domain properties are read-only.
sample.Name='bad';
// @ts-expect-error Backing fields are hidden.
sample._name='bad';
// @ts-expect-error Owned collections are read-only.
children.push(result);
// @ts-expect-error Typed tokens prevent wrong entity group edits.
b.Data.setPath(token,'bad');
// @ts-expect-error Numeric alternatives exclude booleans.
Annotation.withValue(annotation,false);
// @ts-expect-error Tokens cannot be constructed by callers.
new ModelToken();
void [edited,ref,custom];
