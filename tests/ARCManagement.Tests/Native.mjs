import { Session, DatasetYaml } from "arc-management";
import { Dataset, Sample, Process } from "arc-base-model";
import { WriteResult, CommandResult, ConcurrentWriteException } from "arc-management";
const check = (value, message) => { if (!value) throw new Error(message); };
const rejects = action => { try { action(); } catch { return; } throw new Error("Expected rejection"); };
const session = Session.createInMemory();
const graph = session.createArc(new Dataset(["process-provenance"], ["native-js"]));
const source = new Sample("A");
const added = graph.Sample.add(source);
check(source.Id === undefined && added.Value.Id !== undefined, "IDs assigned to persisted copies only");
check(!("Revision" in added) && !("Revision" in graph), "No public revision plumbing");
rejects(() => graph.Sample.setName(added.Value.Id, "ID is not a subject"));
rejects(() => graph.Sample.setName(Dataset.withId(new Dataset(["administrative"], ["wrong"]), added.Value.Id), "wrong kind"));
rejects(() => { added.Value.Name = "bad"; });
rejects(() => { added.Value._name = "bad"; });
rejects(() => { added.Changed = false; });
const originalFreeze = Object.freeze;
try {
    Object.freeze = value => {
        if (value instanceof WriteResult) throw new Error("Injected result construction failure");
        return originalFreeze(value);
    };
    rejects(() => graph.Sample.setName(added.Value, "must roll back"));
} finally {
    Object.freeze = originalFreeze;
}
check(graph.Sample.get(added.Value.Id).Name === "A", "Result construction failure precedes persistent writes");
const proc = graph.Process.add(new Process("p"));
const linked = graph.Process.setInputSample(proc.Value, added.Value);
check(linked.Value.Input instanceof Sample, "Native erased alternative");
rejects(() => graph.Process.setInputSample(proc.Value, added.Value.Id));
rejects(() => graph.Process.setInput(proc.Value, graph.Model));
rejects(() => graph.Dataset.addProcess(graph.Model, proc.Value.Id));
rejects(() => graph.Dataset.addProcess(graph.Model, added.Value));
rejects(() => graph.Dataset.addProcess(graph.Model, null));
rejects(() => graph.Dataset.setProcesses(graph.Model, [proc.Value.Id]));
rejects(() => graph.Entity.setObjectProperty(graph.Model, "process", proc.Value.Id));
rejects(() => graph.Descriptor.create(added.Value.Id));
const relationGraph = session.createArc(new Dataset(["process-provenance"], ["relations"]));
const relationProcess = relationGraph.Process.create("process").Value;
let enumerations = 0;
const iterable = { *[Symbol.iterator]() { enumerations++; yield relationProcess; yield relationProcess; } };
const relations = relationGraph.Dataset.setProcesses(relationGraph.Model, iterable);
check(enumerations === 1 && relations.Value.Processes[0] === relations.Value.Processes[1], "Native iterable consumed once with sharing");
relationGraph.Dataset.removeProcess(relationGraph.Model, relationProcess);
check(relationGraph.Model.Processes.length === 1 && relations.Value.Processes.length === 2, "Removal preserves earlier snapshot");
relationGraph.Dataset.addProcess(relationGraph.Model, relationProcess);
check(relationGraph.Model.Processes.length === 2, "Add takes an object");
rejects(() => relationGraph.Dataset.addProcess(relationGraph.Model, proc.Value));
rejects(() => relationGraph.Entity.addObjectProperty(relationGraph.Model, "foreign", proc.Value));
let callbackRan = false;
rejects(() => relationGraph.Dataset.setProcesses(relationGraph.Model, (function* () {
    callbackRan = true;
    relationGraph.Dataset.addProcess(relationGraph.Model, relationProcess);
    yield relationProcess;
})()));
check(callbackRan && relationGraph.Model.Processes.length === 2, "Relationship iterable runs under repository gate");
const rootRead = graph.readModel();
const root = graph.Dataset.upsert(Dataset.withProcesses(rootRead, [linked.Value, linked.Value]), { baseline: rootRead });
rejects(() => root.Value.Processes.push(linked.Value));
rejects(() => { root.Value.Processes[0] = linked.Value; });
rejects(() => graph.Dataset.upsert(root.Value, true));
rejects(() => graph.Dataset.upsert(root.Value, { recursive: 1 }));
rejects(() => graph.Dataset.upsert(root.Value, { recursive: undefined }));
rejects(() => graph.Dataset.upsert(root.Value, { recursively: true }));
rejects(() => graph.Process.delete(linked.Value));
rejects(() => graph.Process.delete(linked.Value, true));
rejects(() => graph.Process.delete(linked.Value, Object.create({ cascade: true })));
let optionGetterCalled = false;
rejects(() => graph.Process.delete(linked.Value, { get cascade() { optionGetterCalled = true; return true; } }));
check(!optionGetterCalled, "Option getters cannot run callbacks or enable deletion");
const removed = graph.Process.delete(linked.Value, { cascade: true });
check(graph.Model.Processes.length === 0, "Cascade detaches all occurrences");
graph.History.undo();
const restored = graph.Model;
check(restored.Processes.length === 2 && restored.Processes[0] === restored.Processes[1], "Undo sharing and duplicates");
const decoded = DatasetYaml.decode(DatasetYaml.encode(restored));
check(decoded.Processes[0].Input instanceof Sample, "Canonical classes through codec");
rejects(() => graph.Entity.setBoolProperty(graph.Model, "flag", 1));
rejects(() => graph.Entity.setNumberProperty(graph.Model, "number", false));
session.close();
check(restored.Processes.length === 2, "Read lifetime");

// Freeze runs after the read snapshot closes and before the writer transaction.
// A second real connection can commit there without relying on thread timing.
const raceSession = Session.createFile(`build/out/arc-management/native-race-${Date.now()}.sqlite`);
const first = raceSession.createArc(new Dataset(["process-provenance"], ["race"]));
const subject = first.Sample.add(Sample.withId(new Sample("initial"), "race-sample")).Value;
const otherSession = Session.openFile(first.DatabasePath);
const second = otherSession.openArc(first.ArcId);
function race(resultClass, remote, local) {
    let intervened = false;
    try {
        Object.freeze = value => {
            if (!intervened && value instanceof resultClass) {
                intervened = true;
                remote();
            }
            return originalFreeze(value);
        };
        try { local(); throw new Error("Missing concurrent write rejection"); }
        catch (error) { check(error instanceof ConcurrentWriteException, "Structured concurrent write conflict"); }
    } finally { Object.freeze = originalFreeze; }
    check(intervened, "Second writer committed at preparation boundary");
}
race(WriteResult,
    () => second.Sample.setName(second.Sample.get("race-sample"), "remote"),
    () => first.Sample.setName(subject, "local"));
check(first.Sample.get("race-sample").Name === "remote", "Failed command preserves remote state");
race(CommandResult,
    () => second.Dataset.setTitle(second.Model, "remote title"),
    () => first.History.undo());
check(first.Sample.get("race-sample").Name === "remote", "Failed history transition has no partial effect");
check(first.Model.Title === "remote title", "Concurrent history writer preserved");
first.History.undo();
check(first.Model.Title === undefined, "Failed commands add no history");
first.History.undo();
check(first.Sample.get("race-sample").Name === "initial", "Original history remains reversible");
otherSession.close();
raceSession.close();
console.log("Immutable JavaScript management consumer passed");
