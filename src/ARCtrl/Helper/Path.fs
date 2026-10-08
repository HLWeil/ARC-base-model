module internal ARCtrl.Helper.Path

open System
open Fable.Core
open ARCtrl.Helper.CrossAsync

let [<Literal>] PathSeperator = '/'
let [<Literal>] PathSeperatorWindows = '\\'
let seperators = [|PathSeperator; PathSeperatorWindows|]

let split(path: string) =
    path.Split(seperators, enum<System.StringSplitOptions>(3))
    |> Array.filter (fun p -> p <> "" && p <> ".")

let combine (path1 : string) (path2 : string) : string =
    let path1_trimmed = path1.TrimEnd(seperators)
    let path2_trimmed = path2.TrimStart(seperators)
    let combined = path1_trimmed + string PathSeperator + path2_trimmed
    combined // should we trim any excessive path seperators?

let combineMany (paths : string seq) : string =
    paths
    |> Seq.filter (fun p -> not (System.String.IsNullOrWhiteSpace p))
    |> Seq.reduce combine


// Files
let [<Literal>] DatamapFileName = "isa.datamap.xlsx"
let [<Literal>] AssayFileName = "isa.assay.xlsx"
let [<Literal>] StudyFileName = "isa.study.xlsx"
let [<Literal>] WorkflowFileName = "isa.workflow.xlsx"
let [<Literal>] WorkflowCWLFileName = "workflow.cwl"
let [<Literal>] RunFileName = "isa.run.xlsx"
let [<Literal>] RunCWLFileName = "run.cwl"
let [<Literal>] RunYMLFileName = "run.yml"
let [<Literal>] InvestigationFileName = "isa.investigation.xlsx"
let [<Literal>] GitKeepFileName = ".gitkeep"
let [<Literal>] READMEFileName = "README.md"
let [<Literal>] ValidationPackagesYamlFileName = "validation_packages.yml"
let [<Literal>] LICENSEFileName = "LICENSE"
let alternativeLICENSEFileNames = ["LICENSE.txt"; "LICENSE.md"; "LICENSE.rst"]



// Folder
let [<Literal>] ARCConfigFolderName = ".arc"
let [<Literal>] ARCFileName = "arc.yml"
let [<Literal>] AssaysFolderName = "assays"
let [<Literal>] StudiesFolderName = "studies"
let [<Literal>] WorkflowsFolderName = "workflows"
let [<Literal>] RunsFolderName = "runs"
let [<Literal>] AssayProtocolsFolderName = "protocols"
let [<Literal>] AssayDatasetFolderName = "dataset"
let [<Literal>] StudiesProtocolsFolderName = "protocols"
let [<Literal>] StudiesResourcesFolderName = "resources"



// Native filesystem paths retain platform resolution and atomic replacement.
#if FABLE_COMPILER_PYTHON
[<Emit("str(__import__('pathlib').Path($0).resolve())")>]
let fullPath (_path: string) : string = nativeOnly
[<Emit("__import__('os').path.join($0, $1)")>]
let combineNative (_a: string) (_b: string) : string = nativeOnly
[<Emit("__import__('pathlib').Path($0).exists()")>]
let pathExists (_path: string) : bool = nativeOnly
[<Emit("__import__('pathlib').Path($0).mkdir(parents=True, exist_ok=True)")>]
let createDirectory (_path: string) : unit = nativeOnly
[<Emit("__import__('pathlib').Path($0).read_bytes().decode('utf-8')")>]
let readFileText (_path: string) : string = nativeOnly
[<Emit("__import__('pathlib').Path($0).write_bytes($1.encode('utf-8'))")>]
let writeFileText (_path: string) (_text: string) : unit = nativeOnly
[<Emit("__import__('os').replace($0, $1)")>]
let replaceFile (_source: string) (_destination: string) : unit = nativeOnly
[<Emit("__import__('pathlib').Path($0).touch(exist_ok=False)")>]
let createFileExclusive (_path: string) : unit = nativeOnly
[<Emit("__import__('os').remove($0)")>]
let deleteFile (_path: string) : unit = nativeOnly
#else
#if FABLE_COMPILER
[<Import("resolve", "node:path")>]
let fullPath (_path: string) : string = nativeOnly
[<Import("join", "node:path")>]
let combineNative (_a: string) (_b: string) : string = nativeOnly
[<Import("existsSync", "node:fs")>]
let pathExists (_path: string) : bool = nativeOnly
[<Import("mkdirSync", "node:fs")>]
let private makeDirectory (_path: string) (_options: obj) : unit = nativeOnly
[<Import("readFileSync", "node:fs")>]
let private readText (_path: string) (_encoding: string) : string = nativeOnly
[<Import("writeFileSync", "node:fs")>]
let private writeText (_path: string) (_text: string) (_encoding: string) : unit = nativeOnly
[<Import("renameSync", "node:fs")>]
let replaceFile (_source: string) (_destination: string) : unit = nativeOnly
[<Import("openSync", "node:fs")>]
let private openExclusive (_path: string) (_flags: string) : int = nativeOnly
[<Import("closeSync", "node:fs")>]
let private closeFile (_handle: int) : unit = nativeOnly
let createFileExclusive path = closeFile (openExclusive path "wx")
[<Import("unlinkSync", "node:fs")>]
let deleteFile (_path: string) : unit = nativeOnly
let createDirectory path = makeDirectory path {| recursive = true |}
let readFileText path = readText path "utf8"
let writeFileText path text = writeText path text "utf8"
#else
let fullPath path = IO.Path.GetFullPath(path)
let combineNative a b = IO.Path.Combine(a, b)
let pathExists path = IO.File.Exists(path) || IO.Directory.Exists(path)
let createDirectory path = IO.Directory.CreateDirectory(path) |> ignore
let readFileText path = IO.File.ReadAllText(path)
let writeFileText path text = IO.File.WriteAllText(path, text, Text.UTF8Encoding(false))
let replaceFile source destination =
    if IO.File.Exists(destination) then IO.File.Replace(source, destination, null)
    else IO.File.Move(source, destination)
let createFileExclusive path =
    use stream = new IO.FileStream(path, IO.FileMode.CreateNew, IO.FileAccess.Write, IO.FileShare.None)
    ()
let deleteFile path = IO.File.Delete(path)
#endif
#endif

let trim (path : string) : string =
    if path.StartsWith("./") then
        path.Replace("./","").Trim('/')
    else path.Trim('/')

/// Return the absolute path relative to the directoryPath
let makeRelative (directoryPath : string) (path : string) : string =
    if directoryPath = "." || directoryPath = "/" || directoryPath = "" then
        path
    else
        let directoryPath = trim directoryPath
        let path = trim path
        if path.StartsWith(directoryPath) then
            path.Substring(directoryPath.Length)
        else path



let standardizeSlashes (path : string) : string =
    path.Replace("\\","/")


let readFileTextOptional path = if pathExists path then Some(readFileText path) else None

#if FABLE_COMPILER_PYTHON
[<Emit("__import__('pathlib').Path($0).is_dir()")>]
let directoryExists (_path: string) : bool = nativeOnly
[<Emit("__import__('pathlib').Path($0).is_file()")>]
let fileExists (_path: string) : bool = nativeOnly
[<Emit("[str(p) for p in __import__('pathlib').Path($0).iterdir() if p.is_file()]")>]
let private subFiles (_path: string) : seq<string> = nativeOnly
let getSubFiles path = subFiles path |> Seq.toArray
[<Emit("[str(p) for p in __import__('pathlib').Path($0).iterdir() if p.is_dir()]")>]
let private subDirectories (_path: string) : seq<string> = nativeOnly
let getSubDirectories path = subDirectories path |> Seq.toArray
[<Emit("str(__import__('pathlib').Path($0).parent)")>]
let private parentDirectory (_path: string) : string = nativeOnly
[<Emit("__import__('pathlib').Path($0).read_bytes()")>]
let private readBytes (_path: string) : seq<byte> = nativeOnly
let readFileBinary path = readBytes path |> Seq.toArray
[<Emit("__import__('pathlib').Path($0).write_bytes(__import__('builtins').bytes($1))")>]
let writeFileBinary (_path: string) (_bytes: byte array) : unit = nativeOnly
[<Emit("__import__('shutil').rmtree($0)")>]
let deleteDirectory (_path: string) : unit = nativeOnly
[<Emit("__import__('shutil').move($0, $1)")>]
let moveFile (_source: string) (_destination: string) : unit = nativeOnly
let moveDirectory source destination = moveFile source destination
#else
#if FABLE_COMPILER
[<ImportAll("node:fs")>]
let private fs: obj = nativeOnly
let directoryExists (path: string) : bool =
    JsInterop.emitJsExpr (path, fs) "$1.existsSync($0) && $1.statSync($0).isDirectory()"
let fileExists (path: string) : bool =
    JsInterop.emitJsExpr (path, fs) "$1.existsSync($0) && $1.statSync($0).isFile()"
let getSubFiles (path: string) : string array =
    JsInterop.emitJsExpr (path, fs) "$1.readdirSync($0, {withFileTypes:true}).filter(e => e.isFile()).map(e => e.name)"
    |> Array.map (combineNative path)
let getSubDirectories (path: string) : string array =
    JsInterop.emitJsExpr (path, fs) "$1.readdirSync($0, {withFileTypes:true}).filter(e => e.isDirectory()).map(e => e.name)"
    |> Array.map (combineNative path)
[<Import("dirname", "node:path")>]
let private parentDirectory (_path: string) : string = nativeOnly
let readFileBinary (path: string) : byte array = JsInterop.emitJsExpr (path, fs) "$1.readFileSync($0)"
let writeFileBinary (path: string) (bytes: byte array) : unit =
    JsInterop.emitJsExpr (path, bytes, fs) "$2.writeFileSync($0, new Uint8Array($1))"
let deleteDirectory (path: string) : unit = JsInterop.emitJsExpr (path, fs) "$1.rmSync($0, {recursive:true})"
let moveFile source destination = replaceFile source destination
let moveDirectory source destination = replaceFile source destination
#else
let directoryExists path = IO.Directory.Exists(path)
let fileExists path = IO.File.Exists(path)
let getSubFiles path = IO.Directory.GetFiles(path)
let getSubDirectories path = IO.Directory.GetDirectories(path)
let private parentDirectory path = IO.Path.GetDirectoryName(path)
let readFileBinary path = IO.File.ReadAllBytes(path)
let writeFileBinary path bytes = IO.File.WriteAllBytes(path, bytes)
let deleteDirectory path = IO.Directory.Delete(path, true)
let moveFile source destination = IO.File.Move(source, destination)
let moveDirectory source destination = IO.Directory.Move(source, destination)
#endif
#endif

let appendFileText path text = writeFileText path ((readFileTextOptional path |> Option.defaultValue "") + text)
let readFileLines path = (readFileText path).Replace("\r", "").Split('\n') |> Seq.toArray
let ensureDirectoryOfFile path = createDirectory(parentDirectory path)

// Async conveniences share the same adapters instead of duplicating IO per target.
let directoryExistsAsync path = crossAsync { return directoryExists path }
let fileExistsAsync path = crossAsync { return fileExists path }
let createDirectoryAsync path = crossAsync { createDirectory path }
let ensureDirectoryAsync path = createDirectoryAsync path
let ensureDirectoryOfFileAsync path = crossAsync { ensureDirectoryOfFile path }
let readFileTextAsync path = crossAsync { return readFileText path }
let readFileBinaryAsync path = crossAsync { return readFileBinary path }
let writeFileTextAsync path text = crossAsync { writeFileText path text }
let writeFileBinaryAsync path bytes = crossAsync { writeFileBinary path bytes }
let moveFileAsync source destination = crossAsync { moveFile source destination }
let moveDirectoryAsync source destination = crossAsync { moveDirectory source destination }
let deleteFileAsync path = crossAsync { if fileExists path then deleteFile path }
let deleteDirectoryAsync path = crossAsync { if directoryExists path then deleteDirectory path }
let getSubFilesAsync path = crossAsync { return getSubFiles path |> Array.map standardizeSlashes }
let getSubDirectoriesAsync path = crossAsync { return getSubDirectories path |> Array.map standardizeSlashes }

let getAllFilePathsAsync directoryPath =
    let rec collect directory =
        seq {
            yield! getSubFiles directory
            for child in getSubDirectories directory do yield! collect child
        }
    crossAsync {
        let root = fullPath directoryPath
        return collect root |> Seq.map (makeRelative root >> standardizeSlashes) |> Seq.toArray
    }

let renameFileOrDirectoryAsync source destination =
    crossAsync {
        if fileExists source then moveFile source destination
        elif directoryExists source then moveDirectory source destination
    }

let deleteFileOrDirectoryAsync path =
    crossAsync {
        if fileExists path then deleteFile path
        elif directoryExists path then deleteDirectory path
    }
