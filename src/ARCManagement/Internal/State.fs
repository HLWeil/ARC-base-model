namespace ARCManagement.Internal

/// Internal snapshots keep scalar alternatives and ordered references explicit.
/// They are session data, separate from the core SQL persistence profile.
type internal Cell =
    | Text of string
    | Number of float
    | Texts of string list
    | Links of string list

type internal ExtensionCell =
    | ExtensionText of string
    | ExtensionNumber of float
    | ExtensionBool of bool
    | ExtensionNull
    | ExtensionBlob of string
    | ExtensionObject of string
    | ExtensionCollection of ExtensionCell list

type internal EntityRow = { Id: string; Kind: string; Properties: Map<string, Cell>; Extensions: Map<string, ExtensionCell> }
type internal State = { Root: string; Entities: EntityRow list }

/// One entity that differs between two states. A missing side means the entity does not exist there.
type internal Change = { EntityId: string; Ordinal: int64; Before: EntityRow option; After: EntityRow option }
