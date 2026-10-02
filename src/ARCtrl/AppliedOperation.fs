namespace ARCtrl

open Fable.Core

/// A committed command; undo/redo act on the session history rather than this value.
[<AttachMembers>]
type AppliedOperation internal (id: string, kind: string) =
    member _.Id = id
    member _.Kind = kind
