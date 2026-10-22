namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermOperations internal (session: GraphContext) =
    member _.create(name: string) = let value = DefinedTerm(name) in session.Register(value,"DefinedTerm"); value
    member _.add(value: DefinedTerm) = session.Register(value,"DefinedTerm"); value
    member _.upsert(value: DefinedTerm): unit = session.Set(value,"DefinedTerm")
    member _.get(id: string) = session.Get<DefinedTerm>("DefinedTerm",id)
    member _.list() = session.List<DefinedTerm>("DefinedTerm")
    member _.delete(value: DefinedTerm) = session.Delete(value)
    member _.setName(value: DefinedTerm, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTerm.setName")
    member _.setIdentifier(value: DefinedTerm, replacement: string option) =
        session.Change(value,"identifier",replacement |> Option.map (fun replacement -> Text(Model.required "identifier" replacement)),"DefinedTerm.setIdentifier")
    member _.setTAN(value: DefinedTerm, replacement: string option) =
        session.Change(value,"tan",replacement |> Option.map (fun replacement -> Text(Model.required "tan" replacement)),"DefinedTerm.setTAN")
    member _.setInDefinedTermSet(value: DefinedTerm, replacement: DefinedTermSetReference option) =
        session.Change(value,"inDefinedTermSet",replacement |> Option.map (fun replacement -> match replacement with DefinedTermSetReference.Url v -> Text(Model.required "inDefinedTermSet" v) | DefinedTermSetReference.TermSet v -> Links [session.Id(v)]),"DefinedTerm.setInDefinedTermSet")
