module internal ARCtrl.Helper.WebRequest

let downloadFile (url: string) =
#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
    ARCtrl.Helper.WebRequestHelpers.NodeJs.downloadFile url
#else
#if FABLE_COMPILER_PYTHON
    ARCtrl.Helper.WebRequestHelpers.Py.downloadFile url
#else
    async {
        use client = new System.Net.Http.HttpClient()
        let! response = client.GetAsync(url) |> Async.AwaitTask
        use response = response
        let! text = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        if int response.StatusCode <> 200 then failwithf "Status %d => %s" (int response.StatusCode) text
        return text
    }
#endif
#endif
