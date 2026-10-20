namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type OrganizationOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<Organization>(Organization(name), "Organization", true,None,false)
    member _.add(value: Organization) = session.Adopt<Organization>(value, "Organization", true,None,false)
    member _.upsert(value: Organization, ?baseline: Organization, ?recursive: bool) = session.Adopt<Organization>(value, "Organization", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Organization>("Organization",id)
    member _.list() = session.List<Organization>("Organization")
    member _.read(id: string) = session.Read<Organization>("Organization",id)
    member _.readAll() = session.ReadAll<Organization>("Organization")
    member _.delete(value: Organization, ?cascade: bool) = session.Delete(session.Id(value,"Organization"),"Organization",defaultArg cascade false)
    member _.setName(value: Organization, replacement: string) =
        session.Change<Organization>(session.Id(value,"Organization"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"Organization.setName")
    member _.setUrl(value: Organization, replacement: string) =
        session.Change<Organization>(session.Id(value,"Organization"),"url", (fun () -> Some(Text(Model.required "url" replacement))),"Organization.setUrl")
    member _.clearUrl(value: Organization) = session.Change<Organization>(session.Id(value,"Organization"),"url", (fun () -> None),"Organization.clearUrl")
    member _.setAdditionalTypes(value: Organization, replacement: seq<string>) =
        session.Change<Organization>(session.Id(value,"Organization"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Organization.setAdditionalTypes")
