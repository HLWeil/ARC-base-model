namespace ARCManagement

open System
open Fable.Core
open ARCManagement.Internal

/// One retained editable graph. The owning Session controls the connection.
[<AttachMembers>]
type ManagedGraph private (context: GraphContext) =
    let entityOperations = EntityOperations(context)
    let datasetOperations = DatasetOperations(context)
    let processOperations = ProcessOperations(context)
    let sampleOperations = SampleOperations(context)
    let organizationOperations = OrganizationOperations(context)
    let agentOperations = AgentOperations(context)
    let scholarlyArticleOperations = ScholarlyArticleOperations(context)
    let annotationOperations = AnnotationOperations(context)
    let formalParameterOperations = FormalParameterOperations(context)
    let definedTermSetOperations = DefinedTermSetOperations(context)
    let definedTermOperations = DefinedTermOperations(context)
    let descriptorOperations = DescriptorOperations(context)
    let dataOperations = DataOperations(context)
    let recipeOperations = RecipeOperations(context)
    let historyOperations = HistoryOperations(context)
    member internal _.Context = context
    member _.Model = context.Model
    member _.readModel() = context.ReadModel()
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
    member _.ArcId = context.ArcId
    member _.DatabasePath = context.DatabasePath
    member _.HasSessionOnlyObjects = context.HasSessionOnlyObjects
    member _.toYaml() =
        context.Export()
    member _.close() =
        if not context.IsClosed then context.Repository.Access(context.Close)
    static member internal Wrap(context: GraphContext) =
        let graph = new ManagedGraph(context)
        context.Repository.Track(context.ArcId,box graph,context.Close)
        graph
    interface IDisposable with member this.Dispose() = this.close()
