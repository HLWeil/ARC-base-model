namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ScholarlyArticleOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, headline: string) = session.Adopt<ScholarlyArticle>(revision, ScholarlyArticle(headline), "ScholarlyArticle", true, false)
    member _.add(revision: RevisionToken, value: ScholarlyArticle) = session.Adopt<ScholarlyArticle>(revision, value, "ScholarlyArticle", true, false)
    member _.upsert(revision: RevisionToken, value: ScholarlyArticle, ?recursive: bool) = session.Adopt<ScholarlyArticle>(revision, value, "ScholarlyArticle", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<ScholarlyArticle>("ScholarlyArticle",id)
    member _.list() = session.List<ScholarlyArticle>("ScholarlyArticle")
    member _.read(id: string) = session.Read<ScholarlyArticle>("ScholarlyArticle",id)
    member _.readAll() = session.ReadAll<ScholarlyArticle>("ScholarlyArticle")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"ScholarlyArticle",defaultArg cascade false)
    member _.setHeadline(revision: RevisionToken, id: string, replacement: string) =
        session.Change<ScholarlyArticle>(revision,id,"headline", (fun () -> Some(Text(Model.required "headline" replacement))),"ScholarlyArticle.setHeadline")
    member _.setIdentifiers(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(revision,id,"identifiers", (fun () -> Some(Texts(List.ofSeq replacement))),"ScholarlyArticle.setIdentifiers")
    member _.setAuthors(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(revision,id,"authors", (fun () -> Some(Links(replacement |> List.ofSeq))),"ScholarlyArticle.setAuthors")
    member _.addAuthor(revision: RevisionToken, id: string, target: string) = session.Collection<ScholarlyArticle>(revision,id,"authors",target,true,"ScholarlyArticle.addAuthor")
    member _.removeAuthor(revision: RevisionToken, id: string, target: string) = session.Collection<ScholarlyArticle>(revision,id,"authors",target,false,"ScholarlyArticle.removeAuthor")
    member _.setCreativeWorkStatus(revision: RevisionToken, id: string, replacement: string) =
        session.Change<ScholarlyArticle>(revision,id,"creativeWorkStatus", (fun () -> Some(Links [replacement])),"ScholarlyArticle.setCreativeWorkStatus")
    member _.clearCreativeWorkStatus(revision: RevisionToken, id: string) = session.Change<ScholarlyArticle>(revision,id,"creativeWorkStatus", (fun () -> None),"ScholarlyArticle.clearCreativeWorkStatus")
    member _.setAdditionalProperties(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(revision,id,"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"ScholarlyArticle.setAdditionalProperties")
    member _.addAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<ScholarlyArticle>(revision,id,"additionalProperties",target,true,"ScholarlyArticle.addAdditionalProperty")
    member _.removeAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<ScholarlyArticle>(revision,id,"additionalProperties",target,false,"ScholarlyArticle.removeAdditionalProperty")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"ScholarlyArticle.setAdditionalTypes")
