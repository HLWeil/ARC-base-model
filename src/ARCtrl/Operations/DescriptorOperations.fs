namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type DescriptorOperations internal (session: Session) =
    member _.create(describes: EntityReference) = let value = Descriptor(describes) in session.Register(value,"Descriptor"); value
    member _.register(value: Descriptor) = session.Register(value,"Descriptor"); value
    member _.upsert(value: Descriptor): unit = session.Set(value,"Descriptor")
    member _.get(id: string) = session.Get<Descriptor>("Descriptor",id)
    member _.list() = session.List<Descriptor>("Descriptor")
    member _.delete(value: Descriptor) = session.Delete(value)
    member _.setDescribes(value: Descriptor, replacement: EntityReference) =
        session.Change(value,"describes",Some(Links [session.Id(Model.endpoint replacement)]),"Descriptor.setDescribes")
    member _.setAnnotations(value: Descriptor, replacement: seq<Annotation>) =
        session.Change(value,"annotations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Descriptor.setAnnotations")
    member _.addAnnotation(value: Descriptor, target: Annotation) = session.Collection(value,"annotations",target,true,"Descriptor.addAnnotation")
    member _.removeAnnotation(value: Descriptor, target: Annotation) = session.Collection(value,"annotations",target,false,"Descriptor.removeAnnotation")
    member _.setAdditionalTypes(value: Descriptor, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Descriptor.setAdditionalTypes")
    member _.setDescribesSample(value: Descriptor, target: Sample) = session.Change(value,"describes",Some(Links [session.Id(target)]),"Descriptor.setDescribesSample")
    member _.setDescribesData(value: Descriptor, target: Data) = session.Change(value,"describes",Some(Links [session.Id(target)]),"Descriptor.setDescribesData")
