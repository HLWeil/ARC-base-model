import * as Management from "arc-management";
import { Session, ManagedGraph, DatasetYaml, SnapshotConflictException } from "arc-management";
import { ARC } from "arc-management/arctrl";
import { Dataset, Sample, Process, ModelBuilder } from "arc-base-model";

function check(value: boolean, message: string): asserts value { if (!value) throw new Error(message); }
function rejects(action: () => unknown): void { try { action(); } catch { return; } throw new Error("Expected rejection"); }
const source = Dataset.withId(new Dataset(["process-provenance"], ["root"]), "root");
const session = Session.createInMemory();
const graph = session.createArc(source);
check(graph instanceof ManagedGraph, "Canonical graph instance");
check(session.openArc(graph.ArcId) === graph, "Native facade cached");
check(!("ARC" in Management), "Workspace export separate");
check(!("RevisionToken" in Management) && !("ReadResult" in Management), "No public concurrency plumbing");
const detached = Sample.withId(new Sample("A"), "s");
const added = graph.Sample.add(detached);
check(added.Value instanceof Sample && detached.Id === "s", "Canonical immutable model");
const changed = graph.Sample.setName(graph.Sample.get("s"), "B");
check(changed.Value.Name === "B" && added.Value.Name === "A", "Old snapshots preserved");
check(changed.Changed && changed.Operation !== undefined, "Changed metadata");
const types = graph.Sample.setAdditionalTypes(added.Value, ["new type"]);
check(types.Value.Name === "B", "Focused command preserves current fields using an older subject");
try { graph.Sample.upsert(added.Value, { baseline: added.Value }); throw new Error("Missing conflict"); }
catch (error) { check(error instanceof SnapshotConflictException && error.EntityId === "s", "Structured snapshot conflict"); }
const unchanged = graph.Sample.upsert(types.Value, { baseline: types.Value });
check(!unchanged.Changed && unchanged.Operation === undefined, "No-op metadata");
const before = graph.readModel();
const proc = Process.withInput(new Process("p"), types.Value);
const attached = graph.Dataset.upsert(Dataset.withProcesses(before, [proc, proc]), { baseline: before });
check(attached.Value.Processes[0] === attached.Value.Processes[1], "Duplicate sharing");
check(Object.isFrozen(attached.Value) && Object.isFrozen(attached.Value.Processes), "Native immutability");
check(Array.isArray(attached.Value.Processes), "Native arrays");
const nestedSample = Sample.withName(types.Value, "C");
const nestedProcess = Process.withInput(attached.Value.Processes[0], nestedSample);
const replacement = Dataset.withProcesses(attached.Value, [nestedProcess, nestedProcess]);
rejects(() => graph.Dataset.upsert(replacement, { baseline: attached.Value }));
const recursive = graph.Dataset.upsert(replacement, { baseline: attached.Value, recursive: true });
check(graph.Sample.get("s").Name === "C", "Explicit recursion saves nested edits");
// @ts-expect-error native options must be named
if (false) graph.Dataset.upsert(replacement, true);
// @ts-expect-error immutable field
if (false) recursive.Value.Title = "mutation";
// @ts-expect-error immutable collection
if (false) recursive.Value.Processes.push(proc);
// @ts-expect-error subjects are entity values
if (false) graph.Sample.setName("s", "wrong subject");
// @ts-expect-error baseline must have the same entity type
if (false) graph.Sample.upsert(types.Value, { baseline: graph.Model });
// @ts-expect-error unknown option
if (false) graph.Dataset.upsert(replacement, { recursively: true });
const builder = new ModelBuilder();
const token = builder.cloneGraph(recursive.Value);
builder.Dataset.setTitle(token, "builder edit");
const built = builder.finish().get(token);
const saved = graph.Dataset.upsert(built, { baseline: recursive.Value, recursive: true });
check(saved.Value.Title === "builder edit", "Native typed builder");
const yaml = graph.toYaml();
const decoded = DatasetYaml.decode(yaml);
check(decoded instanceof Dataset && decoded.Processes[0] === decoded.Processes[1], "YAML uses shared canonical classes");
const folder = `build/out/arc-management/native-ts-${Date.now()}`;
const arc = ARC.bind(graph, folder);
check(arc.ManagedGraph === graph && arc.Dataset === graph.Dataset, "Workspace forwards facade identity");
arc.save();
check(!arc.IsDirty, "Explicit publication");
const held = graph.Sample.get("s");
arc.close();
check(held.Name === "C", "Value survives closure");
session.close();
console.log("Immutable TypeScript management consumer passed");
