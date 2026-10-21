namespace ARCBaseModel

open Fable.Core

type internal OrganizationState = { Name: string; Url: string option }

/// Organization associated with creation, curation, hosting, or publication.
[<AttachMembers>]
type Organization(name: string, ?url: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Organization", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: OrganizationState =
        { Name = Construction.required "name" name
          Url = url }

    do base.ReserveProperties(["name"; "url"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable organization name.
    member _.Name = state.Name
    /// Website or identifier URL.
    member _.Url = state.Url
    static member withName(value: Organization, replacement: string) : Organization =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withUrl(value: Organization, replacement: string option) : Organization =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Url = replacement })

type internal AgentState =
    { Name: string
      GivenName: string option
      FamilyName: string option
      Emails: seq<string>
      Affiliations: seq<Organization>
      Identifiers: seq<string>
      AdditionalProperties: seq<Annotation>
      JobTitles: seq<DefinedTerm> }

/// A person or agentic software system associated with a dataset or citation.
[<AttachMembers>]
type Agent(name: string, ?givenName: string, ?familyName: string, ?emails: seq<string>,
           ?affiliations: seq<Organization>, ?identifiers: seq<string>, ?additionalProperties: seq<Annotation>,
           ?jobTitles: seq<DefinedTerm>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Agent", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: AgentState =
        { Name = Construction.required "name" name
          GivenName = givenName
          FamilyName = familyName
          Emails = Construction.optionalCollection "emails" emails
          Affiliations = Construction.optionalCollection "affiliations" affiliations
          Identifiers = Construction.optionalCollection "identifiers" identifiers
          AdditionalProperties = Construction.optionalCollection "additionalProperties" additionalProperties
          JobTitles = Construction.optionalCollection "jobTitles" jobTitles }

    do base.ReserveProperties(["name"; "givenName"; "familyName"; "emails"; "affiliations"; "identifiers"; "additionalProperties"; "jobTitles"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Supplied display name; never inferred from other properties.
    member _.Name = state.Name
    /// Optional given name for a person.
    member _.GivenName = state.GivenName
    /// Optional family name for a person.
    member _.FamilyName = state.FamilyName
    /// Contact email addresses in supplied order.
    member _.Emails = state.Emails
    /// Affiliated organizations, retaining supplied references.
    member _.Affiliations = state.Affiliations
    /// Identifiers by which this agent is known; unrelated to automatic identity.
    member _.Identifiers = state.Identifiers
    /// Extensible agent metadata.
    member _.AdditionalProperties = state.AdditionalProperties
    /// Occupation, role, or function terms.
    member _.JobTitles = state.JobTitles
    static member withName(value: Agent, replacement: string) : Agent =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withGivenName(value: Agent, replacement: string option) : Agent =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with GivenName = replacement })
    static member withFamilyName(value: Agent, replacement: string option) : Agent =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with FamilyName = replacement })
    static member withEmails(value: Agent, replacement: seq<string>) : Agent =
        let items = Construction.collection "emails" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Emails = items })
    static member withAffiliations(value: Agent, replacement: seq<Organization>) : Agent =
        let items = Construction.collection "affiliations" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Affiliations = items })
    static member withIdentifiers(value: Agent, replacement: seq<string>) : Agent =
        let items = Construction.collection "identifiers" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Identifiers = items })
    static member withAdditionalProperties(value: Agent, replacement: seq<Annotation>) : Agent =
        let items = Construction.collection "additionalProperties" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with AdditionalProperties = items })
    static member withJobTitles(value: Agent, replacement: seq<DefinedTerm>) : Agent =
        let items = Construction.collection "jobTitles" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with JobTitles = items })

type internal ScholarlyArticleState =
    { Headline: string
      Identifiers: seq<string>
      Authors: seq<Agent>
      CreativeWorkStatus: DefinedTerm option
      AdditionalProperties: seq<Annotation> }

/// A scholarly publication associated with a dataset.
[<AttachMembers>]
type ScholarlyArticle(headline: string, ?identifiers: seq<string>, ?authors: seq<Agent>,
                      ?creativeWorkStatus: DefinedTerm, ?additionalProperties: seq<Annotation>,
                      ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("ScholarlyArticle", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: ScholarlyArticleState =
        { Headline = Construction.required "headline" headline
          Identifiers = Construction.optionalCollection "identifiers" identifiers
          Authors = Construction.optionalCollection "authors" authors
          CreativeWorkStatus = creativeWorkStatus
          AdditionalProperties = Construction.optionalCollection "additionalProperties" additionalProperties }

    do base.ReserveProperties(["headline"; "identifiers"; "authors"; "creativeWorkStatus"; "additionalProperties"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable article title.
    member _.Headline = state.Headline
    /// DOI, PubMed ID, repository identifier, or other identifying strings.
    member _.Identifiers = state.Identifiers
    /// Credited authors, retaining supplied ordering, duplicates, and references.
    member _.Authors = state.Authors
    /// Publication lifecycle status as a controlled vocabulary term.
    member _.CreativeWorkStatus = state.CreativeWorkStatus
    /// Extensible article metadata.
    member _.AdditionalProperties = state.AdditionalProperties
    static member withHeadline(value: ScholarlyArticle, replacement: string) : ScholarlyArticle =
        let headline = Construction.required "headline" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Headline = headline })
    static member withIdentifiers(value: ScholarlyArticle, replacement: seq<string>) : ScholarlyArticle =
        let items = Construction.collection "identifiers" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Identifiers = items })
    static member withAuthors(value: ScholarlyArticle, replacement: seq<Agent>) : ScholarlyArticle =
        let items = Construction.collection "authors" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Authors = items })
    static member withCreativeWorkStatus(value: ScholarlyArticle, replacement: DefinedTerm option) : ScholarlyArticle =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with CreativeWorkStatus = replacement })
    static member withAdditionalProperties(value: ScholarlyArticle, replacement: seq<Annotation>) : ScholarlyArticle =
        let items = Construction.collection "additionalProperties" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with AdditionalProperties = items })
