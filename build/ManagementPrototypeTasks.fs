module ManagementPrototypeTasks

open BlackFox.Fake
open Fake.DotNet

/// Isolated .NET prototype checks; native JS/Python runtime verification is deferred.
let testManagementPrototype = BuildTask.createFn "TestManagementPrototype" [] (fun _ ->
    let result = DotNet.exec id "run" "--project tests/ManagementPrototype.Tests/ManagementPrototype.Tests.fsproj --configuration Release"
    if not result.OK then failwith "ARC management prototype tests failed."
)
