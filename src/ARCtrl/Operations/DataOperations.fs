namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type DataOperations internal (session: Session) =
    member _.create(path: string) = let value = Data(path) in session.Register(value,"Data"); value
    member _.register(value: Data) = session.Register(value,"Data"); value
    member _.upsert(value: Data): unit = session.Set(value,"Data")
    member _.get(id: string) = session.Get<Data>("Data",id)
    member _.list() = session.List<Data>("Data")
    member _.delete(value: Data) = session.Delete(value)
    member _.setPath(value: Data, replacement: string) =
        session.Change(value,"path",Some(Text(Model.required "path" replacement)),"Data.setPath")
    member _.setSelector(value: Data, replacement: string) =
        session.Change(value,"selector",Some(Text(Model.required "selector" replacement)),"Data.setSelector")
    member _.clearSelector(value: Data) = session.Change(value,"selector",None,"Data.clearSelector")
    member _.setSelectorFormat(value: Data, replacement: string) =
        session.Change(value,"selectorFormat",Some(Text(Model.required "selectorFormat" replacement)),"Data.setSelectorFormat")
    member _.clearSelectorFormat(value: Data) = session.Change(value,"selectorFormat",None,"Data.clearSelectorFormat")
    member _.setEncodingFormat(value: Data, replacement: string) =
        session.Change(value,"encodingFormat",Some(Text(Model.required "encodingFormat" replacement)),"Data.setEncodingFormat")
    member _.clearEncodingFormat(value: Data) = session.Change(value,"encodingFormat",None,"Data.clearEncodingFormat")
    member _.setHasParts(value: Data, replacement: seq<Data>) =
        session.Change(value,"hasParts",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setHasParts")
    member _.addPart(value: Data, target: Data) = session.Collection(value,"hasParts",target,true,"Data.addPart")
    member _.removePart(value: Data, target: Data) = session.Collection(value,"hasParts",target,false,"Data.removePart")
    member _.setAdditionalProperties(value: Data, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setAdditionalProperties")
    member _.addAdditionalProperty(value: Data, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Data.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Data, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Data.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Data, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Data.setAdditionalTypes")
