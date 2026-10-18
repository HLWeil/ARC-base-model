import * as Management from "arc-management";
import { Session, ManagedGraph, DatasetYaml, RevisionConflictException } from "arc-management";
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
const detached = Sample.withId(new Sample("A"), "s");
const added = graph.Sample.add(graph.readModel().Revision, detached);
check(added.Value instanceof Sample && detached.Id === "s", "Canonical immutable model");
const changed = graph.Sample.setName(added.Revision, "s", "B");
check(changed.Value.Name === "B" && added.Value.Name === "A", "Old snapshots preserved");
check(changed.Changed && changed.Operation !== undefined, "Changed metadata");
try { graph.Sample.setName(added.Revision, "s", "stale"); throw new Error("Missing conflict"); }
catch (error) { check(error instanceof RevisionConflictException, "Structured stale conflict"); }
const unchanged = graph.Sample.upsert(changed.Revision, changed.Value);
check(!unchanged.Changed && unchanged.Operation === undefined, "No-op metadata");
check(unchanged.Revision.Value === changed.Revision.Value, "No-op revision stable");
const before = graph.readModel();
const proc = Process.withInput(new Process("p"), changed.Value);
const attached = graph.Dataset.upsert(before.Revision, Dataset.withProcesses(before.Value, [proc, proc]));
check(attached.Value.Processes[0] === attached.Value.Processes[1], "Duplicate sharing");
check(Object.isFrozen(attached.Value) && Object.isFrozen(attached.Value.Processes), "Native immutability");
check(Array.isArray(attached.Value.Processes), "Native arrays");
const nestedSample = Sample.withName(changed.Value, "C");
const nestedProcess = Process.withInput(attached.Value.Processes[0], nestedSample);
const replacement = Dataset.withProcesses(attached.Value, [nestedProcess, nestedProcess]);
rejects(() => graph.Dataset.upsert(attached.Revision, replacement));
const recursive = graph.Dataset.upsert(attached.Revision, replacement, { recursive: true });
check(graph.Sample.get("s").Name === "C", "Explicit recursion saves nested edits");
// @ts-expect-error native options must be named
if (false) graph.Dataset.upsert(recursive.Revision, replacement, true);
// @ts-expect-error immutable field
if (false) recursive.Value.Title = "mutation";
// @ts-expect-error immutable collection
if (false) recursive.Value.Processes.push(proc);
// @ts-expect-error opaque revision constructor
if (false) new Management.RevisionToken();
// @ts-expect-error unknown option
if (false) graph.Dataset.upsert(recursive.Revision, replacement, { recursively: true });
const builder = new ModelBuilder();
const token = builder.cloneGraph(recursive.Value);
builder.Dataset.setTitle(token, "builder edit");
const built = builder.finish().get(token);
const saved = graph.Dataset.upsert(recursive.Revision, built, { recursive: true });
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
