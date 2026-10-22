import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdirSync, existsSync } from "node:fs";
import * as Management from "arc-management";
import { DatasetYaml, ManagedGraph, Session } from "arc-management";
import { ARC } from "arc-management/arctrl";
import { Dataset, EntityObject, Sample } from "arc-base-model";

const folder = `build/out/arc-management/native-js-${randomUUID()}`;
mkdirSync(folder, { recursive: true });
const path = `${folder}/collection.sqlite`;
const root = () => EntityObject.withId(new Dataset(["process-provenance"], ["native"]), "root");
let session = Session.createFile(path);
const first = session.createArc(root());
const second = session.createArc(root());
assert.equal("ARC" in Management, false);
assert.ok(first instanceof ManagedGraph);
assert.equal("save" in first, false);
// Immutable values cannot form cycles, so the test decodes the cyclic fixture from YAML.
const detached = DatasetYaml.decode([
  "type: Dataset",
  "id: detached",
  "conformsTo: [process-provenance]",
  "identifier: detached",
  "hasPart:",
  "  - \"@id\": detached",
  "  - \"@id\": detached",
  ""
].join("\n"));
const detachedCopy = DatasetYaml.decode(DatasetYaml.encode(detached));
assert.equal(detached.Id, "detached");
assert.equal(detachedCopy.Id, "detached");
assert.equal(detachedCopy.HasParts[0], detachedCopy);
assert.equal(detachedCopy.HasParts[0], detachedCopy.HasParts[1]);
assert.equal(DatasetYaml.encodeIndexed(detachedCopy), DatasetYaml.encode(detachedCopy));
assert.throws(() => DatasetYaml.decode("type: Dataset\nidentifier: x\nhasPart:\n  - {type: Dataset, id: same, identifier: a}\n  - {type: Dataset, id: same, identifier: b}\n"));
const importedGraph = session.importYaml(DatasetYaml.encode(detached));
assert.ok(importedGraph instanceof ManagedGraph);
assert.equal(importedGraph.Model.HasParts[0], importedGraph.Model);
const exportedGraph = DatasetYaml.decode(importedGraph.toYaml());
assert.equal(exportedGraph.HasParts[0], exportedGraph);
importedGraph.close();
const sample = new Sample("first", undefined, "same");
const stored = first.Sample.add(sample);
assert.equal(stored.Id, "same");
assert.notStrictEqual(stored, sample);
const other = new Sample("second", undefined, "same");
second.Sample.add(other);
const process = first.Process.create("process");
assert.throws(() => first.Process.setInput(process, new Sample("unknown", undefined, "unknown-id")));
// A value from another ARC resolves by ID in the destination.
assert.equal(first.Process.setInput(process, other).Input.Name, "first");
first.Dataset.addProcess(first.Model, process);
const renamed = first.Sample.setName(stored, "changed");
assert.equal(renamed.Name, "changed");
assert.equal(stored.Name, "first");
first.History.undo();
assert.equal(first.Sample.get("same").Name, "first");
assert.equal(renamed.Name, "changed");
assert.equal(second.Sample.get("same").Name, "second");
// An operation group rejects subjects of another kind and changes nothing.
assert.throws(() => first.Process.setName(first.Sample.get("same"), "wrong"));
assert.throws(() => first.Process.delete(first.Sample.get("same")));
assert.equal(first.Sample.get("same").Name, "first");
// One-shot iterables are consumed once, so target kinds are still checked and valid targets are all stored.
function* once(items) { yield* items; }
const probeSession = Session.createInMemory();
const probe = probeSession.createArc(root());
const probeProcess = probe.Process.create("probe");
const annotationA = probe.Annotation.create("a");
const annotationB = probe.Annotation.create("b");
assert.throws(() => probe.Process.setParameterValues(probeProcess, once([annotationA, new Sample("wrong", undefined, annotationB.Id)])));
assert.equal(probe.Process.get(probeProcess.Id).ParameterValues.length, 0);
assert.equal(probe.Process.setParameterValues(probeProcess, once([annotationA, annotationB])).ParameterValues.length, 2);
probeSession.close();
assert.equal(session.openArc(first.ArcId), first);
assert.equal(session.listArcs().length, 3);
const workspace = ARC.bind(first, `${folder}/export`);
assert.equal(workspace.ManagedGraph, first);
assert.equal(workspace.Sample, first.Sample);
assert.equal(ARC.fromGraph(first).ManagedGraph, first);
assert.equal(existsSync(`${folder}/export/arc.yml`), false);
workspace.save();
const firstId = first.ArcId;
const secondId = second.ArcId;
workspace.close();
assert.throws(() => first.Sample.create("closed"));
second.Sample.setName(second.Sample.get("same"), "independent");
session.close();
session = Session.openFile(path);
const resumed = session.openArc(firstId);
assert.equal(resumed.Model.Processes[0].Input.Id, resumed.Sample.get("same").Id);
resumed.History.redo();
assert.equal(resumed.Sample.get("same").Name, "changed");
assert.equal(session.openArc(secondId).Sample.get("same").Name, "independent");
assert.throws(() => Session.createFile(path));
const memory = Session.createInMemory();
const imported = ARC.importFolder(memory, `${folder}/export`);
assert.equal(imported.Model.Processes[0].Input.Name, "first");
memory.close();
session.close();
assert.throws(() => resumed.Sample.list());
console.log("Native JavaScript management and workspace checks passed.");
