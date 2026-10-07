module BaseModelTasks

open BlackFox.Fake
open Fake.Core
open System
open System.IO
open ProjectInfo

let private modelProject = "src/ARCBaseModel/ARCBaseModel.fsproj"
let private testsProject = "tests/ARCBaseModel.Tests/ARCBaseModel.Tests.fsproj"
let private outputRoot = Path.GetFullPath("build/out/base-model")

let private command executable arguments =
    CreateProcess.fromRawCommand executable arguments
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let private python arguments =
    command "uv" (["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"] @ arguments)

// Only this target's generated subtree may be removed. Resolve before deletion,
// including on Windows, and leave other build outputs and source files alone.
let private cleanOutput directory =
    let path = Path.GetFullPath(Path.Combine(outputRoot, directory))
    let prefix = outputRoot + string Path.DirectorySeparatorChar
    if not (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) then
        invalidArg "directory" "Generated output must stay beneath build/out/base-model."
    if Directory.Exists(path) then Directory.Delete(path, true)
    Directory.CreateDirectory(path) |> ignore

let private transpile project language directory =
    cleanOutput directory
    command "dotnet" ["fable"; project; "--lang"; language; "--outDir"; Path.Combine(outputRoot, directory); "--noCache"]

let buildBaseModelDotNet = BuildTask.create "BuildBaseModelDotNet" [] {
    command "dotnet" ["build"; modelProject; "-c"; configuration; "--nologo"]
}

let testBaseModelDotNet = BuildTask.create "TestBaseModelDotNet" [buildBaseModelDotNet] {
    command "dotnet" ["run"; "--project"; testsProject; "-c"; configuration]
}

let buildBaseModelJS = BuildTask.create "BuildBaseModelJS" [] {
    transpile modelProject "ts" "ts"
    command "node" ["build/base-model.mjs"; "build"]
}

let buildBaseModelPy = BuildTask.create "BuildBaseModelPy" [] {
    transpile modelProject "py" "raw-python"
    cleanOutput "python"
    python ["build/base-model-python.py"; "build/out/base-model/raw-python"; "build/out/base-model/python/arc_base_model"]
}

let buildBaseModel =
    BuildTask.createEmpty "BuildBaseModel" [buildBaseModelDotNet; buildBaseModelJS; buildBaseModelPy]

let private buildBaseModelJSTests = BuildTask.create "BuildBaseModelJSTests" [buildBaseModelJS] {
    transpile testsProject "ts" "tests-ts"
    command "node" ["build/base-model.mjs"; "tests"]
}

let private buildBaseModelPyTests = BuildTask.create "BuildBaseModelPyTests" [buildBaseModelPy] {
    transpile testsProject "py" "tests-py"
    python ["build/base-model-python.py"; "compat"; "build/out/base-model/tests-py"]
    python ["build/base-model-python.py"; "tests"; "build/out/base-model/tests-py"; "build/out/base-model/python"]
}

let testBaseModelJS = BuildTask.create "TestBaseModelJS" [buildBaseModelJSTests] {
    command "node" ["build/out/base-model/js/tests.js"]
}

let testBaseModelPy = BuildTask.create "TestBaseModelPy" [buildBaseModelPyTests] {
    python ["build/out/base-model/tests-py/main.py"]
}

let testBaseModelNative = BuildTask.create "TestBaseModelNative" [buildBaseModelJSTests; buildBaseModelPyTests] {
    command "node" ["build/base-model.mjs"; "typecheck"]
    command "node" ["build/out/base-model/js/consumer/javascript.mjs"]
    CreateProcess.fromRawCommand "uv"
        ["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"; "tests/ARCBaseModel.Native/python.py"]
    |> CreateProcess.setEnvironmentVariable "PYTHONPATH" (Path.Combine(outputRoot, "python"))
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
}

let testBaseModelPacked = BuildTask.create "TestBaseModelPacked" [buildBaseModelJSTests; buildBaseModelPyTests] {
    python ["build/base-model-packed.py"]
}

let testBaseModel =
    BuildTask.createEmpty "TestBaseModel" [testBaseModelDotNet; testBaseModelJS; testBaseModelPy; testBaseModelNative; testBaseModelPacked]
