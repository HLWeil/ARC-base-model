import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const directory = path.resolve(process.argv[2] ?? "build/out/arc-management/benchmarks");
const operations = ["cold_sample_read", "warm_sample_read", "storage_root_closure", "cold_root_read",
  "warm_root_read", "static_root_title_copy", "focused_sample_write", "no_op_sample_write",
  "focused_root_title_write", "recursive_graph_upsert", "append_duplicate_reference", "undo", "redo"];
let checked = 0;
for (const runtime of ["dotnet", "javascript", "python"]) {
  const [header, ...lines] = fs.readFileSync(path.join(directory, `${runtime}.csv`), "utf8").trim().split(/\r?\n/);
  const columns = header.split(",");
  const rows = lines.map(line => Object.fromEntries(line.split(",").map((value, index) => [columns[index], value])));
  assert.equal(rows.length, 39, `${runtime}: all operations at all three sizes`);
  for (const size of [100, 1000, 10000]) for (const operation of operations) {
    const matches = rows.filter(row => row.processes === String(size) && row.operation === operation);
    assert.equal(matches.length, 1, `${runtime}/${size}/${operation}: one measurement`);
    const row = matches[0];
    const count = key => {
      const value = Number(row[key]);
      assert.ok(Number.isFinite(value) && value >= 0, `${runtime}/${operation}: valid ${key}`);
      return value;
    };
    assert.equal(row.runtime, runtime);
    const repetitions = count("repetitions");
    const cells = count("cells_written"), history = count("history_cells"), hydrated = count("materialized_values");
    assert.equal(count("full_state_reads"), 0, `${runtime}/${operation}: no full registry scan`);
    count("median_ms"); count("p95_ms");
    if (operation.startsWith("warm_")) {
      assert.equal(count("queries"), repetitions, "One revision query per cached read");
      assert.equal(count("rows_read"), 0);
      assert.equal(hydrated, 0);
    }
    if (operation === "static_root_title_copy") {
      assert.equal(count("queries"), 0);
      assert.equal(count("rows_read"), 0);
      assert.equal(hydrated, 0);
    }
    if (["focused_sample_write", "focused_root_title_write", "recursive_graph_upsert", "append_duplicate_reference"].includes(operation)) {
      assert.equal(cells, repetitions, "One changed storage cell per command");
      assert.equal(history, repetitions, "One changed history cell per command");
      assert.equal(hydrated, repetitions * (operation === "focused_sample_write" ? 1 : size + 2), "Only returned reachable values materialized");
    } else {
      assert.equal(history, 0, "Reads, pure copies, no-ops and transitions record no new history delta");
      if (operation === "redo") assert.equal(cells, repetitions);
      else assert.equal(cells, 0);
    }
    if (operation === "no_op_sample_write") assert.equal(hydrated, 0);
    checked++;
  }
}
console.log(`Passed ${checked} benchmark work-count checks across all three runtimes.`);
