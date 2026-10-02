namespace ARCtrl.Internal

type internal DatasetRow = {
    Id: string; Types: string list; Profiles: string list; Identifiers: string list
    Title: string option; Description: string option; License: string option
    Published: string option; Created: string option; Modified: string option
    Parts: string list; Processes: string list
}
type internal ProcessRow = { Id: string; Types: string list; Name: string; Input: string option; Output: string option }
type internal SampleRow = { Id: string; Types: string list; Name: string }
type internal State = { Root: string; Datasets: DatasetRow list; Processes: ProcessRow list; Samples: SampleRow list }
