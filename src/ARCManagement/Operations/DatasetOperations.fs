namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DatasetOperations internal (session: GraphContext) =
    member _.create(identifier: string) = session.Adopt<Dataset>(Dataset(["process-provenance"], [Model.required "identifier" identifier]), "Dataset", true,None,false)
    member _.add(value: Dataset) = session.Adopt<Dataset>(value, "Dataset", true,None,false)
    member _.upsert(value: Dataset, ?baseline: Dataset, ?recursive: bool) = session.Adopt<Dataset>(value, "Dataset", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Dataset>("Dataset",id)
    member _.list() = session.List<Dataset>("Dataset")
    member _.read(id: string) = session.Read<Dataset>("Dataset",id)
    member _.readAll() = session.ReadAll<Dataset>("Dataset")
    member _.delete(value: Dataset, ?cascade: bool) = session.Delete(session.Id(value,"Dataset"),"Dataset",defaultArg cascade false)
    member _.setConformsTo(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"conformsTo", (fun () -> Some(Texts(List.ofSeq replacement))),"Dataset.setConformsTo")
    member _.setIdentifiers(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"identifiers", (fun () -> Some(Texts(List.ofSeq replacement))),"Dataset.setIdentifiers")
    member _.setTitle(value: Dataset, replacement: string) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"title", (fun () -> Some(Text(Model.required "title" replacement))),"Dataset.setTitle")
    member _.clearTitle(value: Dataset) = session.Change<Dataset>(session.Id(value,"Dataset"),"title", (fun () -> None),"Dataset.clearTitle")
    member _.setDescription(value: Dataset, replacement: string) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"description", (fun () -> Some(Text(Model.required "description" replacement))),"Dataset.setDescription")
    member _.clearDescription(value: Dataset) = session.Change<Dataset>(session.Id(value,"Dataset"),"description", (fun () -> None),"Dataset.clearDescription")
    member _.setLicense(value: Dataset, replacement: string) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"license", (fun () -> Some(Text(Model.required "license" replacement))),"Dataset.setLicense")
    member _.clearLicense(value: Dataset) = session.Change<Dataset>(session.Id(value,"Dataset"),"license", (fun () -> None),"Dataset.clearLicense")
    member _.setDatePublished(value: Dataset, replacement: string) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"datePublished", (fun () -> Some(Text(Model.required "datePublished" replacement))),"Dataset.setDatePublished")
    member _.clearDatePublished(value: Dataset) = session.Change<Dataset>(session.Id(value,"Dataset"),"datePublished", (fun () -> None),"Dataset.clearDatePublished")
    member _.setDateCreated(value: Dataset, replacement: string) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"dateCreated", (fun () -> Some(Text(Model.required "dateCreated" replacement))),"Dataset.setDateCreated")
    member _.clearDateCreated(value: Dataset) = session.Change<Dataset>(session.Id(value,"Dataset"),"dateCreated", (fun () -> None),"Dataset.clearDateCreated")
    member _.setDateModified(value: Dataset, replacement: string) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"dateModified", (fun () -> Some(Text(Model.required "dateModified" replacement))),"Dataset.setDateModified")
    member _.clearDateModified(value: Dataset) = session.Change<Dataset>(session.Id(value,"Dataset"),"dateModified", (fun () -> None),"Dataset.clearDateModified")
    member _.setHasParts(value: Dataset, replacement: seq<Dataset>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"hasParts", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Dataset")) |> List.ofSeq))),"Dataset.setHasParts")
    member _.addPart(value: Dataset, target: Dataset) = session.Collection<Dataset>(session.Id(value,"Dataset"),"hasParts",target,"Dataset",true,"Dataset.addPart")
    member _.removePart(value: Dataset, target: Dataset) = session.Collection<Dataset>(session.Id(value,"Dataset"),"hasParts",target,"Dataset",false,"Dataset.removePart")
    member _.setDataFiles(value: Dataset, replacement: seq<Data>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"dataFiles", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Data")) |> List.ofSeq))),"Dataset.setDataFiles")
    member _.addDataFile(value: Dataset, target: Data) = session.Collection<Dataset>(session.Id(value,"Dataset"),"dataFiles",target,"Data",true,"Dataset.addDataFile")
    member _.removeDataFile(value: Dataset, target: Data) = session.Collection<Dataset>(session.Id(value,"Dataset"),"dataFiles",target,"Data",false,"Dataset.removeDataFile")
    member _.setAgents(value: Dataset, replacement: seq<Agent>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"agents", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Agent")) |> List.ofSeq))),"Dataset.setAgents")
    member _.addAgent(value: Dataset, target: Agent) = session.Collection<Dataset>(session.Id(value,"Dataset"),"agents",target,"Agent",true,"Dataset.addAgent")
    member _.removeAgent(value: Dataset, target: Agent) = session.Collection<Dataset>(session.Id(value,"Dataset"),"agents",target,"Agent",false,"Dataset.removeAgent")
    member _.setCitations(value: Dataset, replacement: seq<ScholarlyArticle>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"citations", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"ScholarlyArticle")) |> List.ofSeq))),"Dataset.setCitations")
    member _.addCitation(value: Dataset, target: ScholarlyArticle) = session.Collection<Dataset>(session.Id(value,"Dataset"),"citations",target,"ScholarlyArticle",true,"Dataset.addCitation")
    member _.removeCitation(value: Dataset, target: ScholarlyArticle) = session.Collection<Dataset>(session.Id(value,"Dataset"),"citations",target,"ScholarlyArticle",false,"Dataset.removeCitation")
    member _.setProcesses(value: Dataset, replacement: seq<ARCBaseModel.Process>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"processes", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Process")) |> List.ofSeq))),"Dataset.setProcesses")
    member _.addProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection<Dataset>(session.Id(value,"Dataset"),"processes",target,"Process",true,"Dataset.addProcess")
    member _.removeProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection<Dataset>(session.Id(value,"Dataset"),"processes",target,"Process",false,"Dataset.removeProcess")
    member _.setDescriptors(value: Dataset, replacement: seq<Descriptor>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"descriptors", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Descriptor")) |> List.ofSeq))),"Dataset.setDescriptors")
    member _.addDescriptor(value: Dataset, target: Descriptor) = session.Collection<Dataset>(session.Id(value,"Dataset"),"descriptors",target,"Descriptor",true,"Dataset.addDescriptor")
    member _.removeDescriptor(value: Dataset, target: Descriptor) = session.Collection<Dataset>(session.Id(value,"Dataset"),"descriptors",target,"Descriptor",false,"Dataset.removeDescriptor")
    member _.setAdditionalProperties(value: Dataset, replacement: seq<Annotation>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"additionalProperties", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"Dataset.setAdditionalProperties")
    member _.addAdditionalProperty(value: Dataset, target: Annotation) = session.Collection<Dataset>(session.Id(value,"Dataset"),"additionalProperties",target,"Annotation",true,"Dataset.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Dataset, target: Annotation) = session.Collection<Dataset>(session.Id(value,"Dataset"),"additionalProperties",target,"Annotation",false,"Dataset.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(session.Id(value,"Dataset"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Dataset.setAdditionalTypes")
    member _.movePart(child: Dataset, destination: Dataset) = session.Move<Dataset>(session.Id(child,"Dataset"),session.Id(destination,"Dataset"),"hasParts","Dataset.movePart")
    member _.moveProcess(value: ARCBaseModel.Process, destination: Dataset) = session.Move<ARCBaseModel.Process>(session.Id(value,"Process"),session.Id(destination,"Dataset"),"processes","Dataset.moveProcess")
