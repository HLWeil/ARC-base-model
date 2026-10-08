namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type SampleOperations internal (session: Session) =
    member _.create(name: string) = let value = Sample(name) in session.Register(value,"Sample"); value
    member _.register(value: Sample) = session.Register(value,"Sample"); value
    member _.upsert(value: Sample): unit = session.Set(value,"Sample")
    member _.get(id: string) = session.Get<Sample>("Sample",id)
    member _.list() = session.List<Sample>("Sample")
    member _.delete(value: Sample) = session.Delete(value)
    member _.setName(value: Sample, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Sample.setName")
    member _.setAdditionalProperties(value: Sample, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Sample.setAdditionalProperties")
    member _.addAdditionalProperty(value: Sample, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Sample.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Sample, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Sample.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Sample, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Sample.setAdditionalTypes")
