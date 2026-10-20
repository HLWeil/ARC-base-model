namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<DefinedTerm>(DefinedTerm(name), "DefinedTerm", true,None,false)
    member _.add(value: DefinedTerm) = session.Adopt<DefinedTerm>(value, "DefinedTerm", true,None,false)
    member _.upsert(value: DefinedTerm, ?baseline: DefinedTerm, ?recursive: bool) = session.Adopt<DefinedTerm>(value, "DefinedTerm", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<DefinedTerm>("DefinedTerm",id)
    member _.list() = session.List<DefinedTerm>("DefinedTerm")
    member _.read(id: string) = session.Read<DefinedTerm>("DefinedTerm",id)
    member _.readAll() = session.ReadAll<DefinedTerm>("DefinedTerm")
    member _.delete(value: DefinedTerm, ?cascade: bool) = session.Delete(session.Id(value,"DefinedTerm"),"DefinedTerm",defaultArg cascade false)
    member _.setName(value: DefinedTerm, replacement: string) =
        session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"DefinedTerm.setName")
    member _.setIdentifier(value: DefinedTerm, replacement: string) =
        session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"identifier", (fun () -> Some(Text(Model.required "identifier" replacement))),"DefinedTerm.setIdentifier")
    member _.clearIdentifier(value: DefinedTerm) = session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"identifier", (fun () -> None),"DefinedTerm.clearIdentifier")
    member _.setTAN(value: DefinedTerm, replacement: string) =
        session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"tan", (fun () -> Some(Text(Model.required "tan" replacement))),"DefinedTerm.setTAN")
    member _.clearTAN(value: DefinedTerm) = session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"tan", (fun () -> None),"DefinedTerm.clearTAN")
    member _.clearInDefinedTermSet(value: DefinedTerm) = session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"inDefinedTermSet", (fun () -> None),"DefinedTerm.clearInDefinedTermSet")
    member _.setAdditionalTypes(value: DefinedTerm, replacement: seq<string>) =
        session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"DefinedTerm.setAdditionalTypes")
    member _.setInDefinedTermSetUrl(value: DefinedTerm, replacement: string) = session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"inDefinedTermSet", (fun () -> Some(Text(Model.required "url" replacement))),"DefinedTerm.setInDefinedTermSetUrl")
    member _.setInDefinedTermSetEntity(value: DefinedTerm, replacement: string) = session.Change<DefinedTerm>(session.Id(value,"DefinedTerm"),"inDefinedTermSet", (fun () -> Some(Links [replacement])),"DefinedTerm.setInDefinedTermSetEntity")
