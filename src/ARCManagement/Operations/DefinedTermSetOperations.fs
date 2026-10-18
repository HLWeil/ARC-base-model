namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermSetOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<DefinedTermSet>(revision, DefinedTermSet(name), "DefinedTermSet", true, false)
    member _.add(revision: RevisionToken, value: DefinedTermSet) = session.Adopt<DefinedTermSet>(revision, value, "DefinedTermSet", true, false)
    member _.upsert(revision: RevisionToken, value: DefinedTermSet, ?recursive: bool) = session.Adopt<DefinedTermSet>(revision, value, "DefinedTermSet", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<DefinedTermSet>("DefinedTermSet",id)
    member _.list() = session.List<DefinedTermSet>("DefinedTermSet")
    member _.read(id: string) = session.Read<DefinedTermSet>("DefinedTermSet",id)
    member _.readAll() = session.ReadAll<DefinedTermSet>("DefinedTermSet")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"DefinedTermSet",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<DefinedTermSet>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"DefinedTermSet.setName")
    member _.setIdentifier(revision: RevisionToken, id: string, replacement: string) =
        session.Change<DefinedTermSet>(revision,id,"identifier", (fun () -> Some(Text(Model.required "identifier" replacement))),"DefinedTermSet.setIdentifier")
    member _.clearIdentifier(revision: RevisionToken, id: string) = session.Change<DefinedTermSet>(revision,id,"identifier", (fun () -> None),"DefinedTermSet.clearIdentifier")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<DefinedTermSet>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"DefinedTermSet.setAdditionalTypes")
