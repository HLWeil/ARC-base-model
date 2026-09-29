module BaseModelTasks

open BlackFox.Fake
open Fake.Core
open ProjectInfo

let private modelProject = "src/ARCBaseModel/ARCBaseModel.fsproj"
let private testsProject = "tests/ARCBaseModel.Tests/ARCBaseModel.Tests.fsproj"

let private command executable arguments =
    CreateProcess.fromRawCommand executable arguments
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let buildBaseModelDotNet = BuildTask.create "BuildBaseModelDotNet" [] {
    command "dotnet" ["build"; modelProject; "-c"; configuration; "--nologo"]
}

let testBaseModelDotNet = BuildTask.create "TestBaseModelDotNet" [buildBaseModelDotNet] {
    command "dotnet" ["run"; "--project"; testsProject; "-c"; configuration]
}
