namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ProcessOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<ARCBaseModel.Process>(ARCBaseModel.Process(name), "Process", true,None,false)
    member _.add(value: ARCBaseModel.Process) = session.Adopt<ARCBaseModel.Process>(value, "Process", true,None,false)
    member _.upsert(value: ARCBaseModel.Process, ?baseline: ARCBaseModel.Process, ?recursive: bool) = session.Adopt<ARCBaseModel.Process>(value, "Process", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<ARCBaseModel.Process>("Process",id)
    member _.list() = session.List<ARCBaseModel.Process>("Process")
    member _.read(id: string) = session.Read<ARCBaseModel.Process>("Process",id)
    member _.readAll() = session.ReadAll<ARCBaseModel.Process>("Process")
    member _.delete(value: ARCBaseModel.Process, ?cascade: bool) = session.Delete(session.Id(value,"Process"),"Process",defaultArg cascade false)
    member _.setName(value: ARCBaseModel.Process, replacement: string) =
        session.Change<ARCBaseModel.Process>(session.Id(value,"Process"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"Process.setName")
    member _.setInput(value: ARCBaseModel.Process, replacement: EntityReference) =
        session.Endpoint<ARCBaseModel.Process>(session.Id(value,"Process"),"input",replacement,"Process.setInput")
    member _.clearInput(value: ARCBaseModel.Process) = session.Change<ARCBaseModel.Process>(session.Id(value,"Process"),"input", (fun () -> None),"Process.clearInput")
    member _.setOutput(value: ARCBaseModel.Process, replacement: EntityReference) =
        session.Endpoint<ARCBaseModel.Process>(session.Id(value,"Process"),"output",replacement,"Process.setOutput")
    member _.clearOutput(value: ARCBaseModel.Process) = session.Change<ARCBaseModel.Process>(session.Id(value,"Process"),"output", (fun () -> None),"Process.clearOutput")
    member _.setExecutesRecipe(value: ARCBaseModel.Process, replacement: Recipe) =
        session.Reference<ARCBaseModel.Process>(session.Id(value,"Process"),"executesRecipe",replacement,"Recipe","Process.setExecutesRecipe")
    member _.clearExecutesRecipe(value: ARCBaseModel.Process) = session.Change<ARCBaseModel.Process>(session.Id(value,"Process"),"executesRecipe", (fun () -> None),"Process.clearExecutesRecipe")
    member _.setParameterValues(value: ARCBaseModel.Process, replacement: seq<Annotation>) =
        session.Change<ARCBaseModel.Process>(session.Id(value,"Process"),"parameterValues", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"Process.setParameterValues")
    member _.addParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection<ARCBaseModel.Process>(session.Id(value,"Process"),"parameterValues",target,"Annotation",true,"Process.addParameterValue")
    member _.removeParameterValue(value: ARCBaseModel.Process, target: Annotation) = session.Collection<ARCBaseModel.Process>(session.Id(value,"Process"),"parameterValues",target,"Annotation",false,"Process.removeParameterValue")
    member _.setAdditionalTypes(value: ARCBaseModel.Process, replacement: seq<string>) =
        session.Change<ARCBaseModel.Process>(session.Id(value,"Process"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Process.setAdditionalTypes")
    member _.setInputSample(value: ARCBaseModel.Process, target: Sample) = session.Reference<ARCBaseModel.Process>(session.Id(value,"Process"),"input",target,"Sample","Process.setInputSample")
    member _.setInputData(value: ARCBaseModel.Process, target: Data) = session.Reference<ARCBaseModel.Process>(session.Id(value,"Process"),"input",target,"Data","Process.setInputData")
    member _.setOutputSample(value: ARCBaseModel.Process, target: Sample) = session.Reference<ARCBaseModel.Process>(session.Id(value,"Process"),"output",target,"Sample","Process.setOutputSample")
    member _.setOutputData(value: ARCBaseModel.Process, target: Data) = session.Reference<ARCBaseModel.Process>(session.Id(value,"Process"),"output",target,"Data","Process.setOutputData")
