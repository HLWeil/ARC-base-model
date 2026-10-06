module TestTasks

open System.IO
open BlackFox.Fake
open Fake.Core
open Fake.DotNet

open ProjectInfo
open BasicTasks

let skipTestsFlag = "--skipTests"

let failOnFocusFlag = "--fail-on-focused-tests"

[<Literal>]
let jsIOResultFolder = "./tests/TestingUtils/TestResults/js"

[<Literal>]
let pyIOResultFolder = "./tests/TestingUtils/TestResults/py"

let private runTool command args =
    if System.OperatingSystem.IsWindows() then
        CreateProcess.fromRawCommand "cmd.exe" (["/c"; command] @ args)
        |> Proc.run
    else
        CreateProcess.fromRawCommand command args
        |> Proc.run

let private runPyxpectoDotNet project =
    let args =
        [
            "--project"
            $"\"{project}\""
            "--configuration"
            configuration
            "--no-build"
        ]
        |> String.concat " "

    let result = DotNet.exec id "run" args

    if not result.OK then
        failwithf "Pyxpecto tests failed for %s" project

let runTestsJs = BuildTask.createFn "runTestsJS" [clean] (fun tp ->
    if tp.Context.Arguments |> List.exists (fun a -> a.ToLower() = skipTestsFlag.ToLower()) |> not then
        Trace.traceImportant "Start Js tests"
        // Setup test results directory after clean
        System.IO.Directory.CreateDirectory(jsIOResultFolder) |> ignore
        // transpile js files from fsharp code
        run dotnet $"fable {testProject} -o {testProject}/ts --lang ts -e fs.ts --nocache" ""
        // run mocha in target path to execute tests
        // "--timeout 20000" is used, because json schema validation takes a bit of time.
        // run node $"{allTestsProject}/js/Main.js" ""
        run npx $"vitest run --dir {testProject}/ts/" ""
    else
        Trace.traceImportant "Skipping Js tests"
)

let runTestsDotnet = BuildTask.createFn "runTestsDotnet" [clean; buildSolution] (fun tp ->
    if tp.Context.Arguments |> List.exists (fun a -> a.ToLower() = skipTestsFlag.ToLower()) |> not then
        Trace.traceImportant "Start .NET tests"
        let cmd =
            if tp.Context.AllExecutingTargets |> List.exists (fun t -> t.Name = failOnFocusFlag) then
                $"run {failOnFocusFlag}"
            else
                "run"
        let dotnetRun = run dotnet cmd
        dotnetRun testProject
    else
        Trace.traceImportant "Skipping .NET tests"
)

let runTestsPy = BuildTask.createFn "runTestsPy" [clean] (fun tp ->
    if tp.Context.Arguments |> List.exists (fun a -> a.ToLower() = skipTestsFlag.ToLower()) |> not then
        Trace.traceImportant "Start Python tests"
        // Setup test results directory after clean
        System.IO.Directory.CreateDirectory(pyIOResultFolder) |> ignore
        //transpile py files from fsharp code
        run dotnet $"fable {testProject} -o {testProject}/py --lang python --nocache" ""
        // run pyxpecto in target path to execute tests in python
        run uv $"run python {testProject}/py/main.py {failOnFocusFlag}" ""
    else
        Trace.traceImportant "Skipping Python tests"

)

let testManagementPrototype = BuildTask.createFn "TestManagementPrototype" [] (fun _ ->
    let result = DotNet.exec id "run" "--project tests/ManagementPrototype.Tests/ManagementPrototype.Tests.fsproj --configuration Release"
    if not result.OK then failwith "ARC management prototype tests failed."
)

let private command executable arguments =
    CreateProcess.fromRawCommand executable arguments
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

/// Core DDL checks reuse the prototype's existing SQL implementation and providers.
let testCoreSQL = BuildTask.create "TestCoreSQL" [testManagementPrototype; PolyglotSQLiteTasks.buildPolyglotSQLiteJS; PolyglotSQLiteTasks.buildPolyglotSQLitePy] {
    command "node" ["tests/ManagementPrototype.Tests/CoreSql.mjs"]
    CreateProcess.fromRawCommand "uv"
        ["run"; "--no-sync"; "--cache-dir"; "build/out/uv-cache"; "python"; "tests/ManagementPrototype.Tests/CoreSql.py"]
    |> CreateProcess.setEnvironmentVariable "PYTHONPATH" (Path.GetFullPath("build/out/polyglot-sqlite/python"))
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
}


let runTests =
    // TODO: add back python tests when FsSpreadsheet
    BuildTask.create "RunTests" [ clean; buildSolution; runTestsDotnet; (*runTestsPy;*) runTestsJs; BaseModelTasks.testBaseModel; PolyglotSQLiteTasks.testPolyglotSQLite; testCoreSQL ] {
    }
