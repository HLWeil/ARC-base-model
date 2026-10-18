namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DataOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, path: string) = session.Adopt<Data>(revision, Data(path), "Data", true, false)
    member _.add(revision: RevisionToken, value: Data) = session.Adopt<Data>(revision, value, "Data", true, false)
    member _.upsert(revision: RevisionToken, value: Data, ?recursive: bool) = session.Adopt<Data>(revision, value, "Data", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Data>("Data",id)
    member _.list() = session.List<Data>("Data")
    member _.read(id: string) = session.Read<Data>("Data",id)
    member _.readAll() = session.ReadAll<Data>("Data")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Data",defaultArg cascade false)
    member _.setPath(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Data>(revision,id,"path", (fun () -> Some(Text(Model.required "path" replacement))),"Data.setPath")
    member _.setSelector(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Data>(revision,id,"selector", (fun () -> Some(Text(Model.required "selector" replacement))),"Data.setSelector")
    member _.clearSelector(revision: RevisionToken, id: string) = session.Change<Data>(revision,id,"selector", (fun () -> None),"Data.clearSelector")
    member _.setSelectorFormat(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Data>(revision,id,"selectorFormat", (fun () -> Some(Text(Model.required "selectorFormat" replacement))),"Data.setSelectorFormat")
    member _.clearSelectorFormat(revision: RevisionToken, id: string) = session.Change<Data>(revision,id,"selectorFormat", (fun () -> None),"Data.clearSelectorFormat")
    member _.setEncodingFormat(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Data>(revision,id,"encodingFormat", (fun () -> Some(Text(Model.required "encodingFormat" replacement))),"Data.setEncodingFormat")
    member _.clearEncodingFormat(revision: RevisionToken, id: string) = session.Change<Data>(revision,id,"encodingFormat", (fun () -> None),"Data.clearEncodingFormat")
    member _.setHasParts(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Data>(revision,id,"hasParts", (fun () -> Some(Links(replacement |> List.ofSeq))),"Data.setHasParts")
    member _.addPart(revision: RevisionToken, id: string, target: string) = session.Collection<Data>(revision,id,"hasParts",target,true,"Data.addPart")
    member _.removePart(revision: RevisionToken, id: string, target: string) = session.Collection<Data>(revision,id,"hasParts",target,false,"Data.removePart")
    member _.setAdditionalProperties(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Data>(revision,id,"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Data.setAdditionalProperties")
    member _.addAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Data>(revision,id,"additionalProperties",target,true,"Data.addAdditionalProperty")
    member _.removeAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Data>(revision,id,"additionalProperties",target,false,"Data.removeAdditionalProperty")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Data>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Data.setAdditionalTypes")
