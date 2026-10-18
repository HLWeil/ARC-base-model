namespace ARCBaseModel

open Fable.Core

/// Organization associated with creation, curation, hosting, or publication.
[<AttachMembers>]
type Organization(name: string, ?url: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Organization", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _url = url

    do base.ReserveProperties(["name"; "url"])
    do base.SealInternal()
    /// Human-readable organization name.
    member _.Name with get() = _name
    /// Website or identifier URL.
    member _.Url with get() = _url

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "url" -> _url <- unbox<string option> value
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        Construction.text "Name" _name |> ignore
        _url |> Option.iter (Construction.text "Url" >> ignore)
        ()
    static member withName(value:Organization,replacement:string):Organization = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withUrl(value:Organization,replacement:string):Organization = EntityObject.Copy(value,fun copy -> copy.AssignInternal("url",box(Some replacement)))
    static member clearUrl(value:Organization):Organization = EntityObject.Copy(value,fun copy -> copy.AssignInternal("url",box(None:string option)))
    static member withId(value:Organization,replacement:string):Organization = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Organization):Organization = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Organization,replacement:seq<string>):Organization = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Organization,key:string,replacement:Entity):Organization = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Organization,key:string):Organization = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))

/// A person or agentic software system associated with a dataset or citation.
[<AttachMembers>]
type Agent(name: string, ?givenName: string, ?familyName: string, ?emails: seq<string>,
           ?affiliations: seq<Organization>, ?identifiers: seq<string>, ?additionalProperties: seq<Annotation>,
           ?jobTitles: seq<DefinedTerm>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Agent", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _givenName = givenName
    let mutable _familyName = familyName
    let mutable _emails = Construction.optionalCollection emails
    let mutable _affiliations = Construction.optionalCollection affiliations
    let mutable _identifiers = Construction.optionalCollection identifiers
    let mutable _additionalProperties = Construction.optionalCollection additionalProperties
    let mutable _jobTitles = Construction.optionalCollection jobTitles

    do base.ReserveProperties(["name"; "givenName"; "familyName"; "emails"; "affiliations"; "identifiers"; "additionalProperties"; "jobTitles"])
    do base.SealInternal()
    /// Supplied display name; never inferred from other properties.
    member _.Name with get() = _name
    /// Optional given name for a person.
    member _.GivenName with get() = _givenName
    /// Optional family name for a person.
    member _.FamilyName with get() = _familyName
    /// Contact email addresses in supplied order.
    member _.Emails with get() = _emails
    /// Affiliated organizations, retaining supplied references.
    member _.Affiliations with get() = _affiliations
    /// Identifiers by which this agent is known; unrelated to automatic identity.
    member _.Identifiers with get() = _identifiers
    /// Extensible agent metadata.
    member _.AdditionalProperties with get() = _additionalProperties
    /// Occupation, role, or function terms.
    member _.JobTitles with get() = _jobTitles

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "givenName" -> _givenName <- unbox<string option> value
        | "familyName" -> _familyName <- unbox<string option> value
        | "emails" -> _emails <- Construction.collection (unbox<seq<string>> value)
        | "affiliations" -> _affiliations <- Construction.collection (unbox<seq<Organization>> value)
        | "identifiers" -> _identifiers <- Construction.collection (unbox<seq<string>> value)
        | "additionalProperties" -> _additionalProperties <- Construction.collection (unbox<seq<Annotation>> value)
        | "jobTitles" -> _jobTitles <- Construction.collection (unbox<seq<DefinedTerm>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        Construction.text "Name" _name |> ignore
        _givenName |> Option.iter (Construction.text "GivenName" >> ignore)
        _familyName |> Option.iter (Construction.text "FamilyName" >> ignore)
        this.ValidateOwned("emails",box _emails,fun () -> _emails |> Seq.iter (Construction.text "Emails" >> ignore))
        this.ValidateOwned("affiliations",box _affiliations,fun () -> for item in _affiliations do if not (box item :? Organization) then invalidArg "Affiliations" "Unexpected entity kind.")
        this.ValidateOwned("identifiers",box _identifiers,fun () -> _identifiers |> Seq.iter (Construction.text "Identifiers" >> ignore))
        this.ValidateOwned("additionalProperties",box _additionalProperties,fun () -> for item in _additionalProperties do if not (box item :? Annotation) then invalidArg "AdditionalProperties" "Unexpected entity kind.")
        this.ValidateOwned("jobTitles",box _jobTitles,fun () -> for item in _jobTitles do if not (box item :? DefinedTerm) then invalidArg "JobTitles" "Unexpected entity kind.")
        ()
    static member withName(value:Agent,replacement:string):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withGivenName(value:Agent,replacement:string):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("givenName",box(Some replacement)))
    static member clearGivenName(value:Agent):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("givenName",box(None:string option)))
    static member withFamilyName(value:Agent,replacement:string):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("familyName",box(Some replacement)))
    static member clearFamilyName(value:Agent):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("familyName",box(None:string option)))
    static member withEmails(value:Agent,replacement:seq<string>):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("emails",box replacement))
    static member withAffiliations(value:Agent,replacement:seq<Organization>):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("affiliations",box replacement))
    static member withIdentifiers(value:Agent,replacement:seq<string>):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifiers",box replacement))
    static member withAdditionalProperties(value:Agent,replacement:seq<Annotation>):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalProperties",box replacement))
    static member withJobTitles(value:Agent,replacement:seq<DefinedTerm>):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("jobTitles",box replacement))
    static member withId(value:Agent,replacement:string):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Agent):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Agent,replacement:seq<string>):Agent = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Agent,key:string,replacement:Entity):Agent = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Agent,key:string):Agent = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))

/// A scholarly publication associated with a dataset.
[<AttachMembers>]
type ScholarlyArticle(headline: string, ?identifiers: seq<string>, ?authors: seq<Agent>,
                      ?creativeWorkStatus: DefinedTerm, ?additionalProperties: seq<Annotation>,
                      ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("ScholarlyArticle", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _headline = Construction.required "headline" headline
    let mutable _identifiers = Construction.optionalCollection identifiers
    let mutable _authors = Construction.optionalCollection authors
    let mutable _creativeWorkStatus = creativeWorkStatus
    let mutable _additionalProperties = Construction.optionalCollection additionalProperties

    do base.ReserveProperties(["headline"; "identifiers"; "authors"; "creativeWorkStatus"; "additionalProperties"])
    do base.SealInternal()
    /// Human-readable article title.
    member _.Headline with get() = _headline
    /// DOI, PubMed ID, repository identifier, or other identifying strings.
    member _.Identifiers with get() = _identifiers
    /// Credited authors, retaining supplied ordering, duplicates, and references.
    member _.Authors with get() = _authors
    /// Publication lifecycle status as a controlled vocabulary term.
    member _.CreativeWorkStatus with get() = _creativeWorkStatus
    /// Extensible article metadata.
    member _.AdditionalProperties with get() = _additionalProperties

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "headline" -> _headline <- unbox<string> value
        | "identifiers" -> _identifiers <- Construction.collection (unbox<seq<string>> value)
        | "authors" -> _authors <- Construction.collection (unbox<seq<Agent>> value)
        | "creativeWorkStatus" -> _creativeWorkStatus <- unbox<DefinedTerm option> value
        | "additionalProperties" -> _additionalProperties <- Construction.collection (unbox<seq<Annotation>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        Construction.text "Headline" _headline |> ignore
        this.ValidateOwned("identifiers",box _identifiers,fun () -> _identifiers |> Seq.iter (Construction.text "Identifiers" >> ignore))
        this.ValidateOwned("authors",box _authors,fun () -> for item in _authors do if not (box item :? Agent) then invalidArg "Authors" "Unexpected entity kind.")
        _creativeWorkStatus |> Option.iter (fun item -> if not (box item :? DefinedTerm) then invalidArg "CreativeWorkStatus" "Unexpected entity kind.")
        this.ValidateOwned("additionalProperties",box _additionalProperties,fun () -> for item in _additionalProperties do if not (box item :? Annotation) then invalidArg "AdditionalProperties" "Unexpected entity kind.")
        ()
    static member withHeadline(value:ScholarlyArticle,replacement:string):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("headline",box replacement))
    static member withIdentifiers(value:ScholarlyArticle,replacement:seq<string>):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifiers",box replacement))
    static member withAuthors(value:ScholarlyArticle,replacement:seq<Agent>):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("authors",box replacement))
    static member withCreativeWorkStatus(value:ScholarlyArticle,replacement:DefinedTerm):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("creativeWorkStatus",box(Some replacement)))
    static member clearCreativeWorkStatus(value:ScholarlyArticle):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("creativeWorkStatus",box(None:DefinedTerm option)))
    static member withAdditionalProperties(value:ScholarlyArticle,replacement:seq<Annotation>):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalProperties",box replacement))
    static member withId(value:ScholarlyArticle,replacement:string):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:ScholarlyArticle):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:ScholarlyArticle,replacement:seq<string>):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:ScholarlyArticle,key:string,replacement:Entity):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:ScholarlyArticle,key:string):ScholarlyArticle = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
