namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type ProcessOperations internal (session: Session) =
    member _.create(name: string) = let value = ARCBaseModel.Process(name) in session.Register(value, "Process"); value
    member _.register(value: ARCBaseModel.Process) = session.Register(value, "Process"); value
    member _.get(id: string) = session.GetProcess(id)
    member _.list() = session.Processes()
    member _.setName(value: ARCBaseModel.Process, name: string) =
        let id = session.ProcessId(value)
        session.Execute("Process.setName", fun state -> {state with Processes = state.Processes |> List.map (fun row -> if row.Id = id then {row with Name = Model.required "name" name} else row)})
    member _.setInputSample(value: ARCBaseModel.Process, sample: Sample) =
        let id, sampleId = session.ProcessId(value), session.SampleId(sample)
        session.Execute("Process.setInputSample", fun state -> {state with Processes = state.Processes |> List.map (fun row -> if row.Id = id then {row with Input = Some sampleId} else row)})
    member _.clearInput(value: ARCBaseModel.Process) =
        let id = session.ProcessId(value)
        session.Execute("Process.clearInput", fun state -> {state with Processes = state.Processes |> List.map (fun row -> if row.Id = id then {row with Input = None} else row)})
    member _.setOutputSample(value: ARCBaseModel.Process, sample: Sample) =
        let id, sampleId = session.ProcessId(value), session.SampleId(sample)
        session.Execute("Process.setOutputSample", fun state -> {state with Processes = state.Processes |> List.map (fun row -> if row.Id = id then {row with Output = Some sampleId} else row)})
    member _.clearOutput(value: ARCBaseModel.Process) =
        let id = session.ProcessId(value)
        session.Execute("Process.clearOutput", fun state -> {state with Processes = state.Processes |> List.map (fun row -> if row.Id = id then {row with Output = None} else row)})
    member _.delete(value: ARCBaseModel.Process) =
        let id = session.ProcessId(value)
        session.Execute("Process.delete", fun state ->
            {state with
                Processes = state.Processes |> List.filter (fun row -> row.Id <> id)
                Datasets = state.Datasets |> List.map (fun row -> {row with Processes = row.Processes |> List.filter ((<>) id)})})
