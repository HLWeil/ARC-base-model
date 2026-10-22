namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ProcessOperations internal (session: GraphContext) =
    member _.create(name: string): ARCBaseModel.Process = session.Add<ARCBaseModel.Process>(ARCBaseModel.Process(name), Some "Process")
    member _.add(value: ARCBaseModel.Process): ARCBaseModel.Process = session.Add<ARCBaseModel.Process>(value, Some "Process")
    member _.upsert(value: ARCBaseModel.Process, ?recursive: bool): ARCBaseModel.Process = session.Upsert<ARCBaseModel.Process>(value, Some "Process", defaultArg recursive false)
    member _.get(id: string) = session.Get<ARCBaseModel.Process>("Process",id)
    member _.list(): seq<ARCBaseModel.Process> = session.List<ARCBaseModel.Process>("Process")
    member _.delete(value: ARCBaseModel.Process) = session.Delete(value, Some "Process")
    member _.setName(value: ARCBaseModel.Process, replacement: string) =
        session.Change<ARCBaseModel.Process>(value,Some "Process","name",Some(Text(Model.required "name" replacement)),[],"Process.setName")
    member _.setInput(value: ARCBaseModel.Process, replacement: EntityReference option) =
        session.Change<ARCBaseModel.Process>(value,Some "Process","input",replacement |> Option.map (fun replacement -> Links [session.Id(Model.endpoint replacement)]),(replacement |> Option.map (fun replacement -> Model.endpoint replacement) |> Option.toList),"Process.setInput")
    member _.setOutput(value: ARCBaseModel.Process, replacement: EntityReference option) =
        session.Change<ARCBaseModel.Process>(value,Some "Process","output",replacement |> Option.map (fun replacement -> Links [session.Id(Model.endpoint replacement)]),(replacement |> Option.map (fun replacement -> Model.endpoint replacement) |> Option.toList),"Process.setOutput")
    member _.setExecutesRecipe(value: ARCBaseModel.Process, replacement: Recipe option) =
        session.Change<ARCBaseModel.Process>(value,Some "Process","executesRecipe",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),(replacement |> Option.map box |> Option.toList),"Process.setExecutesRecipe")
    member _.setParameterValues(value: ARCBaseModel.Process, replacement: seq<Annotation>) =
        session.ChangeLinks<ARCBaseModel.Process>(value,Some "Process","parameterValues",replacement |> Seq.map box,"Process.setParameterValues")
    member _.addParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection<ARCBaseModel.Process>(value,Some "Process","parameterValues",target,true,"Process.addParameterValue")
    member _.removeParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection<ARCBaseModel.Process>(value,Some "Process","parameterValues",target,false,"Process.removeParameterValue")
