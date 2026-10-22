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
    member _.list(): seq<ScholarlyArticle> = session.List<ScholarlyArticle>("ScholarlyArticle")
    member _.delete(value: ScholarlyArticle) = session.Delete(value, Some "ScholarlyArticle")
    member _.setHeadline(value: ScholarlyArticle, replacement: string) =
        session.Change<ScholarlyArticle>(value,Some "ScholarlyArticle","headline",Some(Text(Model.required "headline" replacement)),[],"ScholarlyArticle.setHeadline")
    member _.setIdentifiers(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(value,Some "ScholarlyArticle","identifiers",Some(Texts(List.ofSeq replacement)),[],"ScholarlyArticle.setIdentifiers")
    member _.setAuthors(value: ScholarlyArticle, replacement: seq<Agent>) =
        session.ChangeLinks<ScholarlyArticle>(value,Some "ScholarlyArticle","authors",replacement |> Seq.map box,"ScholarlyArticle.setAuthors")
    member _.addAuthor(value: ScholarlyArticle, target: Agent) = session.Collection<ScholarlyArticle>(value,Some "ScholarlyArticle","authors",target,true,"ScholarlyArticle.addAuthor")
    member _.removeAuthor(value: ScholarlyArticle, target: Agent) = session.Collection<ScholarlyArticle>(value,Some "ScholarlyArticle","authors",target,false,"ScholarlyArticle.removeAuthor")
    member _.setCreativeWorkStatus(value: ScholarlyArticle, replacement: DefinedTerm option) =
        session.Change<ScholarlyArticle>(value,Some "ScholarlyArticle","creativeWorkStatus",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),(replacement |> Option.map box |> Option.toList),"ScholarlyArticle.setCreativeWorkStatus")
    member _.setAdditionalProperties(value: ScholarlyArticle, replacement: seq<Annotation>) =
        session.ChangeLinks<ScholarlyArticle>(value,Some "ScholarlyArticle","additionalProperties",replacement |> Seq.map box,"ScholarlyArticle.setAdditionalProperties")
    member _.addAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection<ScholarlyArticle>(value,Some "ScholarlyArticle","additionalProperties",target,true,"ScholarlyArticle.addAdditionalProperty")
    member _.removeAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection<ScholarlyArticle>(value,Some "ScholarlyArticle","additionalProperties",target,false,"ScholarlyArticle.removeAdditionalProperty")
