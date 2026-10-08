module internal ARCtrl.Helper.CrossAsync

// F# Async is supported by every target without a separate Promise package.
let inline crossAsync<'T> =
    async

type CrossAsync<'T> =
    Async<'T>

let tryPick (chooser : 'T -> CrossAsync<'U option>) (tasks : 'T seq) : CrossAsync<'U option> =
    let rec loop (en : System.Collections.Generic.IEnumerator<'T>) =
        crossAsync {
            if en.MoveNext() then
                let! r = chooser en.Current
                match r with
                | Some v -> return Some v
                | None -> return! loop en
            else return None
        }
    loop (tasks.GetEnumerator())

let choose (chooser : 'T -> CrossAsync<'U option>) (tasks : 'T seq) : CrossAsync<'U []> =
    let rec loop (en : System.Collections.Generic.IEnumerator<'T>) =
        crossAsync {
            if en.MoveNext() then
                let! r = chooser en.Current
                let! following = loop en
                match r with
                | Some v -> return Array.append [|v|] following
                | None -> return following
            else return [||]
        }
    loop (tasks.GetEnumerator())

let startSequential (starterF : 'T -> CrossAsync<'U>) (tasks : 'T seq) : CrossAsync<'U []> =
    let rec loop (en : System.Collections.Generic.IEnumerator<'T>) =
        crossAsync {
            if en.MoveNext() then
                let! r = starterF en.Current
                let! following = loop en
                return Array.append [|r|] following
            else return [||]
        }
    loop (tasks.GetEnumerator())


    

let all (tasks : CrossAsync<'T> seq) : CrossAsync<'T []> =
    Async.Sequential tasks

let map f v =
    crossAsync {
        let! v = v
        return f v
    }

let bind f v =
    crossAsync {
        let! v = v
        return! f v
    }

let asAsync (v:CrossAsync<'T>) =
    v

let catchWith (f : exn -> 'T) (p : CrossAsync<'T>) : CrossAsync<'T> =
    async {
        let! r = Async.Catch p
        match r with
        | Choice1Of2 x -> return x
        | Choice2Of2 e -> return f e
    }

    
let catchAsResult (p : CrossAsync<'T>) : CrossAsync<Result<'T,string>>=
    async {
        let! r = Async.Catch p
        match r with
        | Choice1Of2 x -> return Ok x
        | Choice2Of2 e -> return Error (e.ToString())
    }
