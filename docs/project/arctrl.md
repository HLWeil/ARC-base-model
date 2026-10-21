---
title: ARCtrl Workspaces
category: Project
categoryindex: 2
index: 11
---

# ARCtrl workspaces

`ARCtrl.ARC` binds a managed graph to one ARC workspace folder. It lives in
`src/ARCManagement/ARCtrl` within the `ARCManagement` assembly. Connections,
mutation, identity, history, and YAML codecs belong to
[`ARCManagement`](arc-management.md); this facade handles workspace paths,
loading, saving, and recovery.

The layout remains `arc.yml` for root-graph export and `.arc/arc.sqlite` for
convenience-created repositories. Project-file dispatch through
`.arc/project.yml` remains future work described in the
[management plan](../../plans/arc-management.md) and
[project-file specification](../spec/project_file.md).

## Bind an existing graph

```fsharp
open ARCBaseModel
open ARCManagement
open ARCtrl

let session = Session.createFile("collection.sqlite")
let graph = session.createArc(Dataset(["process-provenance"], ["example"]))
let arc = ARC.bind(graph, "./arc-a")
let graph = arc.ManagedGraph
let sample = arc.Sample.create("sample")
let proc = arc.Process.create("process")
let linked = arc.Process.setInputSample(proc.Value, sample.Value)
arc.Dataset.addProcess(graph.Model, proc.Value) |> ignore
arc.save()
let arcId = graph.ArcId
arc.close()
let reopened = session.openArc(arcId)
let workspace = ARC.fromGraph(reopened)
workspace.recoverFolder("auto")
session.close()
```

`ARC.bind(graph, folder)` applies the existing binding policy and returns a
workspace facade. A new binding requires absent `arc.yml`; binding neither loads
nor writes files. Instance `bindFolder(folder)` applies the same policy.
`ARC.fromGraph(graph)` requires an existing retained binding and wraps it without
reconciling filesystem changes.

`arc.ManagedGraph` is the supplied graph. `Model`, all entity operation groups,
`Entity`, and `History` forward to that exact graph and its operation objects. Domain reads return immutable snapshots.
`ArcId`, `DatabasePath`, and `HasSessionOnlyObjects` also forward to it. Workspace
properties `FolderBinding`, `Folder`, and `IsDirty` describe the binding and save
baseline. `IsDirty` tracks export changes; `HasSessionOnlyObjects` reports
registered objects outside the root graph.

Closing a workspace closes its graph and invalidates every wrapper sharing it.
A borrowed session and sibling graphs remain usable. Closing the session
invalidates all its graphs. Closing never implicitly saves files.

## Create, open, and import folders

```fsharp
let created = ARC.create("./new-arc", Dataset(["process-provenance"], ["new"]))
created.save()
created.close()
let opened = ARC.openFolder("./new-arc", "auto")
opened.close()

let imported = ARC.importFolder(session, "./existing-export")
```

`ARC.create` and `ARC.openFolder` privately own a session at
`.arc/arc.sqlite`. Closing these facades also closes that session. The folder
opener requires exactly one entry with the matching retained binding and accepts
`"auto"`, `"sql"`, and `"yml"` source preferences.

`ARC.importFolder(session: Session, folder)` reads prototype `arc.yml` into a new
graph in the supplied session, records its binding and filesystem baseline, and
returns a workspace facade. Graph creation, binding, and baseline initialization
share one transaction. The supplied session remains borrowed. For YAML text
without a workspace, use `session.importYaml(text)`.

## Save and recover

`save()` requires a binding. It checks the fresh SQL revision and external file changes, then exports only the graph rooted at `Model`. Registered objects
outside that graph and all undo/redo history remain in SQLite. Export uses the
prototype `id`/`$ref` format, defining shared/circular entities once and preserving
extensions and scalar alternatives. `ManagedGraph.toYaml()` produces the same
root-only text without changing files, history, or save checkpoints.

The baseline compares observed `arc.yml` text, including absence. Save stages a
replacement file and advances the SQL checkpoint only after publication. Failed
saves retain the last successful checkpoint and pending status. Filesystem
publication and SQLite commit cannot be atomic together: if publication succeeds
and checkpoint commit fails, explicitly select a source before retrying.

| Recovery source | Behavior |
|---|---|
| `"auto"` | Resume SQL when the file matches its baseline. Reload changed YAML only without pending edits or session-only objects; otherwise report a conflict. |
| `"sql"` | Keep retained SQL working state and accept the observed filesystem baseline. |
| `"yml"` | Archive the displaced entry, then replace it from YAML. |

`recoverFolder(source)` performs reconciliation explicitly. Opening a database or
wrapping a bound graph does not inspect files. YAML replacement archives only the
selected entry's metadata, normalized state, incremental history, and journal inside the existing
repository. It preserves sibling entries and the shared database file.

Losing the database and its archives loses unsaved and session-only work that
cannot be reconstructed from the exported root graph.

## Native packages and verification

```javascript
import { Session } from "arc-management";
import { ARC } from "arc-management/arctrl";
```

```python
from arc_management import Session
from arc_management.arctrl import ARC
```

Import domain classes from `arc-base-model` / `arc_base_model`. Both management
entrypoints share one implementation and canonical model/SQLite classes. Python
methods use emitted snake_case names such as `open_folder`, `import_folder`,
`from_graph`, `bind_folder`, and `recover_folder`.

```powershell
.\build.cmd TestARCManagement
dotnet run --project tests/ARCManagement.Tests -c Release -- --demo ./build/out/my-arc
```

Choose a new demo folder. The
[walkthrough](../../tests/ARCManagement.Tests/Walkthrough.fs) illustrates creation,
mutation, undo/redo, export, and reopening. `TestARCManagement` exercises shared
behavior on .NET, JavaScript, and Python plus native consumers, declarations, and
packed artifacts. Actual verification is recorded in the
[management plan](../../plans/arc-management.md).
