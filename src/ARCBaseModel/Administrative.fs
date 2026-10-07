namespace ARCBaseModel

open Fable.Core

/// Organization associated with creation, curation, hosting, or publication.
[<AttachMembers>]
type Organization(name: string, ?url: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Organization", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _url = url

    do base.EntityProperties.Reserve(["name"; "url"])
    /// Human-readable organization name.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Website or identifier URL.
    member _.Url with get() = _url and set(value) = _url <- value

/// A person or agentic software system associated with a dataset or citation.
[<AttachMembers>]
type Agent(name: string, ?givenName: string, ?familyName: string, ?emails: seq<string>,
           ?affiliations: seq<Organization>, ?identifiers: seq<string>, ?additionalProperties: seq<Annotation>,
           ?jobTitles: seq<DefinedTerm>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Agent", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _givenName = givenName
    let mutable _familyName = familyName
    let mutable _emails = Construction.collection emails
    let mutable _affiliations = Construction.collection affiliations
    let mutable _identifiers = Construction.collection identifiers
    let mutable _additionalProperties = Construction.collection additionalProperties
    let mutable _jobTitles = Construction.collection jobTitles

    do base.EntityProperties.Reserve(["name"; "givenName"; "familyName"; "emails"; "affiliations"; "identifiers"; "additionalProperties"; "jobTitles"])
    /// Supplied display name; never inferred from other properties.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Optional given name for a person.
    member _.GivenName with get() = _givenName and set(value) = _givenName <- value
    /// Optional family name for a person.
    member _.FamilyName with get() = _familyName and set(value) = _familyName <- value
    /// Contact email addresses in supplied order.
    member _.Emails with get() = _emails and set(value) = _emails <- Construction.copy value
    /// Affiliated organizations, retaining supplied references.
    member _.Affiliations with get() = _affiliations and set(value) = _affiliations <- Construction.copy value
    /// Identifiers by which this agent is known; unrelated to automatic identity.
    member _.Identifiers with get() = _identifiers and set(value) = _identifiers <- Construction.copy value
    /// Extensible agent metadata.
    member _.AdditionalProperties with get() = _additionalProperties and set(value) = _additionalProperties <- Construction.copy value
    /// Occupation, role, or function terms.
    member _.JobTitles with get() = _jobTitles and set(value) = _jobTitles <- Construction.copy value

/// A scholarly publication associated with a dataset.
[<AttachMembers>]
type ScholarlyArticle(headline: string, ?identifiers: seq<string>, ?authors: seq<Agent>,
                      ?creativeWorkStatus: DefinedTerm, ?additionalProperties: seq<Annotation>,
                      ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("ScholarlyArticle", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _headline = Construction.required "headline" headline
    let mutable _identifiers = Construction.collection identifiers
    let mutable _authors = Construction.collection authors
    let mutable _creativeWorkStatus = creativeWorkStatus
    let mutable _additionalProperties = Construction.collection additionalProperties

    do base.EntityProperties.Reserve(["headline"; "identifiers"; "authors"; "creativeWorkStatus"; "additionalProperties"])
    /// Human-readable article title.
    member _.Headline with get() = _headline and set(value) = _headline <- value
    /// DOI, PubMed ID, repository identifier, or other identifying strings.
    member _.Identifiers with get() = _identifiers and set(value) = _identifiers <- Construction.copy value
    /// Credited authors, retaining supplied ordering, duplicates, and references.
    member _.Authors with get() = _authors and set(value) = _authors <- Construction.copy value
    /// Publication lifecycle status as a controlled vocabulary term.
    member _.CreativeWorkStatus with get() = _creativeWorkStatus and set(value) = _creativeWorkStatus <- value
    /// Extensible article metadata.
    member _.AdditionalProperties with get() = _additionalProperties and set(value) = _additionalProperties <- Construction.copy value
