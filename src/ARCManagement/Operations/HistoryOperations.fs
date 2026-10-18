namespace ARCManagement

open Fable.Core
open ARCManagement.Internal

[<AttachMembers>]
type HistoryOperations internal (session: GraphContext) =
    member _.CanUndo = session.CanUndo
    member _.CanRedo = session.CanRedo
    member _.undo(revision: RevisionToken) = session.History(revision,false)
    member _.redo(revision: RevisionToken) = session.History(revision,true)
