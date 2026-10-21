namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type SampleOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<Sample>(Sample(name), "Sample", true,None,false)
    member _.add(value: Sample) = session.Adopt<Sample>(value, "Sample", true,None,false)
    member _.upsert(value: Sample, ?baseline: Sample, ?recursive: bool) = session.Adopt<Sample>(value, "Sample", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Sample>("Sample",id)
    member _.list() = session.List<Sample>("Sample")
    member _.read(id: string) = session.Read<Sample>("Sample",id)
    member _.readAll() = session.ReadAll<Sample>("Sample")
    member _.delete(value: Sample, ?cascade: bool) = session.Delete(session.Id(value,"Sample"),"Sample",defaultArg cascade false)
    member _.setName(value: Sample, replacement: string) =
        session.Change<Sample>(session.Id(value,"Sample"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"Sample.setName")
    member _.setAdditionalProperties(value: Sample, replacement: seq<Annotation>) =
        session.Change<Sample>(session.Id(value,"Sample"),"additionalProperties", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"Sample.setAdditionalProperties")
    member _.addAdditionalProperty(value: Sample, target: Annotation) = session.Collection<Sample>(session.Id(value,"Sample"),"additionalProperties",target,"Annotation",true,"Sample.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Sample, target: Annotation) = session.Collection<Sample>(session.Id(value,"Sample"),"additionalProperties",target,"Annotation",false,"Sample.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Sample, replacement: seq<string>) =
        session.Change<Sample>(session.Id(value,"Sample"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Sample.setAdditionalTypes")
