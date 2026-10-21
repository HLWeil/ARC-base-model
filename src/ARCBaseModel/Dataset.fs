namespace ARCBaseModel

open Fable.Core

module private DatasetChecks =
    let conformsTo (values: seq<string>) =
        let profiles = Construction.collection "conformsTo" values
        if Seq.isEmpty profiles then
            invalidArg "conformsTo" "At least one base-profile discriminator is required."
        if not (profiles |> Seq.exists (fun profile ->
            profile = "administrative" || profile = "process-provenance" || profile = "semantic-designation")) then
            invalidArg "conformsTo" "Include administrative, process-provenance, or semantic-designation."
        profiles

    let identifiers (values: seq<string>) =
        let items = Construction.collection "identifiers" values
        if Seq.isEmpty items then
            invalidArg "identifiers" "At least one dataset identifier is required."
        items

type internal DatasetState =
    { ConformsTo: seq<string>
      Identifiers: seq<string>
      Title: string option
      Description: string option
      License: string option
      DatePublished: string option
      DateCreated: string option
      DateModified: string option
      HasParts: seq<Dataset>
      DataFiles: seq<Data>
      Agents: seq<Agent>
      Citations: seq<ScholarlyArticle>
      Processes: seq<Process>
      Descriptors: seq<Descriptor>
      AdditionalProperties: seq<Annotation> }

/// A dataset combining administrative, process provenance, and semantic designation properties.
/// Each instance declares its own profiles, independently of parents and nested datasets.
and [<AttachMembers>] Dataset(conformsTo: seq<string>, identifiers: seq<string>, ?title: string, ?description: string,
                              ?license: string, ?datePublished: string, ?dateCreated: string, ?dateModified: string,
                              ?hasParts: seq<Dataset>, ?dataFiles: seq<Data>, ?agents: seq<Agent>, ?citations: seq<ScholarlyArticle>,
                              ?processes: seq<Process>, ?descriptors: seq<Descriptor>, ?additionalProperties: seq<Annotation>,
                              ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Dataset", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: DatasetState =
        { ConformsTo = DatasetChecks.conformsTo conformsTo
          Identifiers = DatasetChecks.identifiers identifiers
          Title = title
          Description = description
          License = license
          DatePublished = datePublished
          DateCreated = dateCreated
          DateModified = dateModified
          HasParts = Construction.optionalCollection "hasParts" hasParts
          DataFiles = Construction.optionalCollection "dataFiles" dataFiles
          Agents = Construction.optionalCollection "agents" agents
          Citations = Construction.optionalCollection "citations" citations
          Processes = Construction.optionalCollection "processes" processes
          Descriptors = Construction.optionalCollection "descriptors" descriptors
          AdditionalProperties = Construction.optionalCollection "additionalProperties" additionalProperties }

    do base.ReserveProperties(["conformsTo"; "identifiers"; "title"; "description"; "license"; "datePublished"; "dateCreated"; "dateModified"; "hasParts"; "dataFiles"; "agents"; "citations"; "processes"; "descriptors"; "additionalProperties"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Open collection of profile declarations, validated by the constructor and by withConformsTo.
    member _.ConformsTo = state.ConformsTo
    /// Dataset identifiers, separate from optional Id, validated by the constructor and by withIdentifiers.
    member _.Identifiers = state.Identifiers
    /// Human-readable dataset title.
    member _.Title = state.Title
    /// Dataset description or abstract.
    member _.Description = state.Description
    /// License identifier, URL, or label.
    member _.License = state.License
    /// Publication date represented as supplied text.
    member _.DatePublished = state.DatePublished
    /// Creation date represented as supplied text.
    member _.DateCreated = state.DateCreated
    /// Modification date represented as supplied text.
    member _.DateModified = state.DateModified
    /// Nested datasets with their own profile declarations; no parent links are maintained.
    member _.HasParts = state.HasParts
    /// Data files belonging to this dataset, retaining supplied references.
    member _.DataFiles = state.DataFiles
    /// Agents associated with this dataset.
    member _.Agents = state.Agents
    /// Publications associated with this dataset.
    member _.Citations = state.Citations
    /// Processes contained in this dataset; no graph index or registration is performed.
    member _.Processes = state.Processes
    /// Semantic descriptions associated with this dataset.
    member _.Descriptors = state.Descriptors
    /// Extensible dataset metadata.
    member _.AdditionalProperties = state.AdditionalProperties
    static member withConformsTo(value: Dataset, replacement: seq<string>) : Dataset =
        let profiles = DatasetChecks.conformsTo replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with ConformsTo = profiles })
    static member withIdentifiers(value: Dataset, replacement: seq<string>) : Dataset =
        let items = DatasetChecks.identifiers replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Identifiers = items })
    static member withTitle(value: Dataset, replacement: string option) : Dataset =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Title = replacement })
    static member withDescription(value: Dataset, replacement: string option) : Dataset =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Description = replacement })
    static member withLicense(value: Dataset, replacement: string option) : Dataset =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with License = replacement })
    static member withDatePublished(value: Dataset, replacement: string option) : Dataset =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with DatePublished = replacement })
    static member withDateCreated(value: Dataset, replacement: string option) : Dataset =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with DateCreated = replacement })
    static member withDateModified(value: Dataset, replacement: string option) : Dataset =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with DateModified = replacement })
    static member withHasParts(value: Dataset, replacement: seq<Dataset>) : Dataset =
        let items = Construction.collection "hasParts" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with HasParts = items })
    static member withDataFiles(value: Dataset, replacement: seq<Data>) : Dataset =
        let items = Construction.collection "dataFiles" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with DataFiles = items })
    static member withAgents(value: Dataset, replacement: seq<Agent>) : Dataset =
        let items = Construction.collection "agents" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Agents = items })
    static member withCitations(value: Dataset, replacement: seq<ScholarlyArticle>) : Dataset =
        let items = Construction.collection "citations" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Citations = items })
    static member withProcesses(value: Dataset, replacement: seq<Process>) : Dataset =
        let items = Construction.collection "processes" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Processes = items })
    static member withDescriptors(value: Dataset, replacement: seq<Descriptor>) : Dataset =
        let items = Construction.collection "descriptors" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Descriptors = items })
    static member withAdditionalProperties(value: Dataset, replacement: seq<Annotation>) : Dataset =
        let items = Construction.collection "additionalProperties" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with AdditionalProperties = items })
