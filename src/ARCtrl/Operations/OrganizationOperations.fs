namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type OrganizationOperations internal (session: Session) =
    member _.create(name: string) = let value = Organization(name) in session.Register(value,"Organization"); value
    member _.register(value: Organization) = session.Register(value,"Organization"); value
    member _.upsert(value: Organization): unit = session.Set(value,"Organization")
    member _.get(id: string) = session.Get<Organization>("Organization",id)
    member _.list() = session.List<Organization>("Organization")
    member _.delete(value: Organization) = session.Delete(value)
    member _.setName(value: Organization, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Organization.setName")
    member _.setUrl(value: Organization, replacement: string) =
        session.Change(value,"url",Some(Text(Model.required "url" replacement)),"Organization.setUrl")
    member _.clearUrl(value: Organization) = session.Change(value,"url",None,"Organization.clearUrl")
    member _.setAdditionalTypes(value: Organization, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Organization.setAdditionalTypes")
