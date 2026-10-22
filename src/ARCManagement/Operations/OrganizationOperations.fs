namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type OrganizationOperations internal (session: GraphContext) =
    member _.create(name: string) = let value = Organization(name) in session.Register(value,"Organization"); value
    member _.add(value: Organization) = session.Register(value,"Organization"); value
    member _.upsert(value: Organization): unit = session.Set(value,"Organization")
    member _.get(id: string) = session.Get<Organization>("Organization",id)
    member _.list() = session.List<Organization>("Organization")
    member _.delete(value: Organization) = session.Delete(value)
    member _.setName(value: Organization, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Organization.setName")
    member _.setUrl(value: Organization, replacement: string option) =
        session.Change(value,"url",replacement |> Option.map (fun replacement -> Text(Model.required "url" replacement)),"Organization.setUrl")
