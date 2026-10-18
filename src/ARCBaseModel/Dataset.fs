namespace ARCBaseModel

open Fable.Core

/// A dataset combining administrative, process provenance, and semantic designation properties.
/// Each instance declares its own profiles, independently of parents and nested datasets.
[<AttachMembers>]
type Dataset(conformsTo: seq<string>, identifiers: seq<string>, ?title: string, ?description: string,
             ?license: string, ?datePublished: string, ?dateCreated: string, ?dateModified: string,
             ?hasParts: seq<Dataset>, ?dataFiles: seq<Data>, ?agents: seq<Agent>, ?citations: seq<ScholarlyArticle>,
             ?processes: seq<Process>, ?descriptors: seq<Descriptor>, ?additionalProperties: seq<Annotation>,
             ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Dataset", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _conformsTo = Construction.collection (Construction.required "conformsTo" conformsTo)
    let mutable _identifiers = Construction.collection (Construction.required "identifiers" identifiers)
    let mutable _title = title
    let mutable _description = description
    let mutable _license = license
    let mutable _datePublished = datePublished
    let mutable _dateCreated = dateCreated
    let mutable _dateModified = dateModified
    let mutable _hasParts = Construction.optionalCollection hasParts
    let mutable _dataFiles = Construction.optionalCollection dataFiles
    let mutable _agents = Construction.optionalCollection agents
    let mutable _citations = Construction.optionalCollection citations
    let mutable _processes = Construction.optionalCollection processes
    let mutable _descriptors = Construction.optionalCollection descriptors
    let mutable _additionalProperties = Construction.optionalCollection additionalProperties

    do base.ReserveProperties(["conformsTo"; "identifiers"; "title"; "description"; "license"; "datePublished"; "dateCreated"; "dateModified"; "hasParts"; "dataFiles"; "agents"; "citations"; "processes"; "descriptors"; "additionalProperties"])
    do base.SealInternal()
    /// Open collection of profile declarations, validated when supplied or replaced.
    member _.ConformsTo with get() = _conformsTo
    /// Dataset identifiers, separate from optional Id, validated when supplied or replaced.
    member _.Identifiers with get() = _identifiers
    /// Human-readable dataset title.
    member _.Title with get() = _title
    /// Dataset description or abstract.
    member _.Description with get() = _description
    /// License identifier, URL, or label.
    member _.License with get() = _license
    /// Publication date represented as supplied text.
    member _.DatePublished with get() = _datePublished
    /// Creation date represented as supplied text.
    member _.DateCreated with get() = _dateCreated
    /// Modification date represented as supplied text.
    member _.DateModified with get() = _dateModified
    /// Nested datasets with their own profile declarations; no parent links are maintained.
    member _.HasParts with get() = _hasParts
    /// Data files belonging to this dataset, retaining supplied references.
    member _.DataFiles with get() = _dataFiles
    /// Agents associated with this dataset.
    member _.Agents with get() = _agents
    /// Publications associated with this dataset.
    member _.Citations with get() = _citations
    /// Processes contained in this dataset; no graph index or registration is performed.
    member _.Processes with get() = _processes
    /// Semantic descriptions associated with this dataset.
    member _.Descriptors with get() = _descriptors
    /// Extensible dataset metadata.
    member _.AdditionalProperties with get() = _additionalProperties

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "conformsTo" -> _conformsTo <- Construction.collection (unbox<seq<string>> value)
        | "identifiers" -> _identifiers <- Construction.collection (unbox<seq<string>> value)
        | "title" -> _title <- unbox<string option> value
        | "description" -> _description <- unbox<string option> value
        | "license" -> _license <- unbox<string option> value
        | "datePublished" -> _datePublished <- unbox<string option> value
        | "dateCreated" -> _dateCreated <- unbox<string option> value
        | "dateModified" -> _dateModified <- unbox<string option> value
        | "hasParts" -> _hasParts <- Construction.collection (unbox<seq<Dataset>> value)
        | "dataFiles" -> _dataFiles <- Construction.collection (unbox<seq<Data>> value)
        | "agents" -> _agents <- Construction.collection (unbox<seq<Agent>> value)
        | "citations" -> _citations <- Construction.collection (unbox<seq<ScholarlyArticle>> value)
        | "processes" -> _processes <- Construction.collection (unbox<seq<Process>> value)
        | "descriptors" -> _descriptors <- Construction.collection (unbox<seq<Descriptor>> value)
        | "additionalProperties" -> _additionalProperties <- Construction.collection (unbox<seq<Annotation>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        this.ValidateOwned("conformsTo",box _conformsTo,fun () ->
            _conformsTo |> Seq.iter (Construction.text "ConformsTo" >> ignore)
            if _conformsTo.Count=0 || not (Seq.exists (fun p -> p="administrative" || p="process-provenance" || p="semantic-designation") _conformsTo) then invalidArg "conformsTo" "At least one base profile is required.")
        this.ValidateOwned("identifiers",box _identifiers,fun () ->
            _identifiers |> Seq.iter (Construction.text "Identifiers" >> ignore)
            if _identifiers.Count=0 then invalidArg "identifiers" "At least one identifier is required.")
        _title |> Option.iter (Construction.text "Title" >> ignore)
        _description |> Option.iter (Construction.text "Description" >> ignore)
        _license |> Option.iter (Construction.text "License" >> ignore)
        _datePublished |> Option.iter (Construction.text "DatePublished" >> ignore)
        _dateCreated |> Option.iter (Construction.text "DateCreated" >> ignore)
        _dateModified |> Option.iter (Construction.text "DateModified" >> ignore)
        this.ValidateOwned("hasParts",box _hasParts,fun () -> for item in _hasParts do if not (box item :? Dataset) then invalidArg "HasParts" "Unexpected entity kind.")
        this.ValidateOwned("dataFiles",box _dataFiles,fun () -> for item in _dataFiles do if not (box item :? Data) then invalidArg "DataFiles" "Unexpected entity kind.")
        this.ValidateOwned("agents",box _agents,fun () -> for item in _agents do if not (box item :? Agent) then invalidArg "Agents" "Unexpected entity kind.")
        this.ValidateOwned("citations",box _citations,fun () -> for item in _citations do if not (box item :? ScholarlyArticle) then invalidArg "Citations" "Unexpected entity kind.")
        this.ValidateOwned("processes",box _processes,fun () -> for item in _processes do if not (box item :? Process) then invalidArg "Processes" "Unexpected entity kind.")
        this.ValidateOwned("descriptors",box _descriptors,fun () -> for item in _descriptors do if not (box item :? Descriptor) then invalidArg "Descriptors" "Unexpected entity kind.")
        this.ValidateOwned("additionalProperties",box _additionalProperties,fun () -> for item in _additionalProperties do if not (box item :? Annotation) then invalidArg "AdditionalProperties" "Unexpected entity kind.")
        ()
    static member withConformsTo(value:Dataset,replacement:seq<string>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("conformsTo",box replacement))
    static member withIdentifiers(value:Dataset,replacement:seq<string>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("identifiers",box replacement))
    static member withTitle(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("title",box(Some replacement)))
    static member clearTitle(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("title",box(None:string option)))
    static member withDescription(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("description",box(Some replacement)))
    static member clearDescription(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("description",box(None:string option)))
    static member withLicense(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("license",box(Some replacement)))
    static member clearLicense(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("license",box(None:string option)))
    static member withDatePublished(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("datePublished",box(Some replacement)))
    static member clearDatePublished(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("datePublished",box(None:string option)))
    static member withDateCreated(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("dateCreated",box(Some replacement)))
    static member clearDateCreated(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("dateCreated",box(None:string option)))
    static member withDateModified(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("dateModified",box(Some replacement)))
    static member clearDateModified(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("dateModified",box(None:string option)))
    static member withHasParts(value:Dataset,replacement:seq<Dataset>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("hasParts",box replacement))
    static member withDataFiles(value:Dataset,replacement:seq<Data>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("dataFiles",box replacement))
    static member withAgents(value:Dataset,replacement:seq<Agent>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("agents",box replacement))
    static member withCitations(value:Dataset,replacement:seq<ScholarlyArticle>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("citations",box replacement))
    static member withProcesses(value:Dataset,replacement:seq<Process>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("processes",box replacement))
    static member withDescriptors(value:Dataset,replacement:seq<Descriptor>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("descriptors",box replacement))
    static member withAdditionalProperties(value:Dataset,replacement:seq<Annotation>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalProperties",box replacement))
    static member withId(value:Dataset,replacement:string):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Dataset):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Dataset,replacement:seq<string>):Dataset = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Dataset,key:string,replacement:Entity):Dataset = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Dataset,key:string):Dataset = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
