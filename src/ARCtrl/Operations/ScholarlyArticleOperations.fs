namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type ScholarlyArticleOperations internal (session: Session) =
    member _.create(headline: string) = let value = ScholarlyArticle(headline) in session.Register(value,"ScholarlyArticle"); value
    member _.register(value: ScholarlyArticle) = session.Register(value,"ScholarlyArticle"); value
    member _.set(value: ScholarlyArticle): unit = session.Set(value,"ScholarlyArticle")
    member _.get(id: string) = session.Get<ScholarlyArticle>("ScholarlyArticle",id)
    member _.list() = session.List<ScholarlyArticle>("ScholarlyArticle")
    member _.delete(value: ScholarlyArticle) = session.Delete(value)
    member _.setHeadline(value: ScholarlyArticle, replacement: string) =
        session.Change(value,"headline",Some(Text(Model.required "headline" replacement)),"ScholarlyArticle.setHeadline")
    member _.setIdentifiers(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change(value,"identifiers",Some(Texts(List.ofSeq replacement)),"ScholarlyArticle.setIdentifiers")
    member _.setAuthors(value: ScholarlyArticle, replacement: seq<Agent>) =
        session.Change(value,"authors",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"ScholarlyArticle.setAuthors")
    member _.addAuthor(value: ScholarlyArticle, target: Agent) = session.Collection(value,"authors",target,true,"ScholarlyArticle.addAuthor")
    member _.removeAuthor(value: ScholarlyArticle, target: Agent) = session.Collection(value,"authors",target,false,"ScholarlyArticle.removeAuthor")
    member _.setCreativeWorkStatus(value: ScholarlyArticle, replacement: DefinedTerm) =
        session.Change(value,"creativeWorkStatus",Some(Links [session.Id(replacement)]),"ScholarlyArticle.setCreativeWorkStatus")
    member _.clearCreativeWorkStatus(value: ScholarlyArticle) = session.Change(value,"creativeWorkStatus",None,"ScholarlyArticle.clearCreativeWorkStatus")
    member _.setAdditionalProperties(value: ScholarlyArticle, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"ScholarlyArticle.setAdditionalProperties")
    member _.addAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"ScholarlyArticle.addAdditionalProperty")
    member _.removeAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"ScholarlyArticle.removeAdditionalProperty")
    member _.setAdditionalTypes(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"ScholarlyArticle.setAdditionalTypes")
