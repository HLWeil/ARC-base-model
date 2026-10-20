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

/// Another writer committed while a command was being prepared. No changes were committed.
[<AttachMembers>]
type ConcurrentWriteException internal () =
    inherit System.Exception("Another writer changed this ARC during the command. Read current state before deciding whether to try again.")

/// A full replacement would overwrite a definition changed since its baseline.
[<AttachMembers>]
type SnapshotConflictException internal (entityId: string) =
    inherit System.Exception("The stored definition no longer matches the upsert baseline: " + entityId + ". Read it again and reapply the intended edits.")
    member _.EntityId = entityId

[<AttachMembers>]
type WriteResult<'T> internal (value: 'T, changed: bool, operation: AppliedOperation option) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Value = value
    member _.Changed = changed
    member _.Operation = operation

[<AttachMembers>]
type CommandResult internal (changed: bool, operation: AppliedOperation option) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Changed = changed
    member _.Operation = operation
