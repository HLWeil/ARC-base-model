namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type DatasetOperations internal (session: Session) =
    member _.create(identifier: string) =
        let value = Dataset(["process-provenance"], [Model.required "identifier" identifier])
        session.Register(value,"Dataset"); value
    member _.register(value: Dataset) = session.Register(value,"Dataset"); value
    member _.set(value: Dataset): unit = session.Set(value,"Dataset")
    member _.get(id: string) = session.Get<Dataset>("Dataset",id)
    member _.list() = session.List<Dataset>("Dataset")
    member _.delete(value: Dataset) = session.Delete(value)
    member _.setConformsTo(value: Dataset, replacement: seq<string>) =
        session.Change(value,"conformsTo",Some(Texts(List.ofSeq replacement)),"Dataset.setConformsTo")
    member _.setIdentifiers(value: Dataset, replacement: seq<string>) =
        session.Change(value,"identifiers",Some(Texts(List.ofSeq replacement)),"Dataset.setIdentifiers")
    member _.setTitle(value: Dataset, replacement: string) =
        session.Change(value,"title",Some(Text(Model.required "title" replacement)),"Dataset.setTitle")
    member _.clearTitle(value: Dataset) = session.Change(value,"title",None,"Dataset.clearTitle")
    member _.setDescription(value: Dataset, replacement: string) =
        session.Change(value,"description",Some(Text(Model.required "description" replacement)),"Dataset.setDescription")
    member _.clearDescription(value: Dataset) = session.Change(value,"description",None,"Dataset.clearDescription")
    member _.setLicense(value: Dataset, replacement: string) =
        session.Change(value,"license",Some(Text(Model.required "license" replacement)),"Dataset.setLicense")
    member _.clearLicense(value: Dataset) = session.Change(value,"license",None,"Dataset.clearLicense")
    member _.setDatePublished(value: Dataset, replacement: string) =
        session.Change(value,"datePublished",Some(Text(Model.required "datePublished" replacement)),"Dataset.setDatePublished")
    member _.clearDatePublished(value: Dataset) = session.Change(value,"datePublished",None,"Dataset.clearDatePublished")
    member _.setDateCreated(value: Dataset, replacement: string) =
        session.Change(value,"dateCreated",Some(Text(Model.required "dateCreated" replacement)),"Dataset.setDateCreated")
    member _.clearDateCreated(value: Dataset) = session.Change(value,"dateCreated",None,"Dataset.clearDateCreated")
    member _.setDateModified(value: Dataset, replacement: string) =
        session.Change(value,"dateModified",Some(Text(Model.required "dateModified" replacement)),"Dataset.setDateModified")
    member _.clearDateModified(value: Dataset) = session.Change(value,"dateModified",None,"Dataset.clearDateModified")
    member _.setHasParts(value: Dataset, replacement: seq<Dataset>) =
        session.Change(value,"hasParts",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setHasParts")
    member _.addPart(value: Dataset, target: Dataset) = session.Collection(value,"hasParts",target,true,"Dataset.addPart")
    member _.removePart(value: Dataset, target: Dataset) = session.Collection(value,"hasParts",target,false,"Dataset.removePart")
    member _.setDataFiles(value: Dataset, replacement: seq<Data>) =
        session.Change(value,"dataFiles",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setDataFiles")
    member _.addDataFile(value: Dataset, target: Data) = session.Collection(value,"dataFiles",target,true,"Dataset.addDataFile")
    member _.removeDataFile(value: Dataset, target: Data) = session.Collection(value,"dataFiles",target,false,"Dataset.removeDataFile")
    member _.setAgents(value: Dataset, replacement: seq<Agent>) =
        session.Change(value,"agents",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setAgents")
    member _.addAgent(value: Dataset, target: Agent) = session.Collection(value,"agents",target,true,"Dataset.addAgent")
    member _.removeAgent(value: Dataset, target: Agent) = session.Collection(value,"agents",target,false,"Dataset.removeAgent")
    member _.setCitations(value: Dataset, replacement: seq<ScholarlyArticle>) =
        session.Change(value,"citations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setCitations")
    member _.addCitation(value: Dataset, target: ScholarlyArticle) = session.Collection(value,"citations",target,true,"Dataset.addCitation")
    member _.removeCitation(value: Dataset, target: ScholarlyArticle) = session.Collection(value,"citations",target,false,"Dataset.removeCitation")
    member _.setProcesses(value: Dataset, replacement: seq<ARCBaseModel.Process>) =
        session.Change(value,"processes",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setProcesses")
    member _.addProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection(value,"processes",target,true,"Dataset.addProcess")
    member _.removeProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection(value,"processes",target,false,"Dataset.removeProcess")
    member _.setDescriptors(value: Dataset, replacement: seq<Descriptor>) =
        session.Change(value,"descriptors",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setDescriptors")
    member _.addDescriptor(value: Dataset, target: Descriptor) = session.Collection(value,"descriptors",target,true,"Dataset.addDescriptor")
    member _.removeDescriptor(value: Dataset, target: Descriptor) = session.Collection(value,"descriptors",target,false,"Dataset.removeDescriptor")
    member _.setAdditionalProperties(value: Dataset, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setAdditionalProperties")
    member _.addAdditionalProperty(value: Dataset, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Dataset.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Dataset, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Dataset.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Dataset, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Dataset.setAdditionalTypes")
    member _.movePart(child: Dataset, destination: Dataset) = session.Move(child,destination,"hasParts","Dataset.movePart")
    member _.moveProcess(proc: ARCBaseModel.Process, destination: Dataset) = session.Move(proc,destination,"processes","Dataset.moveProcess")
