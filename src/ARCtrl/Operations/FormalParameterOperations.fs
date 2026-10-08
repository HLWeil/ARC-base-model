namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type FormalParameterOperations internal (session: Session) =
    member _.create(name: string) = let value = FormalParameter(name) in session.Register(value,"FormalParameter"); value
    member _.register(value: FormalParameter) = session.Register(value,"FormalParameter"); value
    member _.upsert(value: FormalParameter): unit = session.Set(value,"FormalParameter")
    member _.get(id: string) = session.Get<FormalParameter>("FormalParameter",id)
    member _.list() = session.List<FormalParameter>("FormalParameter")
    member _.delete(value: FormalParameter) = session.Delete(value)
    member _.setName(value: FormalParameter, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"FormalParameter.setName")
    member _.clearName(value: FormalParameter) = session.Change(value,"name",None,"FormalParameter.clearName")
    member _.setNameTAN(value: FormalParameter, replacement: string) =
        session.Change(value,"nameTAN",Some(Text(Model.required "nameTAN" replacement)),"FormalParameter.setNameTAN")
    member _.clearNameTAN(value: FormalParameter) = session.Change(value,"nameTAN",None,"FormalParameter.clearNameTAN")
    member _.setDefaultValue(value: FormalParameter, replacement: Annotation) =
        session.Change(value,"defaultValue",Some(Links [session.Id(replacement)]),"FormalParameter.setDefaultValue")
    member _.clearDefaultValue(value: FormalParameter) = session.Change(value,"defaultValue",None,"FormalParameter.clearDefaultValue")
    member _.setAdditionalTypes(value: FormalParameter, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"FormalParameter.setAdditionalTypes")
