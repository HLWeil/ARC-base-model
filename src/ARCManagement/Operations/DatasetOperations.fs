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
    member _.delete(value: Dataset) = session.Delete(value, Some "Dataset")
    member _.setConformsTo(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(value,Some "Dataset","conformsTo",Some(Texts(List.ofSeq replacement)),[],"Dataset.setConformsTo")
    member _.setIdentifiers(value: Dataset, replacement: seq<string>) =
        session.Change<Dataset>(value,Some "Dataset","identifiers",Some(Texts(List.ofSeq replacement)),[],"Dataset.setIdentifiers")
    member _.setTitle(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,Some "Dataset","title",replacement |> Option.map (fun replacement -> Text(Model.required "title" replacement)),[],"Dataset.setTitle")
    member _.setDescription(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,Some "Dataset","description",replacement |> Option.map (fun replacement -> Text(Model.required "description" replacement)),[],"Dataset.setDescription")
    member _.setLicense(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,Some "Dataset","license",replacement |> Option.map (fun replacement -> Text(Model.required "license" replacement)),[],"Dataset.setLicense")
    member _.setDatePublished(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,Some "Dataset","datePublished",replacement |> Option.map (fun replacement -> Text(Model.required "datePublished" replacement)),[],"Dataset.setDatePublished")
    member _.setDateCreated(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,Some "Dataset","dateCreated",replacement |> Option.map (fun replacement -> Text(Model.required "dateCreated" replacement)),[],"Dataset.setDateCreated")
    member _.setDateModified(value: Dataset, replacement: string option) =
        session.Change<Dataset>(value,Some "Dataset","dateModified",replacement |> Option.map (fun replacement -> Text(Model.required "dateModified" replacement)),[],"Dataset.setDateModified")
    member _.setHasParts(value: Dataset, replacement: seq<Dataset>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","hasParts",replacement |> Seq.map box,"Dataset.setHasParts")
    member _.addPart(value: Dataset, target: Dataset) = session.Collection<Dataset>(value,Some "Dataset","hasParts",target,true,"Dataset.addPart")
    member _.removePart(value: Dataset, target: Dataset) = session.Collection<Dataset>(value,Some "Dataset","hasParts",target,false,"Dataset.removePart")
    member _.setDataFiles(value: Dataset, replacement: seq<Data>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","dataFiles",replacement |> Seq.map box,"Dataset.setDataFiles")
    member _.addDataFile(value: Dataset, target: Data) = session.Collection<Dataset>(value,Some "Dataset","dataFiles",target,true,"Dataset.addDataFile")
    member _.removeDataFile(value: Dataset, target: Data) = session.Collection<Dataset>(value,Some "Dataset","dataFiles",target,false,"Dataset.removeDataFile")
    member _.setAgents(value: Dataset, replacement: seq<Agent>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","agents",replacement |> Seq.map box,"Dataset.setAgents")
    member _.addAgent(value: Dataset, target: Agent) = session.Collection<Dataset>(value,Some "Dataset","agents",target,true,"Dataset.addAgent")
    member _.removeAgent(value: Dataset, target: Agent) = session.Collection<Dataset>(value,Some "Dataset","agents",target,false,"Dataset.removeAgent")
    member _.setCitations(value: Dataset, replacement: seq<ScholarlyArticle>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","citations",replacement |> Seq.map box,"Dataset.setCitations")
    member _.addCitation(value: Dataset, target: ScholarlyArticle) = session.Collection<Dataset>(value,Some "Dataset","citations",target,true,"Dataset.addCitation")
    member _.removeCitation(value: Dataset, target: ScholarlyArticle) = session.Collection<Dataset>(value,Some "Dataset","citations",target,false,"Dataset.removeCitation")
    member _.setProcesses(value: Dataset, replacement: seq<ARCBaseModel.Process>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","processes",replacement |> Seq.map box,"Dataset.setProcesses")
    member _.addProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection<Dataset>(value,Some "Dataset","processes",target,true,"Dataset.addProcess")
    member _.removeProcess(value: Dataset, target: ARCBaseModel.Process) = session.Collection<Dataset>(value,Some "Dataset","processes",target,false,"Dataset.removeProcess")
    member _.setDescriptors(value: Dataset, replacement: seq<Descriptor>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","descriptors",replacement |> Seq.map box,"Dataset.setDescriptors")
    member _.addDescriptor(value: Dataset, target: Descriptor) = session.Collection<Dataset>(value,Some "Dataset","descriptors",target,true,"Dataset.addDescriptor")
    member _.removeDescriptor(value: Dataset, target: Descriptor) = session.Collection<Dataset>(value,Some "Dataset","descriptors",target,false,"Dataset.removeDescriptor")
    member _.setAdditionalProperties(value: Dataset, replacement: seq<Annotation>) =
        session.ChangeLinks<Dataset>(value,Some "Dataset","additionalProperties",replacement |> Seq.map box,"Dataset.setAdditionalProperties")
    member _.addAdditionalProperty(value: Dataset, target: Annotation) = session.Collection<Dataset>(value,Some "Dataset","additionalProperties",target,true,"Dataset.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Dataset, target: Annotation) = session.Collection<Dataset>(value,Some "Dataset","additionalProperties",target,false,"Dataset.removeAdditionalProperty")
