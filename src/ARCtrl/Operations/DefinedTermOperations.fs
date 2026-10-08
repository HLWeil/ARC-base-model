namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type DefinedTermOperations internal (session: Session) =
    member _.create(name: string) = let value = DefinedTerm(name) in session.Register(value,"DefinedTerm"); value
    member _.register(value: DefinedTerm) = session.Register(value,"DefinedTerm"); value
    member _.upsert(value: DefinedTerm): unit = session.Set(value,"DefinedTerm")
    member _.get(id: string) = session.Get<DefinedTerm>("DefinedTerm",id)
    member _.list() = session.List<DefinedTerm>("DefinedTerm")
    member _.delete(value: DefinedTerm) = session.Delete(value)
    member _.setName(value: DefinedTerm, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTerm.setName")
    member _.setIdentifier(value: DefinedTerm, replacement: string) =
        session.Change(value,"identifier",Some(Text(Model.required "identifier" replacement)),"DefinedTerm.setIdentifier")
    member _.clearIdentifier(value: DefinedTerm) = session.Change(value,"identifier",None,"DefinedTerm.clearIdentifier")
    member _.setTAN(value: DefinedTerm, replacement: string) =
        session.Change(value,"tan",Some(Text(Model.required "tan" replacement)),"DefinedTerm.setTAN")
    member _.clearTAN(value: DefinedTerm) = session.Change(value,"tan",None,"DefinedTerm.clearTAN")
    member _.setInDefinedTermSet(value: DefinedTerm, replacement: DefinedTermSetReference) =
        session.Change(value,"inDefinedTermSet",Some(match replacement with DefinedTermSetReference.Url v -> Text(Model.required "inDefinedTermSet" v) | DefinedTermSetReference.TermSet v -> Links [session.Id(v)]),"DefinedTerm.setInDefinedTermSet")
    member _.clearInDefinedTermSet(value: DefinedTerm) = session.Change(value,"inDefinedTermSet",None,"DefinedTerm.clearInDefinedTermSet")
    member _.setAdditionalTypes(value: DefinedTerm, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"DefinedTerm.setAdditionalTypes")
    member _.setInDefinedTermSetUrl(value: DefinedTerm, replacement: string) = session.Change(value,"inDefinedTermSet",Some(Text(Model.required "url" replacement)),"DefinedTerm.setInDefinedTermSetUrl")
    member _.setInDefinedTermSetEntity(value: DefinedTerm, replacement: DefinedTermSet) = session.Change(value,"inDefinedTermSet",Some(Links [session.Id(replacement)]),"DefinedTerm.setInDefinedTermSetEntity")
