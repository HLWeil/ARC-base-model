namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DataOperations internal (session: GraphContext) =
    member _.create(path: string): Data = session.Add<Data>(Data(path), Some "Data")
    member _.add(value: Data): Data = session.Add<Data>(value, Some "Data")
    member _.upsert(value: Data, ?recursive: bool): Data = session.Upsert<Data>(value, Some "Data", defaultArg recursive false)
    member _.get(id: string) = session.Get<Data>("Data",id)
    member _.list() = session.List<Data>("Data")
    member _.delete(value: Data) = session.Delete(value)
    member _.setPath(value: Data, replacement: string) =
        session.Change<Data>(value,"path",Some(Text(Model.required "path" replacement)),"Data.setPath")
    member _.setSelector(value: Data, replacement: string option) =
        session.Change<Data>(value,"selector",replacement |> Option.map (fun replacement -> Text(Model.required "selector" replacement)),"Data.setSelector")
    member _.setSelectorFormat(value: Data, replacement: string option) =
        session.Change<Data>(value,"selectorFormat",replacement |> Option.map (fun replacement -> Text(Model.required "selectorFormat" replacement)),"Data.setSelectorFormat")
    member _.setEncodingFormat(value: Data, replacement: string option) =
        session.Change<Data>(value,"encodingFormat",replacement |> Option.map (fun replacement -> Text(Model.required "encodingFormat" replacement)),"Data.setEncodingFormat")
    member _.setHasParts(value: Data, replacement: seq<Data>) =
        session.Change<Data>(value,"hasParts",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setHasParts")
    member _.addPart(value: Data, target: Data) = session.Collection<Data>(value,"hasParts",target,true,"Data.addPart")
    member _.removePart(value: Data, target: Data) = session.Collection<Data>(value,"hasParts",target,false,"Data.removePart")
    member _.setAdditionalProperties(value: Data, replacement: seq<Annotation>) =
        session.Change<Data>(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setAdditionalProperties")
    member _.addAdditionalProperty(value: Data, target: Annotation) = session.Collection<Data>(value,"additionalProperties",target,true,"Data.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Data, target: Annotation) = session.Collection<Data>(value,"additionalProperties",target,false,"Data.removeAdditionalProperty")
