module Build
open BlackFox.Fake
open System.IO
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools

open Helpers

initializeContext()

open BasicTasks
open TestTasks
open PackageTasks
open DocumentationTasks
open ReleaseTasks

/// Full release of nuget package for the prerelease version.
let _release = 
    BuildTask.createEmpty 
        "Release" 
        [clean; buildSolution; runTests; pack; createTag; publishNuget; publishNPM; publishPyPi]

let _docs = [buildDocs; watchDocs] |> ignore

let _baseModel = [BaseModelTasks.buildBaseModel; BaseModelTasks.testBaseModel] |> ignore

let _polyglotSQLite = [PolyglotSQLiteTasks.buildPolyglotSQLite; PolyglotSQLiteTasks.testPolyglotSQLite] |> ignore

let _managementPrototype = TestTasks.testManagementPrototype |> ignore

let _coreSQL = TestTasks.testCoreSQL |> ignore

ReleaseNotesTasks.updateReleaseNotes |> ignore
// PerformanceTasks.perforanceReport |> ignore

[<EntryPoint>]
let main args = 
    runOrDefault buildSolution args
