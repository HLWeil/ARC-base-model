namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type SampleOperations internal (session: Session) =
    member _.create(name: string) = let value = Sample(name) in session.Register(value, "Sample"); value
    member _.register(value: Sample) = session.Register(value, "Sample"); value
    /// Register a new Sample or replace supported values in the existing instance by ID.
    /// Use a detached value for updates; direct mutation of registered objects is unsupported.
    member _.set(value: Sample): unit = session.SetSample(value)
    member _.get(id: string) = session.GetSample(id)
    member _.list() = session.Samples()
    member _.setName(value: Sample, name: string) =
        let id = session.SampleId(value)
        session.Execute("Sample.setName", fun state -> {state with Samples = state.Samples |> List.map (fun row -> if row.Id = id then {row with Name = Model.required "name" name} else row)})
    member _.delete(value: Sample) =
        let id = session.SampleId(value)
        session.Execute("Sample.delete", fun state ->
            {state with
                Samples = state.Samples |> List.filter (fun row -> row.Id <> id)
                Processes = state.Processes |> List.map (fun row -> {row with Input = row.Input |> Option.filter ((<>) id); Output = row.Output |> Option.filter ((<>) id)})})
