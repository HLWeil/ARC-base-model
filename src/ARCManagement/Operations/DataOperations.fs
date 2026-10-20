namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DataOperations internal (session: GraphContext) =
    member _.create(path: string) = session.Adopt<Data>(Data(path), "Data", true,None,false)
    member _.add(value: Data) = session.Adopt<Data>(value, "Data", true,None,false)
    member _.upsert(value: Data, ?baseline: Data, ?recursive: bool) = session.Adopt<Data>(value, "Data", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Data>("Data",id)
    member _.list() = session.List<Data>("Data")
    member _.read(id: string) = session.Read<Data>("Data",id)
    member _.readAll() = session.ReadAll<Data>("Data")
    member _.delete(value: Data, ?cascade: bool) = session.Delete(session.Id(value,"Data"),"Data",defaultArg cascade false)
    member _.setPath(value: Data, replacement: string) =
        session.Change<Data>(session.Id(value,"Data"),"path", (fun () -> Some(Text(Model.required "path" replacement))),"Data.setPath")
    member _.setSelector(value: Data, replacement: string) =
        session.Change<Data>(session.Id(value,"Data"),"selector", (fun () -> Some(Text(Model.required "selector" replacement))),"Data.setSelector")
    member _.clearSelector(value: Data) = session.Change<Data>(session.Id(value,"Data"),"selector", (fun () -> None),"Data.clearSelector")
    member _.setSelectorFormat(value: Data, replacement: string) =
        session.Change<Data>(session.Id(value,"Data"),"selectorFormat", (fun () -> Some(Text(Model.required "selectorFormat" replacement))),"Data.setSelectorFormat")
    member _.clearSelectorFormat(value: Data) = session.Change<Data>(session.Id(value,"Data"),"selectorFormat", (fun () -> None),"Data.clearSelectorFormat")
    member _.setEncodingFormat(value: Data, replacement: string) =
        session.Change<Data>(session.Id(value,"Data"),"encodingFormat", (fun () -> Some(Text(Model.required "encodingFormat" replacement))),"Data.setEncodingFormat")
    member _.clearEncodingFormat(value: Data) = session.Change<Data>(session.Id(value,"Data"),"encodingFormat", (fun () -> None),"Data.clearEncodingFormat")
    member _.setHasParts(value: Data, replacement: seq<string>) =
        session.Change<Data>(session.Id(value,"Data"),"hasParts", (fun () -> Some(Links(replacement |> List.ofSeq))),"Data.setHasParts")
    member _.addPart(value: Data, target: string) = session.Collection<Data>(session.Id(value,"Data"),"hasParts",target,true,"Data.addPart")
    member _.removePart(value: Data, target: string) = session.Collection<Data>(session.Id(value,"Data"),"hasParts",target,false,"Data.removePart")
    member _.setAdditionalProperties(value: Data, replacement: seq<string>) =
        session.Change<Data>(session.Id(value,"Data"),"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Data.setAdditionalProperties")
    member _.addAdditionalProperty(value: Data, target: string) = session.Collection<Data>(session.Id(value,"Data"),"additionalProperties",target,true,"Data.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Data, target: string) = session.Collection<Data>(session.Id(value,"Data"),"additionalProperties",target,false,"Data.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Data, replacement: seq<string>) =
        session.Change<Data>(session.Id(value,"Data"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Data.setAdditionalTypes")
