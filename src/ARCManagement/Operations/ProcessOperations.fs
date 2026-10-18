namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ProcessOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<ARCBaseModel.Process>(revision, ARCBaseModel.Process(name), "Process", true, false)
    member _.add(revision: RevisionToken, value: ARCBaseModel.Process) = session.Adopt<ARCBaseModel.Process>(revision, value, "Process", true, false)
    member _.upsert(revision: RevisionToken, value: ARCBaseModel.Process, ?recursive: bool) = session.Adopt<ARCBaseModel.Process>(revision, value, "Process", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<ARCBaseModel.Process>("Process",id)
    member _.list() = session.List<ARCBaseModel.Process>("Process")
    member _.read(id: string) = session.Read<ARCBaseModel.Process>("Process",id)
    member _.readAll() = session.ReadAll<ARCBaseModel.Process>("Process")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Process",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<ARCBaseModel.Process>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"Process.setName")
    member _.setInput(revision: RevisionToken, id: string, replacement: string) =
        session.Change<ARCBaseModel.Process>(revision,id,"input", (fun () -> Some(Links [replacement])),"Process.setInput")
    member _.clearInput(revision: RevisionToken, id: string) = session.Change<ARCBaseModel.Process>(revision,id,"input", (fun () -> None),"Process.clearInput")
    member _.setOutput(revision: RevisionToken, id: string, replacement: string) =
        session.Change<ARCBaseModel.Process>(revision,id,"output", (fun () -> Some(Links [replacement])),"Process.setOutput")
    member _.clearOutput(revision: RevisionToken, id: string) = session.Change<ARCBaseModel.Process>(revision,id,"output", (fun () -> None),"Process.clearOutput")
    member _.setExecutesRecipe(revision: RevisionToken, id: string, replacement: string) =
        session.Change<ARCBaseModel.Process>(revision,id,"executesRecipe", (fun () -> Some(Links [replacement])),"Process.setExecutesRecipe")
    member _.clearExecutesRecipe(revision: RevisionToken, id: string) = session.Change<ARCBaseModel.Process>(revision,id,"executesRecipe", (fun () -> None),"Process.clearExecutesRecipe")
    member _.setParameterValues(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<ARCBaseModel.Process>(revision,id,"parameterValues", (fun () -> Some(Links(replacement |> List.ofSeq))),"Process.setParameterValues")
    member _.addParameterValue(revision: RevisionToken, id: string, target: string) = session.Collection<ARCBaseModel.Process>(revision,id,"parameterValues",target,true,"Process.addParameterValue")
    member _.removeParameterValue(revision: RevisionToken, id: string, target: string) = session.Collection<ARCBaseModel.Process>(revision,id,"parameterValues",target,false,"Process.removeParameterValue")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<ARCBaseModel.Process>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Process.setAdditionalTypes")
    member _.setInputSample(revision: RevisionToken, id: string, target: string) = session.Change<ARCBaseModel.Process>(revision,id,"input", (fun () -> Some(Links [target])),"Process.setInputSample")
    member _.setInputData(revision: RevisionToken, id: string, target: string) = session.Change<ARCBaseModel.Process>(revision,id,"input", (fun () -> Some(Links [target])),"Process.setInputData")
    member _.setOutputSample(revision: RevisionToken, id: string, target: string) = session.Change<ARCBaseModel.Process>(revision,id,"output", (fun () -> Some(Links [target])),"Process.setOutputSample")
    member _.setOutputData(revision: RevisionToken, id: string, target: string) = session.Change<ARCBaseModel.Process>(revision,id,"output", (fun () -> Some(Links [target])),"Process.setOutputData")
