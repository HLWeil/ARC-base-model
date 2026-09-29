module PolyglotSQLiteTasks

open BlackFox.Fake
open Fake.Core
open System
open System.IO
open ProjectInfo

let private libraryProject = "src/PolyglotSQLite/PolyglotSQLite.fsproj"
let private testsProject = "tests/PolyglotSQLite.Tests/PolyglotSQLite.Tests.fsproj"
let private outputRoot = Path.GetFullPath("build/out/polyglot-sqlite")

let private command executable arguments =
    CreateProcess.fromRawCommand executable arguments
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let private python arguments =
    command "uv" (["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"] @ arguments)

let private pythonWithPackages packageRoot arguments =
    CreateProcess.fromRawCommand "uv"
        (["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"] @ arguments)
    |> CreateProcess.setEnvironmentVariable "PYTHONPATH" packageRoot
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

// Resolve and check every deletion. No other generated or tracked subtree is owned here.
let private cleanOutput directory =
    let path = Path.GetFullPath(Path.Combine(outputRoot, directory))
    if not (path.StartsWith(outputRoot + string Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) then
        invalidArg "directory" "Generated output must stay beneath build/out/polyglot-sqlite."
    if Directory.Exists(path) then Directory.Delete(path, true)
    Directory.CreateDirectory(path) |> ignore

let private transpile project language directory =
    cleanOutput directory
    command "dotnet" ["fable"; project; "--lang"; language; "--outDir"; Path.Combine(outputRoot, directory); "--noCache"]

let buildPolyglotSQLiteDotNet = BuildTask.create "BuildPolyglotSQLiteDotNet" [] {
    command "dotnet" ["build"; libraryProject; "-c"; configuration; "--nologo"]
}

let testPolyglotSQLiteDotNet = BuildTask.create "TestPolyglotSQLiteDotNet" [buildPolyglotSQLiteDotNet] {
    command "dotnet" ["run"; "--project"; testsProject; "-c"; configuration]
}

let buildPolyglotSQLiteJS = BuildTask.create "BuildPolyglotSQLiteJS" [] {
    transpile libraryProject "ts" "ts"
    command "node" ["build/polyglot-sqlite.mjs"; "build"]
}

let buildPolyglotSQLitePy = BuildTask.create "BuildPolyglotSQLitePy" [] {
    transpile libraryProject "py" "raw-python"
    cleanOutput "python"
    python ["build/polyglot-sqlite-python.py"; "build"; "build/out/polyglot-sqlite/raw-python"; "build/out/polyglot-sqlite/python"]
}

let buildPolyglotSQLite =
    BuildTask.createEmpty "BuildPolyglotSQLite" [buildPolyglotSQLiteDotNet; buildPolyglotSQLiteJS; buildPolyglotSQLitePy]

let private buildPolyglotSQLiteJSTests = BuildTask.create "BuildPolyglotSQLiteJSTests" [buildPolyglotSQLiteJS] {
    transpile testsProject "ts" "tests-ts"
    command "node" ["build/polyglot-sqlite.mjs"; "tests"]
}

let private buildPolyglotSQLitePyTests = BuildTask.create "BuildPolyglotSQLitePyTests" [buildPolyglotSQLitePy] {
    transpile testsProject "py" "tests-py"
    python ["build/polyglot-sqlite-python.py"; "tests"; "build/out/polyglot-sqlite/tests-py"; "build/out/polyglot-sqlite/python"]
}

let testPolyglotSQLiteJS = BuildTask.create "TestPolyglotSQLiteJS" [buildPolyglotSQLiteJSTests] {
    command "node" ["build/out/polyglot-sqlite/js/tests.js"]
}

let testPolyglotSQLitePy = BuildTask.create "TestPolyglotSQLitePy" [buildPolyglotSQLitePyTests] {
    pythonWithPackages (Path.Combine(outputRoot, "python")) ["build/out/polyglot-sqlite/tests-py/main.py"]
}

let testPolyglotSQLiteNative = BuildTask.create "TestPolyglotSQLiteNative" [buildPolyglotSQLiteJSTests; buildPolyglotSQLitePyTests] {
    command "node" ["build/polyglot-sqlite.mjs"; "typecheck"]
    command "node" ["build/out/polyglot-sqlite/js/consumer/javascript.mjs"]
    pythonWithPackages (Path.Combine(outputRoot, "python")) ["tests/PolyglotSQLite.Native/python.py"]
    command "node" ["build/polyglot-sqlite.mjs"; "pack-test"]
    python ["build/polyglot-sqlite-python.py"; "pack-test"; "build/out/polyglot-sqlite/python"]
}

let testPolyglotSQLiteInterop = BuildTask.create "TestPolyglotSQLiteInterop"
                                                [testPolyglotSQLiteDotNet; buildPolyglotSQLiteJSTests; buildPolyglotSQLitePyTests] {
    cleanOutput "interop"
    let runners =
        [ "dotnet", (fun arguments -> command "dotnet" (["run"; "--project"; testsProject; "-c"; configuration; "--no-build"; "--"] @ arguments))
          "javascript", (fun arguments -> command "node" (["build/out/polyglot-sqlite/js/tests.js"] @ arguments))
          "python", (fun arguments -> pythonWithPackages (Path.Combine(outputRoot, "python")) (["build/out/polyglot-sqlite/tests-py/main.py"] @ arguments)) ]
    for writerName, writeFixture in runners do
        let fixture = Path.Combine(outputRoot, "interop", writerName + ".sqlite")
        writeFixture ["--write-fixture"; fixture]
        for readerName, readFixture in runners do
            Trace.tracefn "Reading %s SQLite fixture with %s" writerName readerName
            readFixture ["--read-fixture"; fixture]
}

let testPolyglotSQLite =
    BuildTask.createEmpty "TestPolyglotSQLite"
        [testPolyglotSQLiteDotNet; testPolyglotSQLiteJS; testPolyglotSQLitePy; testPolyglotSQLiteNative; testPolyglotSQLiteInterop]
