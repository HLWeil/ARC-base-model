namespace ARCBaseModel

open Fable.Core

type internal ProcessState =
    { Name: string
      Input: EntityReference option
      Output: EntityReference option
      ExecutesRecipe: Recipe option
      ParameterValues: seq<Annotation> }

/// One directed transformation with optional singular input and output endpoints.
[<AttachMembers>]
type Process(name: string, ?input: EntityReference, ?output: EntityReference, ?executesRecipe: Recipe,
             ?parameterValues: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Process", ?id = id, ?additionalTypes = additionalTypes)
    let mutable state: ProcessState =
        { Name = Construction.required "name" name
          Input = input
          Output = output
          ExecutesRecipe = executesRecipe
          ParameterValues = Construction.optionalCollection "parameterValues" parameterValues }

    do base.ReserveProperties(["name"; "input"; "output"; "executesRecipe"; "parameterValues"])
    member internal _.State with get() = state and set(value) = state <- value
    /// Human-readable process name.
    member _.Name = state.Name
    /// Optional single Sample or Data input; no graph links are maintained.
    member _.Input = state.Input
    /// Optional single Sample or Data output; no graph links are maintained.
    member _.Output = state.Output
    /// Recipe executed by this process.
    member _.ExecutesRecipe = state.ExecutesRecipe
    /// Actual parameter annotations, preserving order, duplicates, and references.
    member _.ParameterValues = state.ParameterValues
    static member withName(value: Process, replacement: string) : Process =
        let name = Construction.required "name" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Name = name })
    static member withInput(value: Process, replacement: EntityReference option) : Process =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Input = replacement })
    static member withOutput(value: Process, replacement: EntityReference option) : Process =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with Output = replacement })
    static member withExecutesRecipe(value: Process, replacement: Recipe option) : Process =
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with ExecutesRecipe = replacement })
    static member withParameterValues(value: Process, replacement: seq<Annotation>) : Process =
        let items = Construction.collection "parameterValues" replacement
        EntityObject.Copy(value, fun copy -> copy.State <- { value.State with ParameterValues = items })
