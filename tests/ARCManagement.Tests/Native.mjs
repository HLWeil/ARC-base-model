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
// Immutable values cannot form cycles, so the test decodes the cyclic fixture from anchored YAML.
const detached = DatasetYaml.decode([
  "&o1",
  "type: Dataset",
  "conformsTo: [process-provenance]",
  "hasParts:",
  "    -",
  "        *o1",
  "    -",
  "        *o1",
  "identifiers: [detached]",
  ""
].join("\n"));
const detachedCopy = DatasetYaml.decode(DatasetYaml.encode(detached));
assert.equal(detached.Id, undefined);
assert.equal(detachedCopy.Id, undefined);
assert.equal(detachedCopy.HasParts[0], detachedCopy);
assert.equal(detachedCopy.HasParts[0], detachedCopy.HasParts[1]);
const importedGraph = session.importYaml(DatasetYaml.encode(detached));
assert.ok(importedGraph instanceof ManagedGraph);
assert.equal(importedGraph.Model.HasParts[0], importedGraph.Model);
const exportedGraph = DatasetYaml.decode(importedGraph.toYaml());
assert.equal(exportedGraph.HasParts[0], exportedGraph);
importedGraph.close();
const sample = new Sample("first", undefined, "same");
assert.equal(first.Sample.add(sample), sample);
const other = new Sample("second", undefined, "same");
assert.equal(second.Sample.add(other), other);
const process = first.Process.create("process");
assert.throws(() => first.Process.setInputSample(process, other));
first.Process.setInputSample(process, sample);
first.Dataset.addProcess(first.Model, process);
first.Sample.setName(sample, "changed");
first.History.undo();
assert.equal(sample.Name, "first");
assert.equal(second.Sample.get("same").Name, "second");
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
second.Sample.setName(other, "independent");
session.close();
session = Session.openFile(path);
const resumed = session.openArc(firstId);
assert.equal(resumed.Model.Processes[0].Input, resumed.Sample.get("same"));
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
