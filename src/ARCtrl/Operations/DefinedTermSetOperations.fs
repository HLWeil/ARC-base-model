namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type DefinedTermSetOperations internal (session: Session) =
    member _.create(name: string) = let value = DefinedTermSet(name) in session.Register(value,"DefinedTermSet"); value
    member _.register(value: DefinedTermSet) = session.Register(value,"DefinedTermSet"); value
    member _.set(value: DefinedTermSet): unit = session.Set(value,"DefinedTermSet")
    member _.get(id: string) = session.Get<DefinedTermSet>("DefinedTermSet",id)
    member _.list() = session.List<DefinedTermSet>("DefinedTermSet")
    member _.delete(value: DefinedTermSet) = session.Delete(value)
    member _.setName(value: DefinedTermSet, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTermSet.setName")
    member _.setIdentifier(value: DefinedTermSet, replacement: string) =
        session.Change(value,"identifier",Some(Text(Model.required "identifier" replacement)),"DefinedTermSet.setIdentifier")
    member _.clearIdentifier(value: DefinedTermSet) = session.Change(value,"identifier",None,"DefinedTermSet.clearIdentifier")
    member _.setAdditionalTypes(value: DefinedTermSet, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"DefinedTermSet.setAdditionalTypes")
