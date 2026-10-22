namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ScholarlyArticleOperations internal (session: GraphContext) =
    member _.create(headline: string): ScholarlyArticle = session.Add<ScholarlyArticle>(ScholarlyArticle(headline), Some "ScholarlyArticle")
    member _.add(value: ScholarlyArticle): ScholarlyArticle = session.Add<ScholarlyArticle>(value, Some "ScholarlyArticle")
    member _.upsert(value: ScholarlyArticle, ?recursive: bool): ScholarlyArticle = session.Upsert<ScholarlyArticle>(value, Some "ScholarlyArticle", defaultArg recursive false)
    member _.get(id: string) = session.Get<ScholarlyArticle>("ScholarlyArticle",id)
    member _.list() = session.List<ScholarlyArticle>("ScholarlyArticle")
    member _.delete(value: ScholarlyArticle) = session.Delete(value)
    member _.setHeadline(value: ScholarlyArticle, replacement: string) =
        session.Change<ScholarlyArticle>(value,"headline",Some(Text(Model.required "headline" replacement)),"ScholarlyArticle.setHeadline")
    member _.setIdentifiers(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(value,"identifiers",Some(Texts(List.ofSeq replacement)),"ScholarlyArticle.setIdentifiers")
    member _.setAuthors(value: ScholarlyArticle, replacement: seq<Agent>) =
        session.Change<ScholarlyArticle>(value,"authors",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"ScholarlyArticle.setAuthors")
    member _.addAuthor(value: ScholarlyArticle, target: Agent) = session.Collection<ScholarlyArticle>(value,"authors",target,true,"ScholarlyArticle.addAuthor")
    member _.removeAuthor(value: ScholarlyArticle, target: Agent) = session.Collection<ScholarlyArticle>(value,"authors",target,false,"ScholarlyArticle.removeAuthor")
    member _.setCreativeWorkStatus(value: ScholarlyArticle, replacement: DefinedTerm option) =
        session.Change<ScholarlyArticle>(value,"creativeWorkStatus",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),"ScholarlyArticle.setCreativeWorkStatus")
    member _.setAdditionalProperties(value: ScholarlyArticle, replacement: seq<Annotation>) =
        session.Change<ScholarlyArticle>(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"ScholarlyArticle.setAdditionalProperties")
    member _.addAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection<ScholarlyArticle>(value,"additionalProperties",target,true,"ScholarlyArticle.addAdditionalProperty")
    member _.removeAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection<ScholarlyArticle>(value,"additionalProperties",target,false,"ScholarlyArticle.removeAdditionalProperty")
