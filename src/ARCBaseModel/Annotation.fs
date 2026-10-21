namespace ARCBaseModel

open Fable.Core

/// Annotation values retain the distinction between text and numbers.
[<Erase; RequireQualifiedAccess>]
type AnnotationValue =
    | Text of string
    | Number of float

type internal AnnotationState =
    { Name: string
      Value: AnnotationValue option
      Unit: string option
      NameTAN: string option
      ValueTAN: string option
      UnitTAN: string option
      InstanceOf: FormalParameter option }

and internal FormalParameterState =
    { Name: string option
      NameTAN: string option
      DefaultValue: Annotation option }

/// A key, optional value, and optional unit, with optional ontology references.
and [<AttachMembers>] Annotation(name: string, ?value: AnnotationValue, ?unit: string,
                                 ?nameTAN: string, ?valueTAN: string, ?unitTAN: string, ?instanceOf: FormalParameter,
                                 ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Annotation", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: AnnotationState =
        { Name = Construction.required "name" name
          Value = value
          Unit = unit
          NameTAN = nameTAN
          ValueTAN = valueTAN
          UnitTAN = unitTAN
          InstanceOf = instanceOf }

    do base.ReserveProperties(["name"; "value"; "unit"; "nameTAN"; "valueTAN"; "unitTAN"; "instanceOf"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable annotation key.
    member _.Name = state.Name
    /// Optional text or number; zero and empty text are present values.
    member _.Value = state.Value
    /// Human-readable unit of the value.
    member _.Unit = state.Unit
    /// Ontology term URL for the key.
    member _.NameTAN = state.NameTAN
    /// Ontology term URL for the value.
    member _.ValueTAN = state.ValueTAN
    /// Ontology term URL for the unit.
    member _.UnitTAN = state.UnitTAN
    /// Formal parameter instantiated by this value; no backlink is maintained.
    member _.InstanceOf = state.InstanceOf
    static member withName(value: Annotation, replacement: string) : Annotation =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withValue(value: Annotation, replacement: AnnotationValue option) : Annotation =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Value = replacement })
    static member withUnit(value: Annotation, replacement: string option) : Annotation =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Unit = replacement })
    static member withNameTAN(value: Annotation, replacement: string option) : Annotation =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with NameTAN = replacement })
    static member withValueTAN(value: Annotation, replacement: string option) : Annotation =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with ValueTAN = replacement })
    static member withUnitTAN(value: Annotation, replacement: string option) : Annotation =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with UnitTAN = replacement })
    static member withInstanceOf(value: Annotation, replacement: FormalParameter option) : Annotation =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with InstanceOf = replacement })

/// A prospective recipe parameter slot with an optional annotation default.
and [<AttachMembers>] FormalParameter(?name: string, ?nameTAN: string, ?defaultValue: Annotation,
                                     ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("FormalParameter", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: FormalParameterState =
        { Name = name
          NameTAN = nameTAN
          DefaultValue = defaultValue }

    do base.ReserveProperties(["name"; "nameTAN"; "defaultValue"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable parameter name.
    member _.Name = state.Name
    /// Ontology term URL for the parameter key.
    member _.NameTAN = state.NameTAN
    /// Default annotation; no backlink is maintained.
    member _.DefaultValue = state.DefaultValue
    static member withName(value: FormalParameter, replacement: string option) : FormalParameter =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = replacement })
    static member withNameTAN(value: FormalParameter, replacement: string option) : FormalParameter =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with NameTAN = replacement })
    static member withDefaultValue(value: FormalParameter, replacement: Annotation option) : FormalParameter =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with DefaultValue = replacement })
