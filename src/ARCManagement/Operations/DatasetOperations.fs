namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DatasetOperations internal (session: GraphContext) =
    member _.create(identifier: string): Dataset =
        session.Add<Dataset>(Dataset(["process-provenance"], [Model.required "identifier" identifier]), Some "Dataset")
    member _.add(value: Dataset): Dataset = session.Add<Dataset>(value, Some "Dataset")
    member _.upsert(value: Dataset, ?recursive: bool): Dataset = session.Upsert<Dataset>(value, Some "Dataset", defaultArg recursive false)
    member _.get(id: string) = session.Get<Dataset>("Dataset",id)
    member _.list(): seq<Dataset> = session.List<Dataset>("Dataset")
    member _.delete(value: Dataset) = session.Delete(value)
    member _.setConformsTo(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(value,"conformsTo",Some(Texts(List.ofSeq replacement)),"Dataset.setConformsTo")
    member _.setIdentifiers(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(value,"identifiers",Some(Texts(List.ofSeq replacement)),"Dataset.setIdentifiers")
    member _.setTitle(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,"title",replacement |> Option.map (fun replacement -> Text(Model.required "title" replacement)),"Dataset.setTitle")
    member _.setDescription(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,"description",replacement |> Option.map (fun replacement -> Text(Model.required "description" replacement)),"Dataset.setDescription")
    member _.setLicense(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,"license",replacement |> Option.map (fun replacement -> Text(Model.required "license" replacement)),"Dataset.setLicense")
    member _.setDatePublished(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,"datePublished",replacement |> Option.map (fun replacement -> Text(Model.required "datePublished" replacement)),"Dataset.setDatePublished")
    member _.setDateCreated(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,"dateCreated",replacement |> Option.map (fun replacement -> Text(Model.required "dateCreated" replacement)),"Dataset.setDateCreated")
    member _.setDateModified(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,"dateModified",replacement |> Option.map (fun replacement -> Text(Model.required "dateModified" replacement)),"Dataset.setDateModified")
    member _.setHasParts(value: Dataset, replacement: seq<Dataset>) =
        session.Change<Dataset>(value,"hasParts",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setHasParts")
    member _.addPart(value: Dataset, target: Dataset) = session.Collection<Dataset>(value,"hasParts",target,true,"Dataset.addPart")
    member _.removePart(value: Dataset, target: Dataset) = session.Collection<Dataset>(value,"hasParts",target,false,"Dataset.removePart")
    member _.setDataFiles(value: Dataset, replacement: seq<Data>) =
        session.Change<Dataset>(value,"dataFiles",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setDataFiles")
    member _.addDataFile(value: Dataset, target: Data) = session.Collection<Dataset>(value,"dataFiles",target,true,"Dataset.addDataFile")
    member _.removeDataFile(value: Dataset, target: Data) = session.Collection<Dataset>(value,"dataFiles",target,false,"Dataset.removeDataFile")
    member _.setAgents(value: Dataset, replacement: seq<Agent>) =
        session.Change<Dataset>(value,"agents",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setAgents")
    member _.addAgent(value: Dataset, target: Agent) = session.Collection<Dataset>(value,"agents",target,true,"Dataset.addAgent")
    member _.removeAgent(value: Dataset, target: Agent) = session.Collection<Dataset>(value,"agents",target,false,"Dataset.removeAgent")
    member _.setCitations(value: Dataset, replacement: seq<ScholarlyArticle>) =
        session.Change<Dataset>(value,"citations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setCitations")
    member _.addCitation(value: Dataset, target: ScholarlyArticle) = session.Collection<Dataset>(value,"citations",target,true,"Dataset.addCitation")
    member _.removeCitation(value: Dataset, target: ScholarlyArticle) = session.Collection<Dataset>(value,"citations",target,false,"Dataset.removeCitation")
    member _.setProcesses(value: Dataset, replacement: seq<ARCBaseModel.Process>) =
        session.Change<Dataset>(value,"processes",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setProcesses")
    member _.addProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection<Dataset>(value,"processes",target,true,"Dataset.addProcess")
    member _.removeProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection<Dataset>(value,"processes",target,false,"Dataset.removeProcess")
    member _.setDescriptors(value: Dataset, replacement: seq<Descriptor>) =
        session.Change<Dataset>(value,"descriptors",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setDescriptors")
    member _.addDescriptor(value: Dataset, target: Descriptor) = session.Collection<Dataset>(value,"descriptors",target,true,"Dataset.addDescriptor")
    member _.removeDescriptor(value: Dataset, target: Descriptor) = session.Collection<Dataset>(value,"descriptors",target,false,"Dataset.removeDescriptor")
    member _.setAdditionalProperties(value: Dataset, replacement: seq<Annotation>) =
        session.Change<Dataset>(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Dataset.setAdditionalProperties")
    member _.addAdditionalProperty(value: Dataset, target: Annotation) = session.Collection<Dataset>(value,"additionalProperties",target,true,"Dataset.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Dataset, target: Annotation) = session.Collection<Dataset>(value,"additionalProperties",target,false,"Dataset.removeAdditionalProperty")
