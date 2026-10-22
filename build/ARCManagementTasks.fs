module ARCManagementTasks

open BlackFox.Fake
open Fake.Core
open System
open System.IO
open ProjectInfo

let private outputRoot = Path.GetFullPath("build/out/arc-management")
let private command executable arguments =
    CreateProcess.fromRawCommand executable arguments
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
let private python arguments =
    command "uv" (["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"] @ arguments)
let private pythonWithPackages arguments =
    CreateProcess.fromRawCommand "uv"
        (["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"] @ arguments)
    |> CreateProcess.setEnvironmentVariable "PYTHONPATH" (Path.Combine(outputRoot,"python"))
    |> CreateProcess.setEnvironmentVariable "PYTHONUNBUFFERED" "1"
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
let private transpile project language directory =
    let path = Path.GetFullPath(Path.Combine(outputRoot,directory))
    if not (path.StartsWith(outputRoot + string Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) then
        invalidArg "directory" "Generated output must stay beneath build/out/arc-management."
    if Directory.Exists(path) then Directory.Delete(path,true)
    Directory.CreateDirectory(path) |> ignore
    command "dotnet" ["fable"; project; "--lang"; language; "--outDir"; path; "--noCache"]

let testARCManagementDotNet = BuildTask.create "TestARCManagementDotNet" [] {
    command "dotnet" ["run"; "--project"; "tests/ARCManagement.Tests/ARCManagement.Tests.fsproj"; "-c"; configuration]
}
let private buildJS = BuildTask.create "BuildARCManagementJS" [BaseModelTasks.buildBaseModelJS; PolyglotSQLiteTasks.buildPolyglotSQLiteJS] {
    transpile "tests/ARCManagement.Tests/ARCManagement.Tests.fsproj" "ts" "tests-ts"
    command "node" ["build/arc-management.mjs"; "build"]
}
let private buildPy = BuildTask.create "BuildARCManagementPy" [BaseModelTasks.buildBaseModelPy; PolyglotSQLiteTasks.buildPolyglotSQLitePy] {
    transpile "src/ARCManagement/ARCManagement.fsproj" "py" "raw-python"
    python ["build/arc-management-python.py"; "build"]
}
let private buildPyTests = BuildTask.create "BuildARCManagementPyTests" [buildPy] {
    transpile "tests/ARCManagement.Tests/ARCManagement.Tests.fsproj" "py" "tests-py"
    python ["build/arc-management-python.py"; "tests"]
}
let testARCManagementJS = BuildTask.create "TestARCManagementJS" [buildJS] {
    command "node" ["build/out/arc-management/js/tests.js"]
}
let testARCManagementPy = BuildTask.create "TestARCManagementPy" [buildPyTests] {
    pythonWithPackages ["build/out/arc-management/tests-py/main.py"]
}
let testARCManagementNative = BuildTask.create "TestARCManagementNative" [buildJS;buildPy] {
    command "node" ["build/arc-management.mjs"; "typecheck"]
    command "node" ["build/out/arc-management/js/consumer/Native.js"]
    command "node" ["build/out/arc-management/js/consumer/Native.mjs"]
    pythonWithPackages ["tests/ARCManagement.Tests/Native.py"]
    command "node" ["build/arc-management.mjs"; "pack-test"]
    python ["build/arc-management-python.py"; "pack-test"]
}
let testARCManagementInterop = BuildTask.create "TestARCManagementInterop" [testARCManagementDotNet;buildJS;buildPyTests] {
    let folder = Path.Combine(outputRoot,"interop")
    if Directory.Exists(folder) then Directory.Delete(folder,true)
    Directory.CreateDirectory(folder) |> ignore
    let runners =
        [ "dotnet", (fun arguments -> command "dotnet" (["run";"--project";"tests/ARCManagement.Tests/ARCManagement.Tests.fsproj";"-c";configuration;"--no-build";"--"] @ arguments))
          "javascript", (fun arguments -> command "node" (["build/out/arc-management/js/tests.js"] @ arguments))
          "python", (fun arguments -> pythonWithPackages (["build/out/arc-management/tests-py/main.py"] @ arguments)) ]
    for writerName,writeFixture in runners do
        let fixture = Path.Combine(folder,writerName + ".sqlite")
        writeFixture ["--write-fixture";fixture]
        for readerName,readFixture in runners do
            Trace.tracefn "Reading %s ARC fixture with %s" writerName readerName
            readFixture ["--read-fixture";fixture]
}
let testARCManagement =
    BuildTask.createEmpty "TestARCManagement"
        [testARCManagementDotNet;testARCManagementJS;testARCManagementPy;testARCManagementNative;testARCManagementInterop]
