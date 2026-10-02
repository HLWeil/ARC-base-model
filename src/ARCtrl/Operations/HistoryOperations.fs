namespace ARCtrl

open Fable.Core
open ARCtrl.Internal

[<AttachMembers>]
type HistoryOperations internal (session: Session) =
    member _.CanUndo = session.CanUndo
    member _.CanRedo = session.CanRedo
    member _.undo() = session.History(false)
    member _.redo() = session.History(true)
