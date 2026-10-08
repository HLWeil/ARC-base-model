module internal ARCtrl.Helper.ResizeArray

/// Replace contents while preserving collection identity and self-inputs.
let replace (target: ResizeArray<'T>) (values: seq<'T>) =
    let snapshot = Seq.toArray values
    target.Clear()
    target.AddRange(snapshot)

let map (f : 'T -> 'U) (arr : ResizeArray<'T>) : ResizeArray<'U> =
    let result = ResizeArray<'U>(arr.Count)
    for item in arr do
        result.Add(f item)
    result
