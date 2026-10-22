namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type SampleOperations internal (session: GraphContext) =
    member _.create(name: string): Sample = session.Add<Sample>(Sample(name), Some "Sample")
    member _.add(value: Sample): Sample = session.Add<Sample>(value, Some "Sample")
    member _.upsert(value: Sample, ?recursive: bool): Sample = session.Upsert<Sample>(value, Some "Sample", defaultArg recursive false)
    member _.get(id: string) = session.Get<Sample>("Sample",id)
    member _.list() = session.List<Sample>("Sample")
    member _.delete(value: Sample) = session.Delete(value)
    member _.setName(value: Sample, replacement: string) =
        session.Change<Sample>(value,"name",Some(Text(Model.required "name" replacement)),"Sample.setName")
    member _.setAdditionalProperties(value: Sample, replacement: seq<Annotation>) =
        session.Change<Sample>(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Sample.setAdditionalProperties")
    member _.addAdditionalProperty(value: Sample, target: Annotation) = session.Collection<Sample>(value,"additionalProperties",target,true,"Sample.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Sample, target: Annotation) = session.Collection<Sample>(value,"additionalProperties",target,false,"Sample.removeAdditionalProperty")
