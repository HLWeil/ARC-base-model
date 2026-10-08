namespace ARCtrl

open System
open Fable.Core
open ARCBaseModel
open ARCtrl.Internal
open ARCSession.Internal

/// One managed ARC. SQL retains its full registry; save exports only Model.
[<AttachMembers>]
type ARC private (session: Session, ownsRepository: bool) =
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
    member _.ArcId = session.ArcId
    member _.FolderBinding = session.Folder
    member _.Folder = session.Folder |> Option.defaultWith (fun () -> invalidOp "ARC has no folder binding.")
    member _.bindFolder(folder: string) = Workspace.bind session folder
    member _.recoverFolder(source: string) = Workspace.recover session source
    member _.DatabasePath = session.DatabasePath
    member _.IsDirty = session.IsDirty
    member _.HasSessionOnlyObjects = session.HasSessionOnlyObjects
    member _.save() = Workspace.save session
    member _.close() =
        if not session.IsClosed then session.Repository.Access(session.Close)
        if ownsRepository then session.Repository.Close()
    static member internal Wrap(context: Session, ownsRepository: bool) =
        let arc = new ARC(context,ownsRepository)
        context.Repository.Track(context.ArcId,box arc,context.Close)
        arc
    static member create(folder: string, rootDataset: Dataset) =
        let folder = Files.fullPath(Model.required "folder" folder)
        Model.required "rootDataset" rootDataset |> ignore
        if Files.exists (Workspace.path folder) || Files.exists (Files.combine folder "arc.yml") then invalidOp "Workspace already exists. Use openFolder."
        Files.mkdir(Files.combine folder ".arc")
        let repository = Repository.CreateFile(Workspace.path folder)
        try
            let context = ArcFactory.create repository rootDataset
            Workspace.bind context folder
            ARC.Wrap(context,true)
        with _ ->
            repository.Close()
            Files.remove(Workspace.path folder)
            reraise()
    static member openFolder(folder: string, source: string) = ARC.Wrap(Workspace.openFolder folder source,true)
    /// The native declarations type this compile-order bridge as ARCSession.Session.
    static member importFolder(session: obj, folder: string) =
        let owner = unbox<ISessionOwner>(Model.required "session" session)
        ARC.Wrap(ArcFactory.import owner.Repository folder,false)
    interface IDisposable with member this.Dispose() = this.close()
