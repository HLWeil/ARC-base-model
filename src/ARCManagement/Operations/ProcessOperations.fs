namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ProcessOperations internal (session: GraphContext) =
    member _.create(name: string) = let value = ARCBaseModel.Process(name) in session.Register(value,"Process"); value
    member _.add(value: ARCBaseModel.Process) = session.Register(value,"Process"); value
    member _.upsert(value: ARCBaseModel.Process): unit = session.Set(value,"Process")
    member _.get(id: string) = session.Get<ARCBaseModel.Process>("Process",id)
    member _.list() = session.List<ARCBaseModel.Process>("Process")
    member _.delete(value: ARCBaseModel.Process) = session.Delete(value)
    member _.setName(value: ARCBaseModel.Process, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Process.setName")
    member _.setInput(value: ARCBaseModel.Process, replacement: EntityReference option) =
        session.Change(value,"input",replacement |> Option.map (fun replacement -> Links [session.Id(Model.endpoint replacement)]),"Process.setInput")
    member _.setOutput(value: ARCBaseModel.Process, replacement: EntityReference option) =
        session.Change(value,"output",replacement |> Option.map (fun replacement -> Links [session.Id(Model.endpoint replacement)]),"Process.setOutput")
    member _.setExecutesRecipe(value: ARCBaseModel.Process, replacement: Recipe option) =
        session.Change(value,"executesRecipe",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),"Process.setExecutesRecipe")
    member _.setParameterValues(value: ARCBaseModel.Process, replacement: seq<Annotation>) =
        session.Change(value,"parameterValues",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Process.setParameterValues")
    member _.addParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection(value,"parameterValues",target,true,"Process.addParameterValue")
    member _.removeParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection(value,"parameterValues",target,false,"Process.removeParameterValue")
