module internal ARCtrl.Helper.WebRequestHelpers.Py

#if FABLE_COMPILER_PYTHON
open Fable.Core

[<Emit("__import__('urllib.request', fromlist=['urlopen']).urlopen($0)")>]
let private openResponse (_url: string) : obj = nativeOnly
[<Emit("$0.read().decode($0.headers.get_content_charset() or 'utf-8')")>]
let private readResponse (_response: obj) : string = nativeOnly
[<Emit("$0.getcode()")>]
let private responseStatus (_response: obj) : int = nativeOnly
[<Emit("$0.close()")>]
let private closeResponse (_response: obj) : unit = nativeOnly

let downloadFile url =
    async {
        let response = openResponse url
        try
            let text = readResponse response
            let status = responseStatus response
            if status <> 200 then failwithf "Status %d => %s" status text
            return text
        finally closeResponse response
    }
#endif
