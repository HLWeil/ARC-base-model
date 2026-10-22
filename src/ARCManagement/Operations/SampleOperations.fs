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
    member _.list(): seq<Sample> = session.List<Sample>("Sample")
    member _.delete(value: Sample) = session.Delete(value, Some "Sample")
    member _.setName(value: Sample, replacement: string) =
        session.Change<Sample>(value,Some "Sample","name",Some(Text(Model.required "name" replacement)),[],"Sample.setName")
    member _.setAdditionalProperties(value: Sample, replacement: seq<Annotation>) =
        session.ChangeLinks<Sample>(value,Some "Sample","additionalProperties",replacement |> Seq.map box,"Sample.setAdditionalProperties")
    member _.addAdditionalProperty(value: Sample, target: Annotation) = session.Collection<Sample>(value,Some "Sample","additionalProperties",target,true,"Sample.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Sample, target: Annotation) = session.Collection<Sample>(value,Some "Sample","additionalProperties",target,false,"Sample.removeAdditionalProperty")
