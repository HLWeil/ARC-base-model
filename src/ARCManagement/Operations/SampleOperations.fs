namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type SampleOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<Sample>(revision, Sample(name), "Sample", true, false)
    member _.add(revision: RevisionToken, value: Sample) = session.Adopt<Sample>(revision, value, "Sample", true, false)
    member _.upsert(revision: RevisionToken, value: Sample, ?recursive: bool) = session.Adopt<Sample>(revision, value, "Sample", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Sample>("Sample",id)
    member _.list() = session.List<Sample>("Sample")
    member _.read(id: string) = session.Read<Sample>("Sample",id)
    member _.readAll() = session.ReadAll<Sample>("Sample")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Sample",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Sample>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"Sample.setName")
    member _.setAdditionalProperties(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Sample>(revision,id,"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Sample.setAdditionalProperties")
    member _.addAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Sample>(revision,id,"additionalProperties",target,true,"Sample.addAdditionalProperty")
    member _.removeAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Sample>(revision,id,"additionalProperties",target,false,"Sample.removeAdditionalProperty")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Sample>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Sample.setAdditionalTypes")
