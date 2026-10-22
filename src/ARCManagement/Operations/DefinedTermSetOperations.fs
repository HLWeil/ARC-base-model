namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type DefinedTermSetOperations internal (session: GraphContext) =
    member _.create(name: string): DefinedTermSet = session.Add<DefinedTermSet>(DefinedTermSet(name), Some "DefinedTermSet")
    member _.add(value: DefinedTermSet): DefinedTermSet = session.Add<DefinedTermSet>(value, Some "DefinedTermSet")
    member _.upsert(value: DefinedTermSet, ?recursive: bool): DefinedTermSet = session.Upsert<DefinedTermSet>(value, Some "DefinedTermSet", defaultArg recursive false)
    member _.get(id: string) = session.Get<DefinedTermSet>("DefinedTermSet",id)
    member _.list(): seq<DefinedTermSet> = session.List<DefinedTermSet>("DefinedTermSet")
    member _.delete(value: DefinedTermSet) = session.Delete(value)
    member _.setName(value: DefinedTermSet, replacement: string) =
        session.Change<DefinedTermSet>(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTermSet.setName")
    member _.setIdentifier(value: DefinedTermSet, replacement: string option) =
        session.Change<DefinedTermSet>(value,"identifier",replacement |> Option.map (fun replacement -> Text(Model.required "identifier" replacement)),"DefinedTermSet.setIdentifier")
