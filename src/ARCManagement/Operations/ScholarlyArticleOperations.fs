namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type ScholarlyArticleOperations internal (session: GraphContext) =
    member _.create(headline: string) = session.Adopt<ScholarlyArticle>(ScholarlyArticle(headline), "ScholarlyArticle", true,None,false)
    member _.add(value: ScholarlyArticle) = session.Adopt<ScholarlyArticle>(value, "ScholarlyArticle", true,None,false)
    member _.upsert(value: ScholarlyArticle, ?baseline: ScholarlyArticle, ?recursive: bool) = session.Adopt<ScholarlyArticle>(value, "ScholarlyArticle", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<ScholarlyArticle>("ScholarlyArticle",id)
    member _.list() = session.List<ScholarlyArticle>("ScholarlyArticle")
    member _.read(id: string) = session.Read<ScholarlyArticle>("ScholarlyArticle",id)
    member _.readAll() = session.ReadAll<ScholarlyArticle>("ScholarlyArticle")
    member _.delete(value: ScholarlyArticle, ?cascade: bool) = session.Delete(session.Id(value,"ScholarlyArticle"),"ScholarlyArticle",defaultArg cascade false)
    member _.setHeadline(value: ScholarlyArticle, replacement: string) =
        session.Change<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"headline", (fun () -> Some(Text(Model.required "headline" replacement))),"ScholarlyArticle.setHeadline")
    member _.setIdentifiers(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"identifiers", (fun () -> Some(Texts(List.ofSeq replacement))),"ScholarlyArticle.setIdentifiers")
    member _.setAuthors(value: ScholarlyArticle, replacement: seq<Agent>) =
        session.Change<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"authors", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Agent")) |> List.ofSeq))),"ScholarlyArticle.setAuthors")
    member _.addAuthor(value: ScholarlyArticle, target: Agent) = session.Collection<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"authors",target,"Agent",true,"ScholarlyArticle.addAuthor")
    member _.removeAuthor(value: ScholarlyArticle, target: Agent) = session.Collection<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"authors",target,"Agent",false,"ScholarlyArticle.removeAuthor")
    member _.setCreativeWorkStatus(value: ScholarlyArticle, replacement: DefinedTerm) =
        session.Reference<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"creativeWorkStatus",replacement,"DefinedTerm","ScholarlyArticle.setCreativeWorkStatus")
    member _.clearCreativeWorkStatus(value: ScholarlyArticle) = session.Change<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"creativeWorkStatus", (fun () -> None),"ScholarlyArticle.clearCreativeWorkStatus")
    member _.setAdditionalProperties(value: ScholarlyArticle, replacement: seq<Annotation>) =
        session.Change<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"additionalProperties", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"ScholarlyArticle.setAdditionalProperties")
    member _.addAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"additionalProperties",target,"Annotation",true,"ScholarlyArticle.addAdditionalProperty")
    member _.removeAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"additionalProperties",target,"Annotation",false,"ScholarlyArticle.removeAdditionalProperty")
    member _.setAdditionalTypes(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change<ScholarlyArticle>(session.Id(value,"ScholarlyArticle"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"ScholarlyArticle.setAdditionalTypes")
