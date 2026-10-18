namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<DefinedTerm>(revision, DefinedTerm(name), "DefinedTerm", true, false)
    member _.add(revision: RevisionToken, value: DefinedTerm) = session.Adopt<DefinedTerm>(revision, value, "DefinedTerm", true, false)
    member _.upsert(revision: RevisionToken, value: DefinedTerm, ?recursive: bool) = session.Adopt<DefinedTerm>(revision, value, "DefinedTerm", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<DefinedTerm>("DefinedTerm",id)
    member _.list() = session.List<DefinedTerm>("DefinedTerm")
    member _.read(id: string) = session.Read<DefinedTerm>("DefinedTerm",id)
    member _.readAll() = session.ReadAll<DefinedTerm>("DefinedTerm")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"DefinedTerm",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<DefinedTerm>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"DefinedTerm.setName")
    member _.setIdentifier(revision: RevisionToken, id: string, replacement: string) =
        session.Change<DefinedTerm>(revision,id,"identifier", (fun () -> Some(Text(Model.required "identifier" replacement))),"DefinedTerm.setIdentifier")
    member _.clearIdentifier(revision: RevisionToken, id: string) = session.Change<DefinedTerm>(revision,id,"identifier", (fun () -> None),"DefinedTerm.clearIdentifier")
    member _.setTAN(revision: RevisionToken, id: string, replacement: string) =
        session.Change<DefinedTerm>(revision,id,"tan", (fun () -> Some(Text(Model.required "tan" replacement))),"DefinedTerm.setTAN")
    member _.clearTAN(revision: RevisionToken, id: string) = session.Change<DefinedTerm>(revision,id,"tan", (fun () -> None),"DefinedTerm.clearTAN")
    member _.clearInDefinedTermSet(revision: RevisionToken, id: string) = session.Change<DefinedTerm>(revision,id,"inDefinedTermSet", (fun () -> None),"DefinedTerm.clearInDefinedTermSet")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<DefinedTerm>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"DefinedTerm.setAdditionalTypes")
    member _.setInDefinedTermSetUrl(revision: RevisionToken, id: string, replacement: string) = session.Change<DefinedTerm>(revision,id,"inDefinedTermSet", (fun () -> Some(Text(Model.required "url" replacement))),"DefinedTerm.setInDefinedTermSetUrl")
    member _.setInDefinedTermSetEntity(revision: RevisionToken, id: string, replacement: string) = session.Change<DefinedTerm>(revision,id,"inDefinedTermSet", (fun () -> Some(Links [replacement])),"DefinedTerm.setInDefinedTermSetEntity")
