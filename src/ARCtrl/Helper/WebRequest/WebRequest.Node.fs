module internal ARCtrl.Helper.WebRequestHelpers.NodeJs

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
open Fable.Core

[<Emit("typeof process !== 'undefined' && process.versions?.node != null")>]
let isNode () : bool = nativeOnly

[<Emit("(async () => { const response = await fetch($0); const text = await response.text(); if (response.status !== 200) throw new Error(`Status ${response.status} => ${text}`); return text; })()")>]
let private get (_url: string) : JS.Promise<string> = nativeOnly

let downloadFile url = get url |> Async.AwaitPromise
#endif
