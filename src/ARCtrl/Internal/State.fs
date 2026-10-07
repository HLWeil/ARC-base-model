namespace ARCtrl.Internal

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

type internal EntityRow = { Id: string; Kind: string; SuppliedId: string option; Properties: Map<string, Cell>; Extensions: Map<string, ExtensionCell> }
type internal State = { Root: string; Entities: EntityRow list }
