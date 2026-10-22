namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermOperations internal (session: GraphContext) =
    member _.create(name: string): DefinedTerm = session.Add<DefinedTerm>(DefinedTerm(name), Some "DefinedTerm")
    member _.add(value: DefinedTerm): DefinedTerm = session.Add<DefinedTerm>(value, Some "DefinedTerm")
    member _.upsert(value: DefinedTerm, ?recursive: bool): DefinedTerm = session.Upsert<DefinedTerm>(value, Some "DefinedTerm", defaultArg recursive false)
    member _.get(id: string) = session.Get<DefinedTerm>("DefinedTerm",id)
    member _.list() = session.List<DefinedTerm>("DefinedTerm")
    member _.delete(value: DefinedTerm) = session.Delete(value)
    member _.setName(value: DefinedTerm, replacement: string) =
        session.Change<DefinedTerm>(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTerm.setName")
    member _.setIdentifier(value: DefinedTerm, replacement: string option) =
        session.Change<DefinedTerm>(value,"identifier",replacement |> Option.map (fun replacement -> Text(Model.required "identifier" replacement)),"DefinedTerm.setIdentifier")
    member _.setTAN(value: DefinedTerm, replacement: string option) =
        session.Change<DefinedTerm>(value,"tan",replacement |> Option.map (fun replacement -> Text(Model.required "tan" replacement)),"DefinedTerm.setTAN")
    member _.setInDefinedTermSet(value: DefinedTerm, replacement: DefinedTermSetReference option) =
        session.Change<DefinedTerm>(value,"inDefinedTermSet",replacement |> Option.map (fun replacement -> match replacement with DefinedTermSetReference.Url v -> Text(Model.required "inDefinedTermSet" v) | DefinedTermSetReference.TermSet v -> Links [session.Id(v)]),"DefinedTerm.setInDefinedTermSet")
