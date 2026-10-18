namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DatasetOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, identifier: string) = session.Adopt<Dataset>(revision, Dataset(["process-provenance"], [Model.required "identifier" identifier]), "Dataset", true, false)
    member _.add(revision: RevisionToken, value: Dataset) = session.Adopt<Dataset>(revision, value, "Dataset", true, false)
    member _.upsert(revision: RevisionToken, value: Dataset, ?recursive: bool) = session.Adopt<Dataset>(revision, value, "Dataset", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Dataset>("Dataset",id)
    member _.list() = session.List<Dataset>("Dataset")
    member _.read(id: string) = session.Read<Dataset>("Dataset",id)
    member _.readAll() = session.ReadAll<Dataset>("Dataset")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Dataset",defaultArg cascade false)
    member _.setConformsTo(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"conformsTo", (fun () -> Some(Texts(List.ofSeq replacement))),"Dataset.setConformsTo")
    member _.setIdentifiers(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"identifiers", (fun () -> Some(Texts(List.ofSeq replacement))),"Dataset.setIdentifiers")
    member _.setTitle(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Dataset>(revision,id,"title", (fun () -> Some(Text(Model.required "title" replacement))),"Dataset.setTitle")
    member _.clearTitle(revision: RevisionToken, id: string) = session.Change<Dataset>(revision,id,"title", (fun () -> None),"Dataset.clearTitle")
    member _.setDescription(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Dataset>(revision,id,"description", (fun () -> Some(Text(Model.required "description" replacement))),"Dataset.setDescription")
    member _.clearDescription(revision: RevisionToken, id: string) = session.Change<Dataset>(revision,id,"description", (fun () -> None),"Dataset.clearDescription")
    member _.setLicense(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Dataset>(revision,id,"license", (fun () -> Some(Text(Model.required "license" replacement))),"Dataset.setLicense")
    member _.clearLicense(revision: RevisionToken, id: string) = session.Change<Dataset>(revision,id,"license", (fun () -> None),"Dataset.clearLicense")
    member _.setDatePublished(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Dataset>(revision,id,"datePublished", (fun () -> Some(Text(Model.required "datePublished" replacement))),"Dataset.setDatePublished")
    member _.clearDatePublished(revision: RevisionToken, id: string) = session.Change<Dataset>(revision,id,"datePublished", (fun () -> None),"Dataset.clearDatePublished")
    member _.setDateCreated(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Dataset>(revision,id,"dateCreated", (fun () -> Some(Text(Model.required "dateCreated" replacement))),"Dataset.setDateCreated")
    member _.clearDateCreated(revision: RevisionToken, id: string) = session.Change<Dataset>(revision,id,"dateCreated", (fun () -> None),"Dataset.clearDateCreated")
    member _.setDateModified(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Dataset>(revision,id,"dateModified", (fun () -> Some(Text(Model.required "dateModified" replacement))),"Dataset.setDateModified")
    member _.clearDateModified(revision: RevisionToken, id: string) = session.Change<Dataset>(revision,id,"dateModified", (fun () -> None),"Dataset.clearDateModified")
    member _.setHasParts(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"hasParts", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setHasParts")
    member _.addPart(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"hasParts",target,true,"Dataset.addPart")
    member _.removePart(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"hasParts",target,false,"Dataset.removePart")
    member _.setDataFiles(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"dataFiles", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setDataFiles")
    member _.addDataFile(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"dataFiles",target,true,"Dataset.addDataFile")
    member _.removeDataFile(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"dataFiles",target,false,"Dataset.removeDataFile")
    member _.setAgents(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"agents", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setAgents")
    member _.addAgent(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"agents",target,true,"Dataset.addAgent")
    member _.removeAgent(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"agents",target,false,"Dataset.removeAgent")
    member _.setCitations(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"citations", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setCitations")
    member _.addCitation(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"citations",target,true,"Dataset.addCitation")
    member _.removeCitation(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"citations",target,false,"Dataset.removeCitation")
    member _.setProcesses(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"processes", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setProcesses")
    member _.addProcess(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"processes",target,true,"Dataset.addProcess")
    member _.removeProcess(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"processes",target,false,"Dataset.removeProcess")
    member _.setDescriptors(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"descriptors", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setDescriptors")
    member _.addDescriptor(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"descriptors",target,true,"Dataset.addDescriptor")
    member _.removeDescriptor(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"descriptors",target,false,"Dataset.removeDescriptor")
    member _.setAdditionalProperties(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Dataset.setAdditionalProperties")
    member _.addAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"additionalProperties",target,true,"Dataset.addAdditionalProperty")
    member _.removeAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Dataset>(revision,id,"additionalProperties",target,false,"Dataset.removeAdditionalProperty")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Dataset>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Dataset.setAdditionalTypes")
    member _.movePart(revision: RevisionToken, childId: string, destinationId: string) = session.Move<Dataset>(revision,childId,destinationId,"hasParts","Dataset.movePart")
    member _.moveProcess(revision: RevisionToken, processId: string, destinationId: string) = session.Move<ARCBaseModel.Process>(revision,processId,destinationId,"processes","Dataset.moveProcess")
