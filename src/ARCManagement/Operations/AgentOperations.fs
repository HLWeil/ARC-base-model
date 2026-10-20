namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type AgentOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<Agent>(Agent(name), "Agent", true,None,false)
    member _.add(value: Agent) = session.Adopt<Agent>(value, "Agent", true,None,false)
    member _.upsert(value: Agent, ?baseline: Agent, ?recursive: bool) = session.Adopt<Agent>(value, "Agent", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Agent>("Agent",id)
    member _.list() = session.List<Agent>("Agent")
    member _.read(id: string) = session.Read<Agent>("Agent",id)
    member _.readAll() = session.ReadAll<Agent>("Agent")
    member _.delete(value: Agent, ?cascade: bool) = session.Delete(session.Id(value,"Agent"),"Agent",defaultArg cascade false)
    member _.setName(value: Agent, replacement: string) =
        session.Change<Agent>(session.Id(value,"Agent"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"Agent.setName")
    member _.setGivenName(value: Agent, replacement: string) =
        session.Change<Agent>(session.Id(value,"Agent"),"givenName", (fun () -> Some(Text(Model.required "givenName" replacement))),"Agent.setGivenName")
    member _.clearGivenName(value: Agent) = session.Change<Agent>(session.Id(value,"Agent"),"givenName", (fun () -> None),"Agent.clearGivenName")
    member _.setFamilyName(value: Agent, replacement: string) =
        session.Change<Agent>(session.Id(value,"Agent"),"familyName", (fun () -> Some(Text(Model.required "familyName" replacement))),"Agent.setFamilyName")
    member _.clearFamilyName(value: Agent) = session.Change<Agent>(session.Id(value,"Agent"),"familyName", (fun () -> None),"Agent.clearFamilyName")
    member _.setEmails(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(session.Id(value,"Agent"),"emails", (fun () -> Some(Texts(List.ofSeq replacement))),"Agent.setEmails")
    member _.setAffiliations(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(session.Id(value,"Agent"),"affiliations", (fun () -> Some(Links(replacement |> List.ofSeq))),"Agent.setAffiliations")
    member _.addAffiliation(value: Agent, target: string) = session.Collection<Agent>(session.Id(value,"Agent"),"affiliations",target,true,"Agent.addAffiliation")
    member _.removeAffiliation(value: Agent, target: string) = session.Collection<Agent>(session.Id(value,"Agent"),"affiliations",target,false,"Agent.removeAffiliation")
    member _.setIdentifiers(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(session.Id(value,"Agent"),"identifiers", (fun () -> Some(Texts(List.ofSeq replacement))),"Agent.setIdentifiers")
    member _.setAdditionalProperties(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(session.Id(value,"Agent"),"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Agent.setAdditionalProperties")
    member _.addAdditionalProperty(value: Agent, target: string) = session.Collection<Agent>(session.Id(value,"Agent"),"additionalProperties",target,true,"Agent.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Agent, target: string) = session.Collection<Agent>(session.Id(value,"Agent"),"additionalProperties",target,false,"Agent.removeAdditionalProperty")
    member _.setJobTitles(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(session.Id(value,"Agent"),"jobTitles", (fun () -> Some(Links(replacement |> List.ofSeq))),"Agent.setJobTitles")
    member _.addJobTitle(value: Agent, target: string) = session.Collection<Agent>(session.Id(value,"Agent"),"jobTitles",target,true,"Agent.addJobTitle")
    member _.removeJobTitle(value: Agent, target: string) = session.Collection<Agent>(session.Id(value,"Agent"),"jobTitles",target,false,"Agent.removeJobTitle")
    member _.setAdditionalTypes(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(session.Id(value,"Agent"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Agent.setAdditionalTypes")
