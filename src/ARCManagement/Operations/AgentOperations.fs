namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type AgentOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<Agent>(revision, Agent(name), "Agent", true, false)
    member _.add(revision: RevisionToken, value: Agent) = session.Adopt<Agent>(revision, value, "Agent", true, false)
    member _.upsert(revision: RevisionToken, value: Agent, ?recursive: bool) = session.Adopt<Agent>(revision, value, "Agent", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Agent>("Agent",id)
    member _.list() = session.List<Agent>("Agent")
    member _.read(id: string) = session.Read<Agent>("Agent",id)
    member _.readAll() = session.ReadAll<Agent>("Agent")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Agent",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Agent>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"Agent.setName")
    member _.setGivenName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Agent>(revision,id,"givenName", (fun () -> Some(Text(Model.required "givenName" replacement))),"Agent.setGivenName")
    member _.clearGivenName(revision: RevisionToken, id: string) = session.Change<Agent>(revision,id,"givenName", (fun () -> None),"Agent.clearGivenName")
    member _.setFamilyName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Agent>(revision,id,"familyName", (fun () -> Some(Text(Model.required "familyName" replacement))),"Agent.setFamilyName")
    member _.clearFamilyName(revision: RevisionToken, id: string) = session.Change<Agent>(revision,id,"familyName", (fun () -> None),"Agent.clearFamilyName")
    member _.setEmails(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Agent>(revision,id,"emails", (fun () -> Some(Texts(List.ofSeq replacement))),"Agent.setEmails")
    member _.setAffiliations(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Agent>(revision,id,"affiliations", (fun () -> Some(Links(replacement |> List.ofSeq))),"Agent.setAffiliations")
    member _.addAffiliation(revision: RevisionToken, id: string, target: string) = session.Collection<Agent>(revision,id,"affiliations",target,true,"Agent.addAffiliation")
    member _.removeAffiliation(revision: RevisionToken, id: string, target: string) = session.Collection<Agent>(revision,id,"affiliations",target,false,"Agent.removeAffiliation")
    member _.setIdentifiers(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Agent>(revision,id,"identifiers", (fun () -> Some(Texts(List.ofSeq replacement))),"Agent.setIdentifiers")
    member _.setAdditionalProperties(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Agent>(revision,id,"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Agent.setAdditionalProperties")
    member _.addAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Agent>(revision,id,"additionalProperties",target,true,"Agent.addAdditionalProperty")
    member _.removeAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Agent>(revision,id,"additionalProperties",target,false,"Agent.removeAdditionalProperty")
    member _.setJobTitles(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Agent>(revision,id,"jobTitles", (fun () -> Some(Links(replacement |> List.ofSeq))),"Agent.setJobTitles")
    member _.addJobTitle(revision: RevisionToken, id: string, target: string) = session.Collection<Agent>(revision,id,"jobTitles",target,true,"Agent.addJobTitle")
    member _.removeJobTitle(revision: RevisionToken, id: string, target: string) = session.Collection<Agent>(revision,id,"jobTitles",target,false,"Agent.removeJobTitle")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Agent>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Agent.setAdditionalTypes")
