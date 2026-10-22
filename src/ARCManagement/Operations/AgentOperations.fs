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
    member _.delete(value: Agent) = session.Delete(value, Some "Agent")
    member _.setName(value: Agent, replacement: string) =
        session.Change<Agent>(value,Some "Agent","name",Some(Text(Model.required "name" replacement)),[],"Agent.setName")
    member _.setGivenName(value: Agent, replacement: string option) =
        session.Change<Agent>(value,Some "Agent","givenName",replacement |> Option.map (fun replacement -> Text(Model.required "givenName" replacement)),[],"Agent.setGivenName")
    member _.setFamilyName(value: Agent, replacement: string option) =
        session.Change<Agent>(value,Some "Agent","familyName",replacement |> Option.map (fun replacement -> Text(Model.required "familyName" replacement)),[],"Agent.setFamilyName")
    member _.setEmails(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(value,Some "Agent","emails",Some(Texts(List.ofSeq replacement)),[],"Agent.setEmails")
    member _.setAffiliations(value: Agent, replacement: seq<Organization>) =
        session.ChangeLinks<Agent>(value,Some "Agent","affiliations",replacement |> Seq.map box,"Agent.setAffiliations")
    member _.addAffiliation(value: Agent, target: Organization) = session.Collection<Agent>(value,Some "Agent","affiliations",target,true,"Agent.addAffiliation")
    member _.removeAffiliation(value: Agent, target: Organization) = session.Collection<Agent>(value,Some "Agent","affiliations",target,false,"Agent.removeAffiliation")
    member _.setIdentifiers(value: Agent, replacement: seq<string>) =
        session.Change<Agent>(value,Some "Agent","identifiers",Some(Texts(List.ofSeq replacement)),[],"Agent.setIdentifiers")
    member _.setAdditionalProperties(value: Agent, replacement: seq<Annotation>) =
        session.ChangeLinks<Agent>(value,Some "Agent","additionalProperties",replacement |> Seq.map box,"Agent.setAdditionalProperties")
    member _.addAdditionalProperty(value: Agent, target: Annotation) = session.Collection<Agent>(value,Some "Agent","additionalProperties",target,true,"Agent.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Agent, target: Annotation) = session.Collection<Agent>(value,Some "Agent","additionalProperties",target,false,"Agent.removeAdditionalProperty")
    member _.setJobTitles(value: Agent, replacement: seq<DefinedTerm>) =
        session.ChangeLinks<Agent>(value,Some "Agent","jobTitles",replacement |> Seq.map box,"Agent.setJobTitles")
    member _.addJobTitle(value: Agent, target: DefinedTerm) = session.Collection<Agent>(value,Some "Agent","jobTitles",target,true,"Agent.addJobTitle")
    member _.removeJobTitle(value: Agent, target: DefinedTerm) = session.Collection<Agent>(value,Some "Agent","jobTitles",target,false,"Agent.removeJobTitle")
