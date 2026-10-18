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
    let mutable _annotations = Construction.optionalCollection annotations

    do base.ReserveProperties(["describes"; "annotations"])
    do base.SealInternal()
    /// Sample or Data described by these assertions.
    member _.Describes with get() = _describes
    /// Semantic assertions, preserving order, duplicates, and references.
    member _.Annotations with get() = _annotations

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "describes" -> _describes <- if isNull value then Unchecked.defaultof<EntityReference> else unbox<EntityReference> value
        | "annotations" -> _annotations <- Construction.collection (unbox<seq<Annotation>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
#if FABLE_COMPILER
        let item = _describes
        if not (box item :? Sample || box item :? Data) then invalidArg "Describes" "Invalid value alternative."
#else
        Construction.required "Describes" _describes |> ignore
#endif
        this.ValidateOwned("annotations",box _annotations,fun () -> for item in _annotations do if not (box item :? Annotation) then invalidArg "Annotations" "Unexpected entity kind.")
        ()
    static member withDescribes(value:Descriptor,replacement:EntityReference):Descriptor = EntityObject.Copy(value,fun copy -> copy.AssignInternal("describes",box replacement))
    static member withAnnotations(value:Descriptor,replacement:seq<Annotation>):Descriptor = EntityObject.Copy(value,fun copy -> copy.AssignInternal("annotations",box replacement))
    static member withId(value:Descriptor,replacement:string):Descriptor = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Descriptor):Descriptor = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Descriptor,replacement:seq<string>):Descriptor = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Descriptor,key:string,replacement:Entity):Descriptor = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Descriptor,key:string):Descriptor = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
