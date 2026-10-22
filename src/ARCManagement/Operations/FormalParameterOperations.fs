namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type FormalParameterOperations internal (session: GraphContext) =
    member _.create(name: string): FormalParameter = session.Add<FormalParameter>(FormalParameter(name), Some "FormalParameter")
    member _.add(value: FormalParameter): FormalParameter = session.Add<FormalParameter>(value, Some "FormalParameter")
    member _.upsert(value: FormalParameter, ?recursive: bool): FormalParameter = session.Upsert<FormalParameter>(value, Some "FormalParameter", defaultArg recursive false)
    member _.get(id: string) = session.Get<FormalParameter>("FormalParameter",id)
    member _.list() = session.List<FormalParameter>("FormalParameter")
    member _.delete(value: FormalParameter) = session.Delete(value)
    member _.setName(value: FormalParameter, replacement: string option) =
        session.Change<FormalParameter>(value,"name",replacement |> Option.map (fun replacement -> Text(Model.required "name" replacement)),"FormalParameter.setName")
    member _.setNameTAN(value: FormalParameter, replacement: string option) =
        session.Change<FormalParameter>(value,"nameTAN",replacement |> Option.map (fun replacement -> Text(Model.required "nameTAN" replacement)),"FormalParameter.setNameTAN")
    member _.setDefaultValue(value: FormalParameter, replacement: Annotation option) =
        session.Change<FormalParameter>(value,"defaultValue",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),"FormalParameter.setDefaultValue")
