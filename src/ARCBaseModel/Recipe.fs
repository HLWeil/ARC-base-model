namespace ARCBaseModel

open Fable.Core

/// Recipe classification represented by text or a controlled vocabulary term.
[<Erase; RequireQualifiedAccess>]
type RecipeIntendedUse =
    | Text of string
    | Term of DefinedTerm

/// Description of a planned procedure and its prospective parameters.
[<AttachMembers>]
type Recipe(?name: string, ?parameters: seq<FormalParameter>, ?description: string,
            ?intendedUse: RecipeIntendedUse, ?additionalProperties: seq<Annotation>, ?components: seq<Annotation>,
            ?version: string, ?url: string, ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = name
    let mutable _parameters = Construction.collection parameters
    let mutable _description = description
    let mutable _intendedUse = intendedUse
    let mutable _additionalProperties = Construction.collection additionalProperties
    let mutable _components = Construction.collection components
    let mutable _version = version
    let mutable _url = url
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "Recipe"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable recipe title.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Prospective parameter slots, preserving references and duplicates.
    member _.Parameters with get() = _parameters and set(value) = _parameters <- Construction.copy value
    /// Description of the planned procedure.
    member _.Description with get() = _description and set(value) = _description <- value
    /// Classification as plain text or a defined term.
    member _.IntendedUse with get() = _intendedUse and set(value) = _intendedUse <- value
    /// Extensible recipe metadata.
    member _.AdditionalProperties with get() = _additionalProperties and set(value) = _additionalProperties <- Construction.copy value
    /// Equipment, software, reagents, or other components.
    member _.Components with get() = _components and set(value) = _components <- Construction.copy value
    /// Recipe version identifier.
    member _.Version with get() = _version and set(value) = _version <- value
    /// URL of an external recipe resource.
    member _.Url with get() = _url and set(value) = _url <- value
