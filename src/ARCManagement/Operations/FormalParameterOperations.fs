namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type FormalParameterOperations internal (session: GraphContext) =
    member _.create(name: string) = let value = FormalParameter(name) in session.Register(value,"FormalParameter"); value
    member _.add(value: FormalParameter) = session.Register(value,"FormalParameter"); value
    member _.upsert(value: FormalParameter): unit = session.Set(value,"FormalParameter")
    member _.get(id: string) = session.Get<FormalParameter>("FormalParameter",id)
    member _.list() = session.List<FormalParameter>("FormalParameter")
    member _.delete(value: FormalParameter) = session.Delete(value)
    member _.setName(value: FormalParameter, replacement: string option) =
        session.Change(value,"name",replacement |> Option.map (fun replacement -> Text(Model.required "name" replacement)),"FormalParameter.setName")
    member _.setNameTAN(value: FormalParameter, replacement: string option) =
        session.Change(value,"nameTAN",replacement |> Option.map (fun replacement -> Text(Model.required "nameTAN" replacement)),"FormalParameter.setNameTAN")
    member _.setDefaultValue(value: FormalParameter, replacement: Annotation option) =
        session.Change(value,"defaultValue",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),"FormalParameter.setDefaultValue")
