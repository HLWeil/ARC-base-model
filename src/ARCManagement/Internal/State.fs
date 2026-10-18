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

type internal EntityRow = { Id: string; Kind: string; SuppliedId: string option; Properties: Map<string, Cell>; Extensions: Map<string, ExtensionCell> }
type internal State = { Root: string; Entities: EntityRow list }

/// A persisted change is limited to one property (or one collection splice).
/// SQL NULL in Before/After denotes absence; an encoded extension null is a value.
type internal StorageChange = {
    EntityId: string
    Domain: string
    Property: string
    Start: int option
    Before: string option
    After: string option
    Ordinal: int64 option
}
