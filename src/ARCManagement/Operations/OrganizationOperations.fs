namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type OrganizationOperations internal (session: GraphContext) =
    member _.create(name: string): Organization = session.Add<Organization>(Organization(name), Some "Organization")
    member _.add(value: Organization): Organization = session.Add<Organization>(value, Some "Organization")
    member _.upsert(value: Organization, ?recursive: bool): Organization = session.Upsert<Organization>(value, Some "Organization", defaultArg recursive false)
    member _.get(id: string) = session.Get<Organization>("Organization",id)
    member _.list() = session.List<Organization>("Organization")
    member _.delete(value: Organization) = session.Delete(value)
    member _.setName(value: Organization, replacement: string) =
        session.Change<Organization>(value,"name",Some(Text(Model.required "name" replacement)),"Organization.setName")
    member _.setUrl(value: Organization, replacement: string option) =
        session.Change<Organization>(value,"url",replacement |> Option.map (fun replacement -> Text(Model.required "url" replacement)),"Organization.setUrl")
