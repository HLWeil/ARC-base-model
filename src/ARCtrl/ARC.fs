namespace ARCtrl

open System
open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

/// Experimental ARC session. SQL retains the full registry; save exports only Model.
[<AttachMembers>]
type ARC private (session: Session) =
    let entityOperations = EntityOperations(session)
    let datasetOperations = DatasetOperations(session)
    let processOperations = ProcessOperations(session)
    let sampleOperations = SampleOperations(session)
    let organizationOperations = OrganizationOperations(session)
    let agentOperations = AgentOperations(session)
    let scholarlyArticleOperations = ScholarlyArticleOperations(session)
    let annotationOperations = AnnotationOperations(session)
    let formalParameterOperations = FormalParameterOperations(session)
    let definedTermSetOperations = DefinedTermSetOperations(session)
    let definedTermOperations = DefinedTermOperations(session)
    let descriptorOperations = DescriptorOperations(session)
    let dataOperations = DataOperations(session)
    let recipeOperations = RecipeOperations(session)
    let historyOperations = HistoryOperations(session)
    member _.Model = session.Model
    member _.Entity = entityOperations
    member _.Dataset = datasetOperations
    member _.Process = processOperations
    member _.Sample = sampleOperations
    member _.Organization = organizationOperations
    member _.Agent = agentOperations
    member _.ScholarlyArticle = scholarlyArticleOperations
    member _.Annotation = annotationOperations
    member _.FormalParameter = formalParameterOperations
    member _.DefinedTermSet = definedTermSetOperations
    member _.DefinedTerm = definedTermOperations
    member _.Descriptor = descriptorOperations
    member _.Data = dataOperations
    member _.Recipe = recipeOperations
    member _.History = historyOperations
    member _.Folder = session.Folder
    member _.DatabasePath = session.DatabasePath
    member _.IsDirty = session.IsDirty
    member _.HasSessionOnlyObjects = session.HasSessionOnlyObjects
    member _.save() = session.Save()
    member _.close() = session.Close()
    static member create(folder: string, rootDataset: Dataset) =
        let folder = Files.fullPath folder
        Model.required "rootDataset" rootDataset |> ignore
        if Files.exists (Workspace.path folder) || Files.exists (Files.combine folder "arc.yml") then invalidOp "Workspace already exists. Use openFolder."
        // Validate/adopt the supplied graph with the same registration pipeline.
        let initialRoot = Dataset(rootDataset.ConformsTo, rootDataset.Identifiers)
        let rootId = Files.newId()
        let row = Model.capture rootId (fun _ -> invalidOp "Unexpected initial reference.") initialRoot
        let temporaryState = { Root = rootId; Entities = [row] }
        let session = Workspace.create folder temporaryState None
        try
            session.Register(rootDataset, "Dataset")
            session.InitializeRoot(rootDataset)
            new ARC(session)
        with _ ->
            session.Close()
            Files.remove (Workspace.path folder)
            reraise()
    static member openFolder(folder: string, source: string) = new ARC(Workspace.openFolder folder source)
    interface IDisposable with member this.Dispose() = this.close()
