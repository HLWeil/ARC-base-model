namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DescriptorOperations internal (session: GraphContext) =
    member _.create(describes: EntityReference) = let value = Descriptor(describes) in session.Register(value,"Descriptor"); value
    member _.add(value: Descriptor) = session.Register(value,"Descriptor"); value
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
