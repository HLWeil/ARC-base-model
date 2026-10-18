import { Session, DatasetYaml } from "arc-management";
import { Dataset, Sample, Process } from "arc-base-model";
import { WriteResult } from "arc-management";
const check = (value, message) => { if (!value) throw new Error(message); };
const rejects = action => { try { action(); } catch { return; } throw new Error("Expected rejection"); };
const session = Session.createInMemory();
const graph = session.createArc(new Dataset(["process-provenance"], ["native-js"]));
const revision = () => graph.readModel().Revision;
const source = new Sample("A");
const added = graph.Sample.add(revision(), source);
check(source.Id === undefined && added.Value.Id !== undefined, "IDs assigned to persisted copies only");
rejects(() => { added.Value.Name = "bad"; });
rejects(() => { added.Value._name = "bad"; });
rejects(() => { added.Changed = false; });
rejects(() => { added.Revision._revision = 0; });
const originalFreeze = Object.freeze;
try {
    Object.freeze = value => {
        if (value instanceof WriteResult) throw new Error("Injected result construction failure");
        return originalFreeze(value);
    };
    rejects(() => graph.Sample.setName(added.Revision, added.Value.Id, "must roll back"));
} finally {
    Object.freeze = originalFreeze;
}
check(graph.Sample.get(added.Value.Id).Name === "A" && revision().Value === added.Revision.Value,
      "Result construction failure precedes persistent writes");
const proc = graph.Process.add(added.Revision, new Process("p"));
const linked = graph.Process.setInputSample(proc.Revision, proc.Value.Id, added.Value.Id);
check(linked.Value.Input instanceof Sample, "Native erased alternative");
const rootRead = graph.readModel();
const root = graph.Dataset.upsert(rootRead.Revision, Dataset.withProcesses(rootRead.Value, [linked.Value, linked.Value]));
rejects(() => root.Value.Processes.push(linked.Value));
rejects(() => { root.Value.Processes[0] = linked.Value; });
rejects(() => graph.Dataset.upsert(root.Revision, root.Value, true));
rejects(() => graph.Dataset.upsert(root.Revision, root.Value, { recursive: 1 }));
rejects(() => graph.Dataset.upsert(root.Revision, root.Value, { recursive: undefined }));
rejects(() => graph.Dataset.upsert(root.Revision, root.Value, { recursively: true }));
rejects(() => graph.Process.delete(root.Revision, linked.Value.Id));
rejects(() => graph.Process.delete(root.Revision, linked.Value.Id, true));
rejects(() => graph.Process.delete(root.Revision, linked.Value.Id, Object.create({ cascade: true })));
let optionGetterCalled = false;
rejects(() => graph.Process.delete(root.Revision, linked.Value.Id, { get cascade() { optionGetterCalled = true; return true; } }));
check(!optionGetterCalled, "Option getters cannot run callbacks or enable deletion");
const removed = graph.Process.delete(root.Revision, linked.Value.Id, { cascade: true });
check(graph.Model.Processes.length === 0, "Cascade detaches all occurrences");
graph.History.undo(removed.Revision);
const restored = graph.Model;
check(restored.Processes.length === 2 && restored.Processes[0] === restored.Processes[1], "Undo sharing and duplicates");
const decoded = DatasetYaml.decode(DatasetYaml.encode(restored));
check(decoded.Processes[0].Input instanceof Sample, "Canonical classes through codec");
rejects(() => graph.Entity.setBoolProperty(revision(), graph.Model.Id, "flag", 1));
rejects(() => graph.Entity.setNumberProperty(revision(), graph.Model.Id, "number", false));
session.close();
check(restored.Processes.length === 2, "Read lifetime");
console.log("Immutable JavaScript management consumer passed");
