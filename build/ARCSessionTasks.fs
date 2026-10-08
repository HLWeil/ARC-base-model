module ARCSessionTasks

open BlackFox.Fake
open Fake.Core
open System
open System.IO
open ProjectInfo

let private outputRoot = Path.GetFullPath("build/out/arc-session")
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
        invalidArg "directory" "Generated output must stay beneath build/out/arc-session."
    if Directory.Exists(path) then Directory.Delete(path,true)
    Directory.CreateDirectory(path) |> ignore
    command "dotnet" ["fable"; project; "--lang"; language; "--outDir"; path; "--noCache"]

let testARCSessionDotNet = BuildTask.create "TestARCSessionDotNet" [] {
    command "dotnet" ["run"; "--project"; "tests/ManagementPrototype.Tests/ManagementPrototype.Tests.fsproj"; "-c"; configuration]
}
let private buildJS = BuildTask.create "BuildARCSessionJS" [BaseModelTasks.buildBaseModelJS; PolyglotSQLiteTasks.buildPolyglotSQLiteJS] {
    transpile "tests/ManagementPrototype.Tests/ManagementPrototype.Tests.fsproj" "ts" "tests-ts"
    command "node" ["build/arc-session.mjs"; "build"]
}
let private buildPy = BuildTask.create "BuildARCSessionPy" [BaseModelTasks.buildBaseModelPy; PolyglotSQLiteTasks.buildPolyglotSQLitePy] {
    transpile "src/ARCtrl/ARCtrl.fsproj" "py" "raw-python"
    python ["build/arc-session-python.py"; "build"]
}
let private buildPyTests = BuildTask.create "BuildARCSessionPyTests" [buildPy] {
    transpile "tests/ManagementPrototype.Tests/ManagementPrototype.Tests.fsproj" "py" "tests-py"
    python ["build/arc-session-python.py"; "tests"]
}
let testARCSessionJS = BuildTask.create "TestARCSessionJS" [buildJS] {
    command "node" ["build/out/arc-session/js/tests.js"]
}
let testARCSessionPy = BuildTask.create "TestARCSessionPy" [buildPyTests] {
    pythonWithPackages ["build/out/arc-session/tests-py/main.py"]
}
let testARCSessionNative = BuildTask.create "TestARCSessionNative" [buildJS;buildPy] {
    command "node" ["build/arc-session.mjs"; "typecheck"]
    command "node" ["build/out/arc-session/js/consumer/Native.js"]
    command "node" ["build/out/arc-session/js/consumer/Native.mjs"]
    pythonWithPackages ["tests/ManagementPrototype.Tests/Native.py"]
    command "node" ["build/arc-session.mjs"; "pack-test"]
    python ["build/arc-session-python.py"; "pack-test"]
}
let testARCSession =
    BuildTask.createEmpty "TestARCSession"
        [testARCSessionDotNet;testARCSessionJS;testARCSessionPy;testARCSessionNative]
