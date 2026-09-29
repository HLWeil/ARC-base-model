namespace ARCBaseModel

open Fable.Core

/// Semantic assertions about one Sample or Data instance.
[<AttachMembers>]
type Descriptor(describes: EntityReference, ?annotations: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    let mutable _describes =
        let reference = Construction.required "describes" describes
        match reference with
        | EntityReference.Sample sample -> Construction.required "describes" sample |> ignore
        | EntityReference.Data data -> Construction.required "describes" data |> ignore
        reference
    let mutable _annotations = Construction.collection annotations
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "Descriptor"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Sample or Data described by these assertions.
    member _.Describes with get() = _describes and set(value) = _describes <- value
    /// Semantic assertions, preserving order, duplicates, and references.
    member _.Annotations with get() = _annotations and set(value) = _annotations <- Construction.copy value
