namespace ARCBaseModel

open Fable.Core

/// Recipe classification represented by text or a controlled vocabulary term.
[<Erase; RequireQualifiedAccess>]
type RecipeIntendedUse =
    | Text of string
    | Term of DefinedTerm

type internal RecipeState =
    { Name: string option
      Parameters: seq<FormalParameter>
      Description: string option
      IntendedUse: RecipeIntendedUse option
      AdditionalProperties: seq<Annotation>
      Components: seq<Annotation>
      Version: string option
      Url: string option }

/// Description of a planned procedure and its prospective parameters.
[<AttachMembers>]
type Recipe(?name: string, ?parameters: seq<FormalParameter>, ?description: string,
            ?intendedUse: RecipeIntendedUse, ?additionalProperties: seq<Annotation>, ?components: seq<Annotation>,
            ?version: string, ?url: string, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Recipe", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: RecipeState =
        { Name = name
          Parameters = Construction.optionalCollection "parameters" parameters
          Description = description
          IntendedUse = intendedUse
          AdditionalProperties = Construction.optionalCollection "additionalProperties" additionalProperties
          Components = Construction.optionalCollection "components" components
          Version = version
          Url = url }

    do base.ReserveProperties(["name"; "parameters"; "description"; "intendedUse"; "additionalProperties"; "components"; "version"; "url"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable recipe title.
    member _.Name = state.Name
    /// Prospective parameter slots, preserving references and duplicates.
    member _.Parameters = state.Parameters
    /// Description of the planned procedure.
    member _.Description = state.Description
    /// Classification as plain text or a defined term.
    member _.IntendedUse = state.IntendedUse
    /// Extensible recipe metadata.
    member _.AdditionalProperties = state.AdditionalProperties
    /// Equipment, software, reagents, or other components.
    member _.Components = state.Components
    /// Recipe version identifier.
    member _.Version = state.Version
    /// URL of an external recipe resource.
    member _.Url = state.Url
    static member withName(value: Recipe, replacement: string option) : Recipe =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = replacement })
    static member withParameters(value: Recipe, replacement: seq<FormalParameter>) : Recipe =
        let items = Construction.collection "parameters" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Parameters = items })
    static member withDescription(value: Recipe, replacement: string option) : Recipe =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Description = replacement })
    static member withIntendedUse(value: Recipe, replacement: RecipeIntendedUse option) : Recipe =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with IntendedUse = replacement })
    static member withAdditionalProperties(value: Recipe, replacement: seq<Annotation>) : Recipe =
        let items = Construction.collection "additionalProperties" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with AdditionalProperties = items })
    static member withComponents(value: Recipe, replacement: seq<Annotation>) : Recipe =
        let items = Construction.collection "components" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Components = items })
    static member withVersion(value: Recipe, replacement: string option) : Recipe =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Version = replacement })
    static member withUrl(value: Recipe, replacement: string option) : Recipe =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Url = replacement })
