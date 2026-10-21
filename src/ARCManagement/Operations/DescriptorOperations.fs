namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DescriptorOperations internal (session: GraphContext) =
    member _.create(target: EntityReference) = session.CreateDescriptor(target)
    member _.add(value: Descriptor) = session.Adopt<Descriptor>(value, "Descriptor", true,None,false)
    member _.upsert(value: Descriptor, ?baseline: Descriptor, ?recursive: bool) = session.Adopt<Descriptor>(value, "Descriptor", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Descriptor>("Descriptor",id)
    member _.list() = session.List<Descriptor>("Descriptor")
    member _.read(id: string) = session.Read<Descriptor>("Descriptor",id)
    member _.readAll() = session.ReadAll<Descriptor>("Descriptor")
    member _.delete(value: Descriptor, ?cascade: bool) = session.Delete(session.Id(value,"Descriptor"),"Descriptor",defaultArg cascade false)
    member _.setDescribes(value: Descriptor, replacement: EntityReference) =
        session.Endpoint<Descriptor>(session.Id(value,"Descriptor"),"describes",replacement,"Descriptor.setDescribes")
    member _.setAnnotations(value: Descriptor, replacement: seq<Annotation>) =
        session.Change<Descriptor>(session.Id(value,"Descriptor"),"annotations", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"Descriptor.setAnnotations")
    member _.addAnnotation(value: Descriptor, target: Annotation) = session.Collection<Descriptor>(session.Id(value,"Descriptor"),"annotations",target,"Annotation",true,"Descriptor.addAnnotation")
    member _.removeAnnotation(value: Descriptor, target: Annotation) = session.Collection<Descriptor>(session.Id(value,"Descriptor"),"annotations",target,"Annotation",false,"Descriptor.removeAnnotation")
    member _.setAdditionalTypes(value: Descriptor, replacement: seq<string>) =
        session.Change<Descriptor>(session.Id(value,"Descriptor"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Descriptor.setAdditionalTypes")
    member _.setDescribesSample(value: Descriptor, target: Sample) = session.Reference<Descriptor>(session.Id(value,"Descriptor"),"describes",target,"Sample","Descriptor.setDescribesSample")
    member _.setDescribesData(value: Descriptor, target: Data) = session.Reference<Descriptor>(session.Id(value,"Descriptor"),"describes",target,"Data","Descriptor.setDescribesData")
