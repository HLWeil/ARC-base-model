namespace ARCBaseModel

open Fable.Core

/// One directed transformation with optional singular input and output endpoints.
[<AttachMembers>]
type Process(name: string, ?input: EntityReference, ?output: EntityReference, ?executesRecipe: Recipe,
             ?parameterValues: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    let mutable _name = Construction.required "name" name
    let mutable _input = input
    let mutable _output = output
    let mutable _executesRecipe = executesRecipe
    let mutable _parameterValues = Construction.collection parameterValues
    let mutable _id = id
    let mutable _additionalTypes = Construction.collection additionalTypes

    /// Fixed entity discriminator.
    member _.Type = "Process"
    /// Optional application-scoped identifier; never assigned automatically.
    member _.Id with get() = _id and set(value) = _id <- value
    /// Additional classifications, preserving duplicates.
    member _.AdditionalTypes with get() = _additionalTypes and set(value) = _additionalTypes <- Construction.copy value
    /// Human-readable process name.
    member _.Name with get() = _name and set(value) = _name <- value
    /// Optional single Sample or Data input; no graph links are maintained.
    member _.Input with get() = _input and set(value) = _input <- value
    /// Optional single Sample or Data output; no graph links are maintained.
    member _.Output with get() = _output and set(value) = _output <- value
    /// Recipe executed by this process.
    member _.ExecutesRecipe with get() = _executesRecipe and set(value) = _executesRecipe <- value
    /// Actual parameter annotations, preserving order, duplicates, and references.
    member _.ParameterValues with get() = _parameterValues and set(value) = _parameterValues <- Construction.copy value
