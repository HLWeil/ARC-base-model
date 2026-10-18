namespace ARCManagement

open Fable.Core
open ARCBaseModel

/// A committed command; undo/redo act on the session history rather than this value.
[<AttachMembers>]
type AppliedOperation internal (id: string, kind: string, ?action:string) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Id = id
    member _.Kind = kind
    member _.Action = defaultArg action "apply"

/// An opaque optimistic-concurrency token belonging to one open graph.
[<AttachMembers>]
type RevisionToken internal (scope: string, arcId: string, revision: int64) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Value = arcId + ":" + scope + ":" + string revision
    member internal _.Scope = scope
    member internal _.ArcId = arcId
    member internal _.Number = revision

[<AttachMembers>]
type RevisionConflictException internal (expected: RevisionToken, current: RevisionToken) =
    inherit System.Exception("The ARC revision changed. Read current state before editing again.")
    member _.Expected = expected
    member _.Current = current

[<AttachMembers>]
type ReadResult<'T> internal (value: 'T, revision: RevisionToken) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Value = value
    member _.Revision = revision

[<AttachMembers>]
type WriteResult<'T> internal (value: 'T, revision: RevisionToken, changed: bool, operation: AppliedOperation option) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Value = value
    member _.Revision = revision
    member _.Changed = changed
    member _.Operation = operation

[<AttachMembers>]
type CommandResult internal (revision: RevisionToken, changed: bool, operation: AppliedOperation option) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Revision = revision
    member _.Changed = changed
    member _.Operation = operation
