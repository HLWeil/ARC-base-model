namespace ARCBaseModel

/// Construction helpers without identity, normalization, or validation policies.
module internal Construction =
    let required name value =
        if isNull (box value) then nullArg name
        value

    let collection (values: seq<'T> option) =
        match values with
        | Some items -> ResizeArray<'T>(items)
        | None -> ResizeArray<'T>()

    let copy (values: ResizeArray<'T>) = ResizeArray<'T>(values)
