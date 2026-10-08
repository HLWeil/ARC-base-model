namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type ProcessOperations internal (session: Session) =
    member _.create(name: string) = let value = ARCBaseModel.Process(name) in session.Register(value,"Process"); value
    member _.register(value: ARCBaseModel.Process) = session.Register(value,"Process"); value
    member _.upsert(value: ARCBaseModel.Process): unit = session.Set(value,"Process")
    member _.get(id: string) = session.Get<ARCBaseModel.Process>("Process",id)
    member _.list() = session.List<ARCBaseModel.Process>("Process")
    member _.delete(value: ARCBaseModel.Process) = session.Delete(value)
    member _.setName(value: ARCBaseModel.Process, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Process.setName")
    member _.setInput(value: ARCBaseModel.Process, replacement: EntityReference) =
        session.Change(value,"input",Some(Links [session.Id(Model.endpoint replacement)]),"Process.setInput")
    member _.clearInput(value: ARCBaseModel.Process) = session.Change(value,"input",None,"Process.clearInput")
    member _.setOutput(value: ARCBaseModel.Process, replacement: EntityReference) =
        session.Change(value,"output",Some(Links [session.Id(Model.endpoint replacement)]),"Process.setOutput")
    member _.clearOutput(value: ARCBaseModel.Process) = session.Change(value,"output",None,"Process.clearOutput")
    member _.setExecutesRecipe(value: ARCBaseModel.Process, replacement: Recipe) =
        session.Change(value,"executesRecipe",Some(Links [session.Id(replacement)]),"Process.setExecutesRecipe")
    member _.clearExecutesRecipe(value: ARCBaseModel.Process) = session.Change(value,"executesRecipe",None,"Process.clearExecutesRecipe")
    member _.setParameterValues(value: ARCBaseModel.Process, replacement: seq<Annotation>) =
        session.Change(value,"parameterValues",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Process.setParameterValues")
    member _.addParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection(value,"parameterValues",target,true,"Process.addParameterValue")
    member _.removeParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection(value,"parameterValues",target,false,"Process.removeParameterValue")
    member _.setAdditionalTypes(value: ARCBaseModel.Process, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Process.setAdditionalTypes")
    member _.setInputSample(value: ARCBaseModel.Process, target: Sample) = session.Change(value,"input",Some(Links [session.Id(target)]),"Process.setInputSample")
    member _.setInputData(value: ARCBaseModel.Process, target: Data) = session.Change(value,"input",Some(Links [session.Id(target)]),"Process.setInputData")
    member _.setOutputSample(value: ARCBaseModel.Process, target: Sample) = session.Change(value,"output",Some(Links [session.Id(target)]),"Process.setOutputSample")
    member _.setOutputData(value: ARCBaseModel.Process, target: Data) = session.Change(value,"output",Some(Links [session.Id(target)]),"Process.setOutputData")
