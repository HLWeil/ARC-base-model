namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type FormalParameterOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<FormalParameter>(revision, FormalParameter(name), "FormalParameter", true, false)
    member _.add(revision: RevisionToken, value: FormalParameter) = session.Adopt<FormalParameter>(revision, value, "FormalParameter", true, false)
    member _.upsert(revision: RevisionToken, value: FormalParameter, ?recursive: bool) = session.Adopt<FormalParameter>(revision, value, "FormalParameter", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<FormalParameter>("FormalParameter",id)
    member _.list() = session.List<FormalParameter>("FormalParameter")
    member _.read(id: string) = session.Read<FormalParameter>("FormalParameter",id)
    member _.readAll() = session.ReadAll<FormalParameter>("FormalParameter")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"FormalParameter",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<FormalParameter>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"FormalParameter.setName")
    member _.clearName(revision: RevisionToken, id: string) = session.Change<FormalParameter>(revision,id,"name", (fun () -> None),"FormalParameter.clearName")
    member _.setNameTAN(revision: RevisionToken, id: string, replacement: string) =
        session.Change<FormalParameter>(revision,id,"nameTAN", (fun () -> Some(Text(Model.required "nameTAN" replacement))),"FormalParameter.setNameTAN")
    member _.clearNameTAN(revision: RevisionToken, id: string) = session.Change<FormalParameter>(revision,id,"nameTAN", (fun () -> None),"FormalParameter.clearNameTAN")
    member _.setDefaultValue(revision: RevisionToken, id: string, replacement: string) =
        session.Change<FormalParameter>(revision,id,"defaultValue", (fun () -> Some(Links [replacement])),"FormalParameter.setDefaultValue")
    member _.clearDefaultValue(revision: RevisionToken, id: string) = session.Change<FormalParameter>(revision,id,"defaultValue", (fun () -> None),"FormalParameter.clearDefaultValue")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<FormalParameter>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"FormalParameter.setAdditionalTypes")
