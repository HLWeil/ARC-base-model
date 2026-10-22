namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type AgentOperations internal (session: GraphContext) =
    member _.create(name: string): Agent = session.Add<Agent>(Agent(name), Some "Agent")
    member _.add(value: Agent): Agent = session.Add<Agent>(value, Some "Agent")
    member _.upsert(value: Agent, ?recursive: bool): Agent = session.Upsert<Agent>(value, Some "Agent", defaultArg recursive false)
    member _.get(id: string) = session.Get<Agent>("Agent",id)
    member _.list(): seq<Agent> = session.List<Agent>("Agent")
    member _.delete(value: Agent) = session.Delete(value)
    member _.setName(value: Agent, replacement: string) =
        session.Change<Agent>(value,"name",Some(Text(Model.required "name" replacement)),"Agent.setName")
    member _.setGivenName(value: Agent, replacement: string option) =
        session.Change<Agent>(value,"givenName",replacement |> Option.map (fun replacement -> Text(Model.required "givenName" replacement)),"Agent.setGivenName")
    member _.setFamilyName(value: Agent, replacement: string option) =
        session.Change<Agent>(value,"familyName",replacement |> Option.map (fun replacement -> Text(Model.required "familyName" replacement)),"Agent.setFamilyName")
    member _.setEmails(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(value,"emails",Some(Texts(List.ofSeq replacement)),"Agent.setEmails")
    member _.setAffiliations(value: Agent, replacement: seq<Organization>) =
        session.Change<Agent>(value,"affiliations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setAffiliations")
    member _.addAffiliation(value: Agent, target: Organization) = session.Collection<Agent>(value,"affiliations",target,true,"Agent.addAffiliation")
    member _.removeAffiliation(value: Agent, target: Organization) = session.Collection<Agent>(value,"affiliations",target,false,"Agent.removeAffiliation")
    member _.setIdentifiers(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(value,"identifiers",Some(Texts(List.ofSeq replacement)),"Agent.setIdentifiers")
    member _.setAdditionalProperties(value: Agent, replacement: seq<Annotation>) =
        session.Change<Agent>(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setAdditionalProperties")
    member _.addAdditionalProperty(value: Agent, target: Annotation) = session.Collection<Agent>(value,"additionalProperties",target,true,"Agent.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Agent, target: Annotation) = session.Collection<Agent>(value,"additionalProperties",target,false,"Agent.removeAdditionalProperty")
    member _.setJobTitles(value: Agent, replacement: seq<DefinedTerm>) =
        session.Change<Agent>(value,"jobTitles",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setJobTitles")
    member _.addJobTitle(value: Agent, target: DefinedTerm) = session.Collection<Agent>(value,"jobTitles",target,true,"Agent.addJobTitle")
    member _.removeJobTitle(value: Agent, target: DefinedTerm) = session.Collection<Agent>(value,"jobTitles",target,false,"Agent.removeJobTitle")
