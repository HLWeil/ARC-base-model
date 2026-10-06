namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type AgentOperations internal (session: Session) =
    member _.create(name: string) = let value = Agent(name) in session.Register(value,"Agent"); value
    member _.register(value: Agent) = session.Register(value,"Agent"); value
    member _.set(value: Agent): unit = session.Set(value,"Agent")
    member _.get(id: string) = session.Get<Agent>("Agent",id)
    member _.list() = session.List<Agent>("Agent")
    member _.delete(value: Agent) = session.Delete(value)
    member _.setName(value: Agent, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Agent.setName")
    member _.setGivenName(value: Agent, replacement: string) =
        session.Change(value,"givenName",Some(Text(Model.required "givenName" replacement)),"Agent.setGivenName")
    member _.clearGivenName(value: Agent) = session.Change(value,"givenName",None,"Agent.clearGivenName")
    member _.setFamilyName(value: Agent, replacement: string) =
        session.Change(value,"familyName",Some(Text(Model.required "familyName" replacement)),"Agent.setFamilyName")
    member _.clearFamilyName(value: Agent) = session.Change(value,"familyName",None,"Agent.clearFamilyName")
    member _.setEmails(value: Agent, replacement: seq<string>) =
        session.Change(value,"emails",Some(Texts(List.ofSeq replacement)),"Agent.setEmails")
    member _.setAffiliations(value: Agent, replacement: seq<Organization>) =
        session.Change(value,"affiliations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setAffiliations")
    member _.addAffiliation(value: Agent, target: Organization) = session.Collection(value,"affiliations",target,true,"Agent.addAffiliation")
    member _.removeAffiliation(value: Agent, target: Organization) = session.Collection(value,"affiliations",target,false,"Agent.removeAffiliation")
    member _.setIdentifiers(value: Agent, replacement: seq<string>) =
        session.Change(value,"identifiers",Some(Texts(List.ofSeq replacement)),"Agent.setIdentifiers")
    member _.setAdditionalProperties(value: Agent, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setAdditionalProperties")
    member _.addAdditionalProperty(value: Agent, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Agent.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Agent, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Agent.removeAdditionalProperty")
    member _.setJobTitles(value: Agent, replacement: seq<DefinedTerm>) =
        session.Change(value,"jobTitles",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setJobTitles")
    member _.addJobTitle(value: Agent, target: DefinedTerm) = session.Collection(value,"jobTitles",target,true,"Agent.addJobTitle")
    member _.removeJobTitle(value: Agent, target: DefinedTerm) = session.Collection(value,"jobTitles",target,false,"Agent.removeJobTitle")
    member _.setAdditionalTypes(value: Agent, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Agent.setAdditionalTypes")
