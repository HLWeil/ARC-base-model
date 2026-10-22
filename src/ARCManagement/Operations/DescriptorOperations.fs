namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DescriptorOperations internal (session: GraphContext) =
    member _.create(describes: EntityReference): Descriptor = session.Add<Descriptor>(Descriptor(describes), Some "Descriptor")
    member _.add(value: Descriptor): Descriptor = session.Add<Descriptor>(value, Some "Descriptor")
    member _.upsert(value: Descriptor, ?recursive: bool): Descriptor = session.Upsert<Descriptor>(value, Some "Descriptor", defaultArg recursive false)
    member _.get(id: string) = session.Get<Descriptor>("Descriptor",id)
    member _.list(): seq<Descriptor> = session.List<Descriptor>("Descriptor")
    member _.delete(value: Descriptor) = session.Delete(value)
    member _.setDescribes(value: Descriptor, replacement: EntityReference) =
        session.Change<Descriptor>(value,"describes",Some(Links [session.Id(Model.endpoint replacement)]),"Descriptor.setDescribes")
    member _.setAnnotations(value: Descriptor, replacement: seq<Annotation>) =
        session.Change<Descriptor>(value,"annotations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Descriptor.setAnnotations")
    member _.addAnnotation(value: Descriptor, target: Annotation) = session.Collection<Descriptor>(value,"annotations",target,true,"Descriptor.addAnnotation")
    member _.removeAnnotation(value: Descriptor, target: Annotation) = session.Collection<Descriptor>(value,"annotations",target,false,"Descriptor.removeAnnotation")
