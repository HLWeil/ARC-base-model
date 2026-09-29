namespace ARCBaseModel

open Fable.Core

/// Annotation values retain the distinction between text and numbers.
[<Erase; RequireQualifiedAccess>]
type AnnotationValue =
    | Text of string
    | Number of float

/// A key, optional value, and optional unit, with optional ontology references.
[<AttachMembers>]
type Annotation(name: string, ?value: AnnotationValue, ?unit: string,
                ?nameTAN: string, ?valueTAN: string, ?unitTAN: string, ?instanceOf: FormalParameter,
                ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = Construction.required "name" name
    let mutable _value = value
    let mutable _unit = unit
    let mutable _nameTAN = nameTAN
    let mutable _valueTAN = valueTAN
    let mutable _unitTAN = unitTAN
    let mutable _instanceOf = instanceOf
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "Annotation"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable annotation key.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Optional text or number; zero and empty text are present values.
    member _.Value with get() = _value and set(value) = _value <- value
    /// Human-readable unit of the value.
    member _.Unit with get() = _unit and set(value) = _unit <- value
    /// Ontology term URL for the key.
    member _.NameTAN with get() = _nameTAN and set(value) = _nameTAN <- value
    /// Ontology term URL for the value.
    member _.ValueTAN with get() = _valueTAN and set(value) = _valueTAN <- value
    /// Ontology term URL for the unit.
    member _.UnitTAN with get() = _unitTAN and set(value) = _unitTAN <- value
    /// Formal parameter instantiated by this value; no backlink is maintained.
    member _.InstanceOf with get() = _instanceOf and set(value) = _instanceOf <- value

/// A prospective recipe parameter slot with an optional annotation default.
and [<AttachMembers>] FormalParameter(?name: string, ?nameTAN: string, ?defaultValue: Annotation,
                                     ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = name
    let mutable _nameTAN = nameTAN
    let mutable _defaultValue = defaultValue
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "FormalParameter"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable parameter name.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Ontology term URL for the parameter key.
    member _.NameTAN with get() = _nameTAN and set(value) = _nameTAN <- value
    /// Default annotation; no backlink is maintained.
    member _.DefaultValue with get() = _defaultValue and set(value) = _defaultValue <- value
