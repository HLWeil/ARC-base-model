# ARCtrl

ARCtrl is the experimental ARC management library over ARCBaseModel and
PolyglotSQLite. Public classes remain in the `ARCtrl` namespace;
implementation details are internal to `ARCtrl.Internal`.
The [general management plan](../../plans/arc-management.md) describes the
intended architecture beyond this .NET-first prototype.

## Project structure

```text
src/ARCtrl/
├── ARC.fs                         Public session facade
├── AppliedOperation.fs            Committed-operation result
├── Operations/                    Dataset, Process, Sample and History APIs
├── Runtime/
│   ├── Session.fs                 Identity, transactions, undo/redo and save
│   └── Workspace.fs               Open/create and source-conflict resolution
├── Internal/
│   ├── State.fs                   Registry snapshots
│   └── Model.fs                   Model capture, validation and reachability
├── Serialization/YamlCodec.fs     Snapshot and root Dataset YAML codecs
├── IO/FileSystem.fs               .NET/Node/Python filesystem boundaries
└── Storage/SQLiteStore.fs          SQLite schema, connection and state mirrors
```

The [project file](../../src/ARCtrl/ARCtrl.fsproj) lists dependencies in F# compile
order. Each operation group depends on the internal session, and the public ARC
facade is compiled last; no mutually recursive public type group is needed.

## Use and verification

Use `open ARCtrl` and `ARC.create(folder, rootDataset)` or
`ARC.openFolder(folder, "auto")`. The operation shape is unchanged:
`arc.Process.setInputSample(proc, sample)` and `arc.History.undo()`.
`save()` writes the root Dataset and its referenced objects to `arc.yml`;
standalone registered objects and undo/redo history remain in `.arc/testing.sqlite`.
Closing does not implicitly save. Explicit `"sql"` and `"yml"` source preferences
resolve conflicts with external YAML changes.

`arc.Sample.set(sample)` returns `unit` and upserts by ID: it registers a new
Sample (assigning a missing ID), or replaces supported values in the existing
instance without breaking Process references. Updates use a detached Sample;
empty `AdditionalTypes` clears the collection. Unsupported `AdditionalProperties`
and cross-type ID collisions are rejected. SQL and history commit together,
so inserts and replacements can both be undone/redone after reopening.

```fsharp
arc.Sample.set(Sample("updated leaf", id = sample.Id.Value))
```

```powershell
dotnet build src/ARCtrl/ARCtrl.fsproj -c Release
.\build.cmd TestManagementPrototype
dotnet run --project tests/ManagementPrototype.Tests -c Release -- --demo ./build/out/my-arc
```

The [walkthrough](../../tests/ManagementPrototype.Tests/Walkthrough.fs) illustrates
creation, mutation, undo/redo, export and reopening. Choose a new demo folder.
Native JavaScript/Python transpilation, packages and runtime verification remain
deferred; this restructuring does not expand the prototype's entity or IO scope.
