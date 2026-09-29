namespace ARCBaseModel

open Fable.Core

/// A biological, chemical, or digital sample without inferred identity.
[<AttachMembers>]
type Sample(name: string, ?additionalProperties: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = Construction.required "name" name
    let mutable _additionalProperties = Construction.collection additionalProperties
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "Sample"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable sample name.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Extensible metadata. Replacement copies the container, retaining annotation references.
    member _.AdditionalProperties with get() = _additionalProperties and set(value) = _additionalProperties <- Construction.copy value

/// A file or file fragment; every nested Data object supplies its own path.
[<AttachMembers>]
type Data(path: string, ?selector: string, ?selectorFormat: string, ?encodingFormat: string,
          ?hasParts: seq<Data>, ?additionalProperties: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    let mutable _path = Construction.required "path" path
    let mutable _selector = selector
    let mutable _selectorFormat = selectorFormat
    let mutable _encodingFormat = encodingFormat
    let mutable _hasParts = Construction.collection hasParts
    let mutable _additionalProperties = Construction.collection additionalProperties
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "Data"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Path to the target file, unchanged by construction.
    member _.Path with get() = _path and set(value) = _path <- value
    /// Optional fragment selector.
    member _.Selector with get() = _selector and set(value) = _selector <- value
    /// URL describing the selector syntax.
    member _.SelectorFormat with get() = _selectorFormat and set(value) = _selectorFormat <- value
    /// MIME type of the data object or fragment.
    member _.EncodingFormat with get() = _encodingFormat and set(value) = _encodingFormat <- value
    /// Nested fragments, preserving supplied references and order.
    member _.HasParts with get() = _hasParts and set(value) = _hasParts <- Construction.copy value
    /// Extensible file, fragment, or content metadata.
    member _.AdditionalProperties with get() = _additionalProperties and set(value) = _additionalProperties <- Construction.copy value

/// A Sample or Data instance used by a process endpoint or descriptor.
[<Erase; RequireQualifiedAccess>]
type EntityReference =
    | Sample of Sample
    | Data of Data
