namespace ARCBaseModel

open Fable.Core

type internal DescriptorState = { Describes: EntityReference; Annotations: seq<Annotation> }

module private DescriptorChecks =
    let describes (describes: EntityReference) =
        let reference = Construction.required "describes" describes
        match reference with
        | EntityReference.Sample sample -> Construction.required "describes" sample |> ignore
        | EntityReference.Data data -> Construction.required "describes" data |> ignore
        reference

/// Semantic assertions about one Sample or Data instance.
[<AttachMembers>]
type Descriptor(describes: EntityReference, ?annotations: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Descriptor", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: DescriptorState =
        { Describes = DescriptorChecks.describes describes
          Annotations = Construction.optionalCollection "annotations" annotations }

    do base.ReserveProperties(["describes"; "annotations"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Sample or Data described by these assertions.
    member _.Describes = state.Describes
    /// Semantic assertions, preserving order, duplicates, and references.
    member _.Annotations = state.Annotations
    static member withDescribes(value: Descriptor, replacement: EntityReference) : Descriptor =
        let reference = DescriptorChecks.describes replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Describes = reference })
    static member withAnnotations(value: Descriptor, replacement: seq<Annotation>) : Descriptor =
        let items = Construction.collection "annotations" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Annotations = items })
