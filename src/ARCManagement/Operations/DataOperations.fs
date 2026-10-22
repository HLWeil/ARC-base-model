namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DataOperations internal (session: GraphContext) =
    member _.create(path: string) = let value = Data(path) in session.Register(value,"Data"); value
    member _.add(value: Data) = session.Register(value,"Data"); value
    member _.upsert(value: Data): unit = session.Set(value,"Data")
    member _.get(id: string) = session.Get<Data>("Data",id)
    member _.list() = session.List<Data>("Data")
    member _.delete(value: Data) = session.Delete(value)
    member _.setPath(value: Data, replacement: string) =
        session.Change(value,"path",Some(Text(Model.required "path" replacement)),"Data.setPath")
    member _.setSelector(value: Data, replacement: string option) =
        session.Change(value,"selector",replacement |> Option.map (fun replacement -> Text(Model.required "selector" replacement)),"Data.setSelector")
    member _.setSelectorFormat(value: Data, replacement: string option) =
        session.Change(value,"selectorFormat",replacement |> Option.map (fun replacement -> Text(Model.required "selectorFormat" replacement)),"Data.setSelectorFormat")
    member _.setEncodingFormat(value: Data, replacement: string option) =
        session.Change(value,"encodingFormat",replacement |> Option.map (fun replacement -> Text(Model.required "encodingFormat" replacement)),"Data.setEncodingFormat")
    member _.setHasParts(value: Data, replacement: seq<Data>) =
        session.Change(value,"hasParts",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setHasParts")
    member _.addPart(value: Data, target: Data) = session.Collection(value,"hasParts",target,true,"Data.addPart")
    member _.removePart(value: Data, target: Data) = session.Collection(value,"hasParts",target,false,"Data.removePart")
    member _.setAdditionalProperties(value: Data, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setAdditionalProperties")
    member _.addAdditionalProperty(value: Data, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Data.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Data, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Data.removeAdditionalProperty")
