namespace ARCManagement

open Fable.Core
open ARCManagement.Internal

[<AttachMembers>]
type HistoryOperations internal (session: GraphContext) =
    member _.CanUndo = session.CanUndo
    member _.CanRedo = session.CanRedo
    member _.undo() = session.History(false)
    member _.redo() = session.History(true)
