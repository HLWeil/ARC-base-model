namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type OrganizationOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<Organization>(revision, Organization(name), "Organization", true, false)
    member _.add(revision: RevisionToken, value: Organization) = session.Adopt<Organization>(revision, value, "Organization", true, false)
    member _.upsert(revision: RevisionToken, value: Organization, ?recursive: bool) = session.Adopt<Organization>(revision, value, "Organization", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Organization>("Organization",id)
    member _.list() = session.List<Organization>("Organization")
    member _.read(id: string) = session.Read<Organization>("Organization",id)
    member _.readAll() = session.ReadAll<Organization>("Organization")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Organization",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Organization>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"Organization.setName")
    member _.setUrl(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Organization>(revision,id,"url", (fun () -> Some(Text(Model.required "url" replacement))),"Organization.setUrl")
    member _.clearUrl(revision: RevisionToken, id: string) = session.Change<Organization>(revision,id,"url", (fun () -> None),"Organization.clearUrl")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Organization>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Organization.setAdditionalTypes")
