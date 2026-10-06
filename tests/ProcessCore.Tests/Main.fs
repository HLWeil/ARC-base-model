module ProcessCore.Tests.Main

open Fable.Pyxpecto
open Fable.Core

#if !FABLE_COMPILER_JAVASCRIPT && !FABLE_COMPILER_TYPESCRIPT
let inline (!!) value = value
#endif

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT
open Fable.Core.JS
open Fable.Core.JsInterop
#endif

let all =
    testList
        "ProcessCore"
        [
            ProcessCore.WebRequest.tests
            Integration.tests
            Types.Annotation.tests
            Types.DefinedTerm.tests
            Types.FormalParameter.tests
            Types.Administrative.tests
            Types.Sample.tests
            Types.Data.tests
            Types.Recipe.tests
            Types.Process.tests
            Types.Dataset.tests
            Types.IONode.tests
            Graph.BackEdges.tests
            Graph.Deduplication.tests
            Graph.Traversal.tests
            Graph.FragmentSelectors.tests
            Graph.AnnotationSources.tests
            Graph.DatasetQueries.tests
            Graph.PathQueries.tests
            Table.CompositeTypes.tests
            Table.TableAux.tests
            Table.TableRead.tests
            Table.TableWrite.tests
            Table.TablesApi.tests
            Spreadsheet.Workbooks.tests
            Spreadsheet.Scaffold.tests
            ProcessCore.Yaml.Tests.All.all
            ARC.tests
            WorkspaceProject.tests
        ]

#if FABLE_COMPILER_JAVASCRIPT || FABLE_COMPILER_TYPESCRIPT

[<Emit("$1.then($0)")>]
let map (a: 'T1 -> 'T2) (pr: JS.Promise<'T1>): JS.Promise<'T2> = jsNative

[<Import("it", from = "vitest")>]
let itAsync(name: string, test: unit -> Promise<unit>) = jsNative

[<Import("describe", from = "vitest")>]
let describe(name: string, testSuit: unit -> unit) = jsNative

describe("index", fun () ->
    itAsync ("add", fun () ->
        Pyxpecto.runTestsAsync [| ConfigArg.DoNotExitWithCode|] all
        |> Async.StartAsPromise
        |> map (fun (exitCode : int) -> Expect.equal exitCode 0 "Tests failed")
    )
)
#else
[<EntryPoint>]
let main argv = Pyxpecto.runTests [||] all
#endif
