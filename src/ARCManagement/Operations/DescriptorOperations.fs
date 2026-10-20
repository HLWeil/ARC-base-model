namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DescriptorOperations internal (session: GraphContext) =
    member _.create(targetId: string) = session.CreateDescriptor(targetId)
    member _.add(value: Descriptor) = session.Adopt<Descriptor>(value, "Descriptor", true,None,false)
    member _.upsert(value: Descriptor, ?baseline: Descriptor, ?recursive: bool) = session.Adopt<Descriptor>(value, "Descriptor", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Descriptor>("Descriptor",id)
    member _.list() = session.List<Descriptor>("Descriptor")
    member _.read(id: string) = session.Read<Descriptor>("Descriptor",id)
    member _.readAll() = session.ReadAll<Descriptor>("Descriptor")
    member _.delete(value: Descriptor, ?cascade: bool) = session.Delete(session.Id(value,"Descriptor"),"Descriptor",defaultArg cascade false)
    member _.setDescribes(value: Descriptor, replacement: string) =
        session.Change<Descriptor>(session.Id(value,"Descriptor"),"describes", (fun () -> Some(Links [replacement])),"Descriptor.setDescribes")
    member _.setAnnotations(value: Descriptor, replacement: seq<string>) =
        session.Change<Descriptor>(session.Id(value,"Descriptor"),"annotations", (fun () -> Some(Links(replacement |> List.ofSeq))),"Descriptor.setAnnotations")
    member _.addAnnotation(value: Descriptor, target: string) = session.Collection<Descriptor>(session.Id(value,"Descriptor"),"annotations",target,true,"Descriptor.addAnnotation")
    member _.removeAnnotation(value: Descriptor, target: string) = session.Collection<Descriptor>(session.Id(value,"Descriptor"),"annotations",target,false,"Descriptor.removeAnnotation")
    member _.setAdditionalTypes(value: Descriptor, replacement: seq<string>) =
        session.Change<Descriptor>(session.Id(value,"Descriptor"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Descriptor.setAdditionalTypes")
    member _.setDescribesSample(value: Descriptor, target: string) = session.Change<Descriptor>(session.Id(value,"Descriptor"),"describes", (fun () -> Some(Links [target])),"Descriptor.setDescribesSample")
    member _.setDescribesData(value: Descriptor, target: string) = session.Change<Descriptor>(session.Id(value,"Descriptor"),"describes", (fun () -> Some(Links [target])),"Descriptor.setDescribesData")
