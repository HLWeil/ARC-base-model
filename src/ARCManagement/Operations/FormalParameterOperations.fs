namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type FormalParameterOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<FormalParameter>(FormalParameter(name), "FormalParameter", true,None,false)
    member _.add(value: FormalParameter) = session.Adopt<FormalParameter>(value, "FormalParameter", true,None,false)
    member _.upsert(value: FormalParameter, ?baseline: FormalParameter, ?recursive: bool) = session.Adopt<FormalParameter>(value, "FormalParameter", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<FormalParameter>("FormalParameter",id)
    member _.list() = session.List<FormalParameter>("FormalParameter")
    member _.read(id: string) = session.Read<FormalParameter>("FormalParameter",id)
    member _.readAll() = session.ReadAll<FormalParameter>("FormalParameter")
    member _.delete(value: FormalParameter, ?cascade: bool) = session.Delete(session.Id(value,"FormalParameter"),"FormalParameter",defaultArg cascade false)
    member _.setName(value: FormalParameter, replacement: string) =
        session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"FormalParameter.setName")
    member _.clearName(value: FormalParameter) = session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"name", (fun () -> None),"FormalParameter.clearName")
    member _.setNameTAN(value: FormalParameter, replacement: string) =
        session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"nameTAN", (fun () -> Some(Text(Model.required "nameTAN" replacement))),"FormalParameter.setNameTAN")
    member _.clearNameTAN(value: FormalParameter) = session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"nameTAN", (fun () -> None),"FormalParameter.clearNameTAN")
    member _.setDefaultValue(value: FormalParameter, replacement: string) =
        session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"defaultValue", (fun () -> Some(Links [replacement])),"FormalParameter.setDefaultValue")
    member _.clearDefaultValue(value: FormalParameter) = session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"defaultValue", (fun () -> None),"FormalParameter.clearDefaultValue")
    member _.setAdditionalTypes(value: FormalParameter, replacement: seq<string>) =
        session.Change<FormalParameter>(session.Id(value,"FormalParameter"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"FormalParameter.setAdditionalTypes")
