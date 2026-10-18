namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DescriptorOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, targetId: string) = session.CreateDescriptor(revision,targetId)
    member _.add(revision: RevisionToken, value: Descriptor) = session.Adopt<Descriptor>(revision, value, "Descriptor", true, false)
    member _.upsert(revision: RevisionToken, value: Descriptor, ?recursive: bool) = session.Adopt<Descriptor>(revision, value, "Descriptor", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Descriptor>("Descriptor",id)
    member _.list() = session.List<Descriptor>("Descriptor")
    member _.read(id: string) = session.Read<Descriptor>("Descriptor",id)
    member _.readAll() = session.ReadAll<Descriptor>("Descriptor")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Descriptor",defaultArg cascade false)
    member _.setDescribes(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Descriptor>(revision,id,"describes", (fun () -> Some(Links [replacement])),"Descriptor.setDescribes")
    member _.setAnnotations(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Descriptor>(revision,id,"annotations", (fun () -> Some(Links(replacement |> List.ofSeq))),"Descriptor.setAnnotations")
    member _.addAnnotation(revision: RevisionToken, id: string, target: string) = session.Collection<Descriptor>(revision,id,"annotations",target,true,"Descriptor.addAnnotation")
    member _.removeAnnotation(revision: RevisionToken, id: string, target: string) = session.Collection<Descriptor>(revision,id,"annotations",target,false,"Descriptor.removeAnnotation")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Descriptor>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Descriptor.setAdditionalTypes")
    member _.setDescribesSample(revision: RevisionToken, id: string, target: string) = session.Change<Descriptor>(revision,id,"describes", (fun () -> Some(Links [target])),"Descriptor.setDescribesSample")
    member _.setDescribesData(revision: RevisionToken, id: string, target: string) = session.Change<Descriptor>(revision,id,"describes", (fun () -> Some(Links [target])),"Descriptor.setDescribesData")
