namespace ARCBaseModel

open Fable.Core

/// Semantic assertions about one Sample or Data instance.
[<AttachMembers>]
type Descriptor(describes: EntityReference, ?annotations: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Descriptor", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _describes =
        let reference = Construction.required "describes" describes
        match reference with
        | EntityReference.Sample sample -> Construction.required "describes" sample |> ignore
        | EntityReference.Data data -> Construction.required "describes" data |> ignore
        reference
    let mutable _annotations = Construction.collection annotations

    do base.EntityProperties.Reserve(["describes"; "annotations"])
    /// Sample or Data described by these assertions.
    member _.Describes with get() = _describes and set(value) = _describes <- value
    /// Semantic assertions, preserving order, duplicates, and references.
    member _.Annotations with get() = _annotations and set(value) = _annotations <- Construction.copy value
