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
    inherit EntityObject("Annotation", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _value = value
    let mutable _unit = unit
    let mutable _nameTAN = nameTAN
    let mutable _valueTAN = valueTAN
    let mutable _unitTAN = unitTAN
    let mutable _instanceOf = instanceOf

    do base.ReserveProperties(["name"; "value"; "unit"; "nameTAN"; "valueTAN"; "unitTAN"; "instanceOf"])
    do base.SealInternal()
    /// Human-readable annotation key.
    member _.Name with get() = _name
    /// Optional text or number; zero and empty text are present values.
    member _.Value with get() = _value
    /// Human-readable unit of the value.
    member _.Unit with get() = _unit
    /// Ontology term URL for the key.
    member _.NameTAN with get() = _nameTAN
    /// Ontology term URL for the value.
    member _.ValueTAN with get() = _valueTAN
    /// Ontology term URL for the unit.
    member _.UnitTAN with get() = _unitTAN
    /// Formal parameter instantiated by this value; no backlink is maintained.
    member _.InstanceOf with get() = _instanceOf

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "value" -> _value <- unbox<AnnotationValue option> value
        | "unit" -> _unit <- unbox<string option> value
        | "nameTAN" -> _nameTAN <- unbox<string option> value
        | "valueTAN" -> _valueTAN <- unbox<string option> value
        | "unitTAN" -> _unitTAN <- unbox<string option> value
        | "instanceOf" -> _instanceOf <- unbox<FormalParameter option> value
        | _ -> base.AssignInternal(key,value)
    override _.ValidateInternal() =
        Construction.text "Name" _name |> ignore
#if FABLE_COMPILER
        _value |> Option.iter (fun item -> if not (Construction.isText (box item) || Construction.isNumber (box item)) then invalidArg "Value" "Invalid value alternative.")
#else
        _value |> Option.iter (Construction.required "Value" >> ignore)
#endif
        _unit |> Option.iter (Construction.text "Unit" >> ignore)
        _nameTAN |> Option.iter (Construction.text "NameTAN" >> ignore)
        _valueTAN |> Option.iter (Construction.text "ValueTAN" >> ignore)
        _unitTAN |> Option.iter (Construction.text "UnitTAN" >> ignore)
        _instanceOf |> Option.iter (fun item -> if not (box item :? FormalParameter) then invalidArg "InstanceOf" "Unexpected entity kind.")
        ()
    static member withName(value:Annotation,replacement:string):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withValue(value:Annotation,replacement:AnnotationValue):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("value",box(Some replacement)))
    static member clearValue(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("value",box(None:AnnotationValue option)))
    static member withUnit(value:Annotation,replacement:string):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("unit",box(Some replacement)))
    static member clearUnit(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("unit",box(None:string option)))
    static member withNameTAN(value:Annotation,replacement:string):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("nameTAN",box(Some replacement)))
    static member clearNameTAN(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("nameTAN",box(None:string option)))
    static member withValueTAN(value:Annotation,replacement:string):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("valueTAN",box(Some replacement)))
    static member clearValueTAN(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("valueTAN",box(None:string option)))
    static member withUnitTAN(value:Annotation,replacement:string):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("unitTAN",box(Some replacement)))
    static member clearUnitTAN(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("unitTAN",box(None:string option)))
    static member withInstanceOf(value:Annotation,replacement:FormalParameter):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("instanceOf",box(Some replacement)))
    static member clearInstanceOf(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("instanceOf",box(None:FormalParameter option)))
    static member withId(value:Annotation,replacement:string):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Annotation):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Annotation,replacement:seq<string>):Annotation = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Annotation,key:string,replacement:Entity):Annotation = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Annotation,key:string):Annotation = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))

/// A prospective recipe parameter slot with an optional annotation default.
and [<AttachMembers>] FormalParameter(?name: string, ?nameTAN: string, ?defaultValue: Annotation,
                                     ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("FormalParameter", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = name
    let mutable _nameTAN = nameTAN
    let mutable _defaultValue = defaultValue

    do base.ReserveProperties(["name"; "nameTAN"; "defaultValue"])
    do base.SealInternal()
    /// Human-readable parameter name.
    member _.Name with get() = _name
    /// Ontology term URL for the parameter key.
    member _.NameTAN with get() = _nameTAN
    /// Default annotation; no backlink is maintained.
    member _.DefaultValue with get() = _defaultValue

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string option> value
        | "nameTAN" -> _nameTAN <- unbox<string option> value
        | "defaultValue" -> _defaultValue <- unbox<Annotation option> value
        | _ -> base.AssignInternal(key,value)
    override _.ValidateInternal() =
        _name |> Option.iter (Construction.text "Name" >> ignore)
        _nameTAN |> Option.iter (Construction.text "NameTAN" >> ignore)
        _defaultValue |> Option.iter (fun item -> if not (box item :? Annotation) then invalidArg "DefaultValue" "Unexpected entity kind.")
        ()
    static member withName(value:FormalParameter,replacement:string):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box(Some replacement)))
    static member clearName(value:FormalParameter):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box(None:string option)))
    static member withNameTAN(value:FormalParameter,replacement:string):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("nameTAN",box(Some replacement)))
    static member clearNameTAN(value:FormalParameter):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("nameTAN",box(None:string option)))
    static member withDefaultValue(value:FormalParameter,replacement:Annotation):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("defaultValue",box(Some replacement)))
    static member clearDefaultValue(value:FormalParameter):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("defaultValue",box(None:Annotation option)))
    static member withId(value:FormalParameter,replacement:string):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:FormalParameter):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:FormalParameter,replacement:seq<string>):FormalParameter = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:FormalParameter,key:string,replacement:Entity):FormalParameter = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:FormalParameter,key:string):FormalParameter = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
