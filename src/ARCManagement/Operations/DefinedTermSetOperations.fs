namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermSetOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<DefinedTermSet>(DefinedTermSet(name), "DefinedTermSet", true,None,false)
    member _.add(value: DefinedTermSet) = session.Adopt<DefinedTermSet>(value, "DefinedTermSet", true,None,false)
    member _.upsert(value: DefinedTermSet, ?baseline: DefinedTermSet, ?recursive: bool) = session.Adopt<DefinedTermSet>(value, "DefinedTermSet", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<DefinedTermSet>("DefinedTermSet",id)
    member _.list() = session.List<DefinedTermSet>("DefinedTermSet")
    member _.read(id: string) = session.Read<DefinedTermSet>("DefinedTermSet",id)
    member _.readAll() = session.ReadAll<DefinedTermSet>("DefinedTermSet")
    member _.delete(value: DefinedTermSet, ?cascade: bool) = session.Delete(session.Id(value,"DefinedTermSet"),"DefinedTermSet",defaultArg cascade false)
    member _.setName(value: DefinedTermSet, replacement: string) =
        session.Change<DefinedTermSet>(session.Id(value,"DefinedTermSet"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"DefinedTermSet.setName")
    member _.setIdentifier(value: DefinedTermSet, replacement: string) =
        session.Change<DefinedTermSet>(session.Id(value,"DefinedTermSet"),"identifier", (fun () -> Some(Text(Model.required "identifier" replacement))),"DefinedTermSet.setIdentifier")
    member _.clearIdentifier(value: DefinedTermSet) = session.Change<DefinedTermSet>(session.Id(value,"DefinedTermSet"),"identifier", (fun () -> None),"DefinedTermSet.clearIdentifier")
    member _.setAdditionalTypes(value: DefinedTermSet, replacement: seq<string>) =
        session.Change<DefinedTermSet>(session.Id(value,"DefinedTermSet"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"DefinedTermSet.setAdditionalTypes")
