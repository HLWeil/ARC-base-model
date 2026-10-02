namespace ARCtrl.Internal

open System
open Fable.Core

module internal Files =
#if FABLE_COMPILER_PYTHON
    [<Emit("str(__import__('pathlib').Path($0).resolve())")>]
    let fullPath (_path: string) : string = nativeOnly
    [<Emit("__import__('os').path.join($0, $1)")>]
    let combine (_a: string) (_b: string) : string = nativeOnly
    [<Emit("__import__('pathlib').Path($0).exists()")>]
    let exists (_path: string) : bool = nativeOnly
    [<Emit("__import__('pathlib').Path($0).mkdir(parents=True, exist_ok=True)")>]
    let mkdir (_path: string) : unit = nativeOnly
    [<Emit("__import__('pathlib').Path($0).read_text(encoding='utf-8')")>]
    let read (_path: string) : string = nativeOnly
    [<Emit("__import__('pathlib').Path($0).write_text($1, encoding='utf-8')")>]
    let write (_path: string) (_text: string) : unit = nativeOnly
    [<Emit("__import__('os').replace($0, $1)")>]
    let replace (_source: string) (_destination: string) : unit = nativeOnly
    [<Emit("__import__('os').remove($0)")>]
    let remove (_path: string) : unit = nativeOnly
#else
#if FABLE_COMPILER
    [<Import("resolve", "node:path")>]
    let fullPath (_path: string) : string = nativeOnly
    [<Import("join", "node:path")>]
    let combine (_a: string) (_b: string) : string = nativeOnly
    [<Import("existsSync", "node:fs")>]
    let exists (_path: string) : bool = nativeOnly
    [<Import("mkdirSync", "node:fs")>]
    let private makeDirectory (_path: string) (_options: obj) : unit = nativeOnly
    [<Import("readFileSync", "node:fs")>]
    let private readText (_path: string) (_encoding: string) : string = nativeOnly
    [<Import("writeFileSync", "node:fs")>]
    let private writeText (_path: string) (_text: string) (_encoding: string) : unit = nativeOnly
    [<Import("renameSync", "node:fs")>]
    let replace (_source: string) (_destination: string) : unit = nativeOnly
    [<Import("unlinkSync", "node:fs")>]
    let remove (_path: string) : unit = nativeOnly
    let mkdir path = makeDirectory path {| recursive = true |}
    let read path = readText path "utf8"
    let write path text = writeText path text "utf8"
#else
    let fullPath path = IO.Path.GetFullPath(path)
    let combine a b = IO.Path.Combine(a, b)
    let exists path = IO.File.Exists(path)
    let mkdir path = IO.Directory.CreateDirectory(path) |> ignore
    let read path = IO.File.ReadAllText(path)
    let write path text = IO.File.WriteAllText(path, text, Text.UTF8Encoding(false))
    let replace source destination =
        if IO.File.Exists(destination) then IO.File.Replace(source, destination, null)
        else IO.File.Move(source, destination)
    let remove path = IO.File.Delete(path)
#endif
#endif
    let readOptional path = if exists path then Some(read path) else None
    let newId () = Guid.NewGuid().ToString("N")
