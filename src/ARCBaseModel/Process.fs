namespace ARCBaseModel

open Fable.Core

/// One directed transformation with optional singular input and output endpoints.
[<AttachMembers>]
type Process(name: string, ?input: EntityReference, ?output: EntityReference, ?executesRecipe: Recipe,
             ?parameterValues: seq<Annotation>, ?id: string, ?additionalTypes: seq<string>) =
    inherit EntityObject("Process", ?id = id, ?additionalTypes = additionalTypes)
    let mutable _name = Construction.required "name" name
    let mutable _input = input
    let mutable _output = output
    let mutable _executesRecipe = executesRecipe
    let mutable _parameterValues = Construction.optionalCollection parameterValues

    do base.ReserveProperties(["name"; "input"; "output"; "executesRecipe"; "parameterValues"])
    do base.SealInternal()
    /// Human-readable process name.
    member _.Name with get() = _name
    /// Optional single Sample or Data input; no graph links are maintained.
    member _.Input with get() = _input
    /// Optional single Sample or Data output; no graph links are maintained.
    member _.Output with get() = _output
    /// Recipe executed by this process.
    member _.ExecutesRecipe with get() = _executesRecipe
    /// Actual parameter annotations, preserving order, duplicates, and references.
    member _.ParameterValues with get() = _parameterValues

    override this.AssignInternal(key,value) =
        this.EnsureUnsealed()
        match key with
        | "name" -> _name <- unbox<string> value
        | "input" -> _input <- unbox<EntityReference option> value
        | "output" -> _output <- unbox<EntityReference option> value
        | "executesRecipe" -> _executesRecipe <- unbox<Recipe option> value
        | "parameterValues" -> _parameterValues <- Construction.collection (unbox<seq<Annotation>> value)
        | _ -> base.AssignInternal(key,value)
    override this.ValidateInternal() =
        Construction.text "Name" _name |> ignore
#if FABLE_COMPILER
        _input |> Option.iter (fun item -> if not (box item :? Sample || box item :? Data) then invalidArg "Input" "Invalid value alternative.")
#else
        _input |> Option.iter (Construction.required "Input" >> ignore)
#endif
#if FABLE_COMPILER
        _output |> Option.iter (fun item -> if not (box item :? Sample || box item :? Data) then invalidArg "Output" "Invalid value alternative.")
#else
        _output |> Option.iter (Construction.required "Output" >> ignore)
#endif
        _executesRecipe |> Option.iter (fun item -> if not (box item :? Recipe) then invalidArg "ExecutesRecipe" "Unexpected entity kind.")
        this.ValidateOwned("parameterValues",box _parameterValues,fun () -> for item in _parameterValues do if not (box item :? Annotation) then invalidArg "ParameterValues" "Unexpected entity kind.")
        ()
    static member withName(value:Process,replacement:string):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("name",box replacement))
    static member withInput(value:Process,replacement:EntityReference):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("input",box(Some replacement)))
    static member clearInput(value:Process):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("input",box(None:EntityReference option)))
    static member withOutput(value:Process,replacement:EntityReference):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("output",box(Some replacement)))
    static member clearOutput(value:Process):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("output",box(None:EntityReference option)))
    static member withExecutesRecipe(value:Process,replacement:Recipe):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("executesRecipe",box(Some replacement)))
    static member clearExecutesRecipe(value:Process):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("executesRecipe",box(None:Recipe option)))
    static member withParameterValues(value:Process,replacement:seq<Annotation>):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("parameterValues",box replacement))
    static member withId(value:Process,replacement:string):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(Some replacement)))
    static member clearId(value:Process):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("id",box(None:string option)))
    static member withAdditionalTypes(value:Process,replacement:seq<string>):Process = EntityObject.Copy(value,fun copy -> copy.AssignInternal("additionalTypes",box replacement))
    static member withEntityProperty(value:Process,key:string,replacement:Entity):Process = EntityObject.Copy(value,fun copy -> copy.SetExtensionInternal(key,replacement))
    static member withoutEntityProperty(value:Process,key:string):Process = EntityObject.Copy(value,fun copy -> copy.RemoveExtensionInternal(key))
