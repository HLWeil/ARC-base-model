namespace ARCBaseModel.Tests

open Fable.Core
open ARCBaseModel

/// Test-only column projections; entity columns stand in for references resolved by a repository.
[<AttachMembers>]
type AnnotationColumns(?text: string, ?number: float) =
    let mutable _text = text
    let mutable _number = number
    member _.Text with get() = _text and set(value) = _text <- value
    member _.Number with get() = _number and set(value) = _number <- value

[<AttachMembers>]
type EntityColumns(?sample: Sample, ?data: Data) =
    let mutable _sample = sample
    let mutable _data = data
    member _.Sample with get() = _sample and set(value) = _sample <- value
    member _.Data with get() = _data and set(value) = _data <- value

[<AttachMembers>]
type IntendedUseColumns(?text: string, ?term: DefinedTerm) =
    let mutable _text = text
    let mutable _term = term
    member _.Text with get() = _text and set(value) = _text <- value
    member _.Term with get() = _term and set(value) = _term <- value

[<AttachMembers>]
type TermSetColumns(?url: string, ?termSet: DefinedTermSet) =
    let mutable _url = url
    let mutable _termSet = termSet
    member _.Url with get() = _url and set(value) = _url <- value
    member _.TermSet with get() = _termSet and set(value) = _termSet <- value

/// Explicit mappings exercise F# pattern matching after erasure on each Fable backend.
[<AttachMembers>]
type MappingProbe() =
    // Different match forms must accept native numeric inputs after transpilation.
    static member IsNumber(value: AnnotationValue) =
        match value with
        | AnnotationValue.Number _ -> true
        | _ -> false

    static member IsText(value: AnnotationValue) =
        match value with
        | AnnotationValue.Text _ -> true
        | _ -> false

    static member ClassifyNumberFirst(value: AnnotationValue) =
        match value with
        | AnnotationValue.Number _ -> "number"
        | AnnotationValue.Text _ -> "text"

    static member CreateNumericAnnotation() =
        Annotation("generated temperature", value = AnnotationValue.Number 23.5)

    static member WriteAnnotation(value: AnnotationValue option) =
        match value with
        | Some(AnnotationValue.Text text) -> AnnotationColumns(text = text)
        | Some(AnnotationValue.Number number) -> AnnotationColumns(number = number)
        | None -> AnnotationColumns()

    static member ReadAnnotation(row: AnnotationColumns) =
        match row.Text, row.Number with
        | Some text, None -> Some(AnnotationValue.Text text)
        | None, Some number -> Some(AnnotationValue.Number number)
        | None, None -> None
        | Some _, Some _ -> invalidArg "row" "Annotation columns contain both alternatives."

    static member WriteEntity(value: EntityReference option) =
        match value with
        | Some(EntityReference.Sample sample) -> EntityColumns(sample = sample)
        | Some(EntityReference.Data data) -> EntityColumns(data = data)
        | None -> EntityColumns()

    static member ReadEntity(row: EntityColumns) =
        match row.Sample, row.Data with
        | Some sample, None -> Some(EntityReference.Sample sample)
        | None, Some data -> Some(EntityReference.Data data)
        | None, None -> None
        | Some _, Some _ -> invalidArg "row" "Entity columns contain both alternatives."

    static member WriteIntendedUse(value: RecipeIntendedUse option) =
        match value with
        | Some(RecipeIntendedUse.Text text) -> IntendedUseColumns(text = text)
        | Some(RecipeIntendedUse.Term term) -> IntendedUseColumns(term = term)
        | None -> IntendedUseColumns()

    static member ReadIntendedUse(row: IntendedUseColumns) =
        match row.Text, row.Term with
        | Some text, None -> Some(RecipeIntendedUse.Text text)
        | None, Some term -> Some(RecipeIntendedUse.Term term)
        | None, None -> None
        | Some _, Some _ -> invalidArg "row" "Intended-use columns contain both alternatives."

    static member WriteTermSet(value: DefinedTermSetReference option) =
        match value with
        | Some(DefinedTermSetReference.Url url) -> TermSetColumns(url = url)
        | Some(DefinedTermSetReference.TermSet termSet) -> TermSetColumns(termSet = termSet)
        | None -> TermSetColumns()

    static member ReadTermSet(row: TermSetColumns) =
        match row.Url, row.TermSet with
        | Some url, None -> Some(DefinedTermSetReference.Url url)
        | None, Some termSet -> Some(DefinedTermSetReference.TermSet termSet)
        | None, None -> None
        | Some _, Some _ -> invalidArg "row" "Term-set columns contain both alternatives."

    static member AssignSampleId(sample: Sample, id: string option) =
        sample.Id <- id
        sample

    static member ReadSampleId(sample: Sample) = sample.Id

    static member ClassifyExtension(value: Entity) =
        match value with
        | Entity.Number _ -> "number"
        | Entity.Bool _ -> "bool"
        | Entity.Text _ -> "text"
        | Entity.Object _ -> "object"
        | Entity.Collection _ -> "collection"
        | Entity.Null _ -> "null"
        | Entity.Blob _ -> "blob"

    static member IsExtensionNumber(value: Entity) =
        match value with
        | Entity.Number _ -> true
        | _ -> false

    static member FillExtensionNumbers(target: EntityObject, values: EntityCollection) =
        target.SetEntityProperty("wrapped", Entity.Number 1.25)
        values.Add(Entity.Number 2.5)
