namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermSetOperations internal (session: GraphContext) =
    member _.create(name: string) = let value = DefinedTermSet(name) in session.Register(value,"DefinedTermSet"); value
    member _.add(value: DefinedTermSet) = session.Register(value,"DefinedTermSet"); value
    member _.upsert(value: DefinedTermSet): unit = session.Set(value,"DefinedTermSet")
    member _.get(id: string) = session.Get<DefinedTermSet>("DefinedTermSet",id)
    member _.list() = session.List<DefinedTermSet>("DefinedTermSet")
    member _.delete(value: DefinedTermSet) = session.Delete(value)
    member _.setName(value: DefinedTermSet, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTermSet.setName")
    member _.setIdentifier(value: DefinedTermSet, replacement: string option) =
        session.Change(value,"identifier",replacement |> Option.map (fun replacement -> Text(Model.required "identifier" replacement)),"DefinedTermSet.setIdentifier")
