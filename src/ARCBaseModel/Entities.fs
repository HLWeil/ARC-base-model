namespace ARCBaseModel

open Fable.Core

type internal SampleState = { Name: string; AdditionalProperties: seq<Annotation> }

/// A biological, chemical, or digital sample without inferred identity.
[<AttachMembers>]
type Sample(name: string, ?additionalProperties: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Sample", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: SampleState =
        { Name = Construction.required "name" name
          AdditionalProperties = Construction.optionalCollection "additionalProperties" additionalProperties }

    do base.ReserveProperties(["name"; "additionalProperties"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable sample name.
    member _.Name = state.Name
    /// Extensible metadata, preserving order, duplicates and references.
    member _.AdditionalProperties = state.AdditionalProperties
    static member withName(value: Sample, replacement: string) : Sample =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withAdditionalProperties(value: Sample, replacement: seq<Annotation>) : Sample =
        let items = Construction.collection "additionalProperties" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with AdditionalProperties = items })

type internal DataState =
    { Path: string
      Selector: string option
      SelectorFormat: string option
      EncodingFormat: string option
      HasParts: seq<Data>
      AdditionalProperties: seq<Annotation> }

/// A file or file fragment; every nested Data object supplies its own path.
and [<AttachMembers>] Data(path: string, ?selector: string, ?selectorFormat: string, ?encodingFormat: string,
                           ?hasParts: seq<Data>, ?additionalProperties: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Data", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: DataState =
        { Path = Construction.required "path" path
          Selector = selector
          SelectorFormat = selectorFormat
          EncodingFormat = encodingFormat
          HasParts = Construction.optionalCollection "hasParts" hasParts
          AdditionalProperties = Construction.optionalCollection "additionalProperties" additionalProperties }

    do base.ReserveProperties(["path"; "selector"; "selectorFormat"; "encodingFormat"; "hasParts"; "additionalProperties"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Path to the target file, unchanged by construction.
    member _.Path = state.Path
    /// Optional fragment selector.
    member _.Selector = state.Selector
    /// URL describing the selector syntax.
    member _.SelectorFormat = state.SelectorFormat
    /// MIME type of the data object or fragment.
    member _.EncodingFormat = state.EncodingFormat
    /// Nested fragments, preserving supplied references and order.
    member _.HasParts = state.HasParts
    /// Extensible file, fragment, or content metadata.
    member _.AdditionalProperties = state.AdditionalProperties
    static member withPath(value: Data, replacement: string) : Data =
        let path = Construction.required "path" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Path = path })
    static member withSelector(value: Data, replacement: string option) : Data =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Selector = replacement })
    static member withSelectorFormat(value: Data, replacement: string option) : Data =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with SelectorFormat = replacement })
    static member withEncodingFormat(value: Data, replacement: string option) : Data =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with EncodingFormat = replacement })
    static member withHasParts(value: Data, replacement: seq<Data>) : Data =
        let items = Construction.collection "hasParts" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with HasParts = items })
    static member withAdditionalProperties(value: Data, replacement: seq<Annotation>) : Data =
        let items = Construction.collection "additionalProperties" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with AdditionalProperties = items })

/// A Sample or Data instance used by a process endpoint or descriptor.
[<Erase; RequireQualifiedAccess>]
type EntityReference =
    | Sample of Sample
    | Data of Data
