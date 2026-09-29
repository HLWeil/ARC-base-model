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
    let mutable _conformsTo = ResizeArray<string>(Construction.required "conformsTo" conformsTo)
    let mutable _identifiers = ResizeArray<string>(Construction.required "identifiers" identifiers)
    let mutable _title = title
    let mutable _description = description
    let mutable _license = license
    let mutable _datePublished = datePublished
    let mutable _dateCreated = dateCreated
    let mutable _dateModified = dateModified
    let mutable _hasParts = Construction.collection hasParts
    let mutable _dataFiles = Construction.collection dataFiles
    let mutable _agents = Construction.collection agents
    let mutable _citations = Construction.collection citations
    let mutable _processes = Construction.collection processes
    let mutable _descriptors = Construction.collection descriptors
    let mutable _additionalProperties = Construction.collection additionalProperties
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    do
        if _conformsTo.Count = 0 then
            invalidArg "conformsTo" "At least one base-profile discriminator is required."
        if not (_conformsTo |> Seq.exists (fun profile ->
            profile = "administrative" || profile = "process-provenance" || profile = "semantic-designation")) then
            invalidArg "conformsTo" "Include administrative, process-provenance, or semantic-designation."
        if _identifiers.Count = 0 then
            invalidArg "identifiers" "At least one dataset identifier is required."

    /// Fixed entity discriminator; profiles do not change the runtime entity type.
    member _.Type = "Dataset"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Open collection of profile declarations; validated only at construction.
    member _.ConformsTo with get() = _conformsTo and set(value) = _conformsTo <- Construction.copy value
    /// Dataset identifiers, separate from optional Id; validated only at construction.
    member _.Identifiers with get() = _identifiers and set(value) = _identifiers <- Construction.copy value
    /// Human-readable dataset title.
    member _.Title with get() = _title and set(value) = _title <- value
    /// Dataset description or abstract.
    member _.Description with get() = _description and set(value) = _description <- value
    /// License identifier, URL, or label.
    member _.License with get() = _license and set(value) = _license <- value
    /// Publication date represented as supplied text.
    member _.DatePublished with get() = _datePublished and set(value) = _datePublished <- value
    /// Creation date represented as supplied text.
    member _.DateCreated with get() = _dateCreated and set(value) = _dateCreated <- value
    /// Modification date represented as supplied text.
    member _.DateModified with get() = _dateModified and set(value) = _dateModified <- value
    /// Nested datasets with their own profile declarations; no parent links are maintained.
    member _.HasParts with get() = _hasParts and set(value) = _hasParts <- Construction.copy value
    /// Data files belonging to this dataset, retaining supplied references.
    member _.DataFiles with get() = _dataFiles and set(value) = _dataFiles <- Construction.copy value
    /// Agents associated with this dataset.
    member _.Agents with get() = _agents and set(value) = _agents <- Construction.copy value
    /// Publications associated with this dataset.
    member _.Citations with get() = _citations and set(value) = _citations <- Construction.copy value
    /// Processes contained in this dataset; no graph index or registration is performed.
    member _.Processes with get() = _processes and set(value) = _processes <- Construction.copy value
    /// Semantic descriptions associated with this dataset.
    member _.Descriptors with get() = _descriptors and set(value) = _descriptors <- Construction.copy value
    /// Extensible dataset metadata.
    member _.AdditionalProperties with get() = _additionalProperties and set(value) = _additionalProperties <- Construction.copy value
