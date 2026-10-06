import assert from "node:assert/strict";
import fs from "node:fs";
import { Sqlite } from "../../build/out/polyglot-sqlite/js/node_modules/polyglot-sqlite/index.js";

const schema = fs.readFileSync("schemas/sql/001_core.sql","utf8");
const seed = fs.readFileSync("schemas/sql/seed_example.sql","utf8");
const cases = fs.readFileSync("tests/ManagementPrototype.Tests/CoreSql.cases.tsv","utf8").trimEnd().split(/\r?\n/);
for (const line of cases) {
  const [kind, statement, expected = ""] = line.split("\t");
  const sql = Sqlite.OpenInMemory();
  try {
    sql.ExecuteScript(schema);
    sql.ExecuteScript(seed);
    const valid = () => assert.equal(sql.Query("SELECT * FROM core_validation_errors").length,0);
    if (kind === "crud") {
      const commands = statement.split(";");
      for (const command of commands.slice(0,-1)) sql.Execute(command);
      assert.equal(sql.Scalar(commands.at(-1)).AsText(),expected,statement);
      valid();
    }
    else if (kind === "reject") assert.throws(()=>sql.Execute(statement));
    else if (kind === "accept") { sql.Execute(statement); valid(); }
    else if (kind === "invalid") { sql.Execute(statement); assert.notEqual(sql.Query("SELECT * FROM core_validation_errors").length,0); }
    else {
      const value = sql.Scalar(statement);
      if (kind === "integer") assert.equal(value.AsInteger(),BigInt(expected),statement);
      else if (kind === "real") { assert.equal(typeof value.AsReal(),"number"); assert.equal(value.AsReal(),Number(expected),statement); }
      else if (kind === "text") assert.equal(value.AsText(),expected,statement);
      else if (kind === "null") assert.equal(value.IsNull,true,statement);
      else throw new Error(`Unknown case ${kind}`);
    }
    assert.equal(sql.Query("PRAGMA foreign_key_check").length,0);
  } finally { sql.Close(); }
}
console.log(`${cases.length} core SQL cases passed on Node using PolyglotSQLite.`);
