import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import ts from "typescript";
import { build as viteBuild } from "vite";

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const output = path.join(repository, "build/out/polyglot-sqlite");
const source = path.join(output, "ts");
const testSource = path.join(output, "tests-ts");
const javascript = path.join(output, "js");
const libraryPackage = path.join(javascript, "node_modules/polyglot-sqlite");
const probePackage = path.join(javascript, "node_modules/polyglot-sqlite-probe");
const nativeTests = path.join(repository, "tests/PolyglotSQLite.Native");

// Curated native contracts deliberately hide Fable helpers, internal readers,
// driver constructors and mutable implementation fields. Runtime classes are
// always the actual generated classes, never replacement facade classes.
const contracts = {
  SqlValue: `export declare class SqlValue {
  private constructor();
  static Null(): SqlValue;
  static Text(value: string): SqlValue;
  static Integer(value: bigint): SqlValue;
  static Real(value: number): SqlValue;
  static Blob(value: Uint8Array): SqlValue;
  readonly Kind: "null" | "text" | "integer" | "real" | "blob";
  readonly IsNull: boolean;
  AsText(): string;
  AsInteger(): bigint;
  AsReal(): number;
  AsBlob(): Uint8Array;
}`,
  SqlParameter: `export declare class SqlParameter {
  constructor(name: string, value: SqlValue);
  readonly Name: string;
  readonly Value: SqlValue;
}`,
  SqlRow: `export declare class SqlRow {
  constructor(columnNames: Iterable<string>, values: Iterable<SqlValue>);
  readonly Count: number;
  GetColumnName(index: number): string;
  Get(index: number): SqlValue;
  GetByName(name: string): SqlValue;
  TryGetByName(name: string): SqlValue | undefined;
}`,
  Sqlite: `export declare class Sqlite {
  private constructor();
  static OpenFile(path: string): SqliteConnection;
  static OpenInMemory(): SqliteConnection;
  static WrapConnection(nativeConnection: object): SqliteConnection;
}`,
  SqliteConnection: `export declare class SqliteConnection {
  private constructor();
  Execute(sql: string, parameters?: Iterable<SqlParameter>): void;
  Query(sql: string, parameters?: Iterable<SqlParameter>): SqlRow[];
  Scalar(sql: string, parameters?: Iterable<SqlParameter>): SqlValue | undefined;
  ExecuteScript(sql: string): void;
  BeginTransaction(): SqliteTransaction;
  WithTransaction<T>(action: () => T & (T extends PromiseLike<unknown> ? never : unknown)): T;
  Close(): void;
}`,
  SqliteTransaction: `export declare class SqliteTransaction {
  private constructor();
  Commit(): void;
  Rollback(): void;
  Close(): void;
}`,
  Table: `export declare class Table<T extends {}> {
  private readonly _rowType: (value: T) => T;
  constructor(name: string, columns: Iterable<string>, primaryKey: Iterable<string>, encode: (row: T) => SqlValue[], decode: (row: SqlRow) => T);
  readonly Name: string;
  readonly Columns: string[];
  readonly PrimaryKey: string[];
}`,
  TableRepository: `export declare class TableRepository<T extends {}> {
  constructor(connection: SqliteConnection, table: Table<T>);
  Insert(row: T): void;
  Update(row: T): void;
  Delete(keyValues: Iterable<SqlValue>): void;
  Get(keyValues: Iterable<SqlValue>): T | undefined;
  List(): T[];
}`,
};

function write(filename, contents) {
  fs.mkdirSync(path.dirname(filename), { recursive: true });
  fs.writeFileSync(filename, contents, "utf8");
}

function assertExists(filename) {
  if (!fs.existsSync(filename)) throw new Error(`Missing generated input: ${filename}`);
}

function cleanGenerated(directory) {
  const resolved = path.resolve(directory);
  if (!resolved.startsWith(output + path.sep)) throw new Error(`Refusing to clean outside ${output}`);
  fs.rmSync(resolved, { recursive: true, force: true });
  fs.mkdirSync(resolved, { recursive: true });
}

function diagnosticsOrThrow(diagnostics) {
  if (diagnostics.length) throw new Error(ts.formatDiagnosticsWithColorAndContext(diagnostics, {
    getCurrentDirectory: () => repository,
    getCanonicalFileName: filename => filename,
    getNewLine: () => "\n",
  }));
}

function parse(filename) {
  assertExists(filename);
  const parsed = ts.createSourceFile(filename, fs.readFileSync(filename, "utf8"), ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
  diagnosticsOrThrow(parsed.parseDiagnostics);
  return parsed;
}

function publicClasses(directory, expected) {
  const classes = new Map();
  const genericAliases = { "Table$1": "Table", "TableRepository$1": "TableRepository" };
  for (const entry of fs.readdirSync(directory).filter(name => name.endsWith(".ts"))) {
    const parsed = parse(path.join(directory, entry));
    for (const node of parsed.statements) {
      const publicName = genericAliases[node.name?.text] ?? node.name?.text;
      if (ts.isClassDeclaration(node) && expected.has(publicName)) {
        if (classes.has(publicName)) throw new Error(`Duplicate class ${publicName}`);
        classes.set(publicName, { filename: entry, node, parsed });
      }
    }
  }
  return classes;
}

function checkedContract(name, generated) {
  const contract = ts.createSourceFile("contract.d.ts", contracts[name], ts.ScriptTarget.Latest, true);
  const declared = contract.statements.find(ts.isClassDeclaration);
  for (const member of declared.members) {
    if (ts.isConstructorDeclaration(member)) continue;
    // A declaration-only brand retains Table<T>'s row type after construction;
    // metadata alone would make every Table<T> structurally interchangeable.
    if (ts.isPropertyDeclaration(member) && member.modifiers?.some(modifier => modifier.kind === ts.SyntaxKind.PrivateKeyword)) continue;
    const memberName = member.name?.getText(contract);
    const actual = generated.node.members.find(candidate => candidate.name?.getText(generated.parsed) === memberName);
    if (!actual) throw new Error(`Generated ${name} is missing declared member ${memberName}`);
    if (ts.isMethodDeclaration(member)) {
      const isStatic = node => !!node.modifiers?.some(modifier => modifier.kind === ts.SyntaxKind.StaticKeyword);
      if (!ts.isMethodDeclaration(actual) || isStatic(member) !== isStatic(actual)
        || member.parameters.length !== actual.parameters.length) {
        throw new Error(`Generated ${name}.${memberName} calling convention changed; review native contract`);
      }
    }
  }
  return contracts[name];
}

function probeDeclarations(probe) {
  const printer = ts.createPrinter({ newLine: ts.NewLineKind.LineFeed });
  function nativeType(node) {
    if (!node) throw new Error("Missing NumericProbe type annotation");
    if (ts.isTypeReferenceNode(node)) {
      const name = node.typeName.getText(probe.parsed);
      const primitives = { int32: "number", int64: "bigint", float64: "number", uint8: "number" };
      if (primitives[name]) return primitives[name];
      if (name === "Option") return `${nativeType(node.typeArguments[0])} | undefined`;
      if ([...Object.keys(contracts), "Uint8Array"].includes(name)) return name;
      throw new Error(`Unexpected probe type ${name}`);
    }
    if (ts.isArrayTypeNode(node)) return `${nativeType(node.elementType)}[]`;
    if ([ts.SyntaxKind.BooleanKeyword, ts.SyntaxKind.StringKeyword, ts.SyntaxKind.NumberKeyword,
      ts.SyntaxKind.BigIntKeyword, ts.SyntaxKind.VoidKeyword].includes(node.kind)) return printer.printNode(ts.EmitHint.Unspecified, node, probe.parsed);
    throw new Error(`Unexpected probe type ${ts.SyntaxKind[node.kind]}`);
  }
  const methods = probe.node.members.filter(ts.isMethodDeclaration).filter(member => !member.name.getText(probe.parsed).startsWith("_"));
  return `import type { SqlValue, SqlParameter, SqlRow } from "polyglot-sqlite";\nexport declare class NumericProbe {\n  private constructor();\n`
    + methods.map(member => `  static ${member.name.getText(probe.parsed)}(${member.parameters.map(parameter => `${parameter.name.getText(probe.parsed)}: ${nativeType(parameter.type)}`).join(", ")}): ${nativeType(member.type)};`).join("\n")
    + "\n}\n";
}

function manifest(name) {
  return JSON.stringify({ name, version: "0.0.0", private: true, type: "module",
    exports: { ".": { types: "./index.d.ts", import: "./index.js" } },
    main: "./index.js", types: "./index.d.ts",
    ...(name === "polyglot-sqlite" ? { dependencies: { "better-sqlite3": JSON.parse(fs.readFileSync(path.join(repository, "package.json"), "utf8")).dependencies["better-sqlite3"] } } : {}),
  }, null, 2) + "\n";
}

function sharedRuntimePlugin() {
  const duplicatePrefix = path.join(testSource, "src/PolyglotSQLite") + path.sep;
  return { name: "polyglot-sqlite-shared-runtime", enforce: "pre", resolveId(specifier, importer) {
    if (!importer || !specifier.startsWith(".")) return null;
    const resolved = path.resolve(path.dirname(importer), specifier);
    if (resolved.startsWith(duplicatePrefix)) {
      const canonical = path.join(source, path.relative(duplicatePrefix, resolved));
      assertExists(canonical);
      return canonical.replaceAll("\\", "/");
    }
    const runtime = resolved.replaceAll("\\", "/").match(/\/fable_modules\/(fable-library-ts\.[^/]+\/.*)$/);
    if (runtime) {
      const canonical = path.join(source, "fable_modules", runtime[1]);
      if (fs.existsSync(canonical)) return canonical.replaceAll("\\", "/");
    }
    return null;
  } };
}

function checkTypes(filenames) {
  const program = ts.createProgram(filenames, { strict: true, noEmit: true, skipLibCheck: false,
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.NodeNext,
    moduleResolution: ts.ModuleResolutionKind.NodeNext, types: [] });
  diagnosticsOrThrow(ts.getPreEmitDiagnostics(program));
}

async function stage(includeTests) {
  const classes = publicClasses(source, new Set(Object.keys(contracts)));
  for (const name of Object.keys(contracts)) if (!classes.has(name)) throw new Error(`Missing public class ${name}`);
  const declarations = [...classes].map(([name, generated]) => checkedContract(name, generated)).join("\n\n") + "\n";
  const entry = path.join(source, "index.ts");
  write(entry, [...classes].map(([name, generated]) => `export { ${generated.node.name.text} as ${name} } from "./${generated.filename}";`).join("\n") + "\n");
  const entries = { "node_modules/polyglot-sqlite/index": entry };
  let probeTypes;
  if (includeTests) {
    const probe = publicClasses(testSource, new Set(["NumericProbe"])).get("NumericProbe");
    if (!probe) throw new Error("Missing NumericProbe");
    probeTypes = probeDeclarations(probe);
    const probeIndex = path.join(testSource, "probe-index.ts");
    write(probeIndex, `export { NumericProbe } from "./${probe.filename}";\n`);
    entries["node_modules/polyglot-sqlite-probe/index"] = probeIndex;
    entries.tests = path.join(testSource, "Main.ts");
    assertExists(entries.tests);
  }
  await viteBuild({ configFile: false, root: repository, publicDir: false,
    plugins: [sharedRuntimePlugin()], build: { outDir: javascript, emptyOutDir: true,
      minify: false, sourcemap: true, target: "es2022", rollupOptions: { input: entries,
        external: ["better-sqlite3"], preserveEntrySignatures: "strict",
        output: { format: "es", entryFileNames: "[name].js", chunkFileNames: "node_modules/polyglot-sqlite/chunks/[name]-[hash].js" },
      } },
  });
  write(path.join(javascript, "package.json"), '{"private":true,"type":"module"}\n');
  write(path.join(libraryPackage, "package.json"), manifest("polyglot-sqlite"));
  write(path.join(libraryPackage, "index.d.ts"), declarations);
  const typeFiles = [path.join(libraryPackage, "index.d.ts")];
  if (includeTests) {
    write(path.join(probePackage, "package.json"), manifest("polyglot-sqlite-probe"));
    write(path.join(probePackage, "index.d.ts"), probeTypes);
    typeFiles.push(path.join(probePackage, "index.d.ts"));
    for (const name of ["javascript.mjs", "consumer.ts"]) {
      const original = path.join(nativeTests, name);
      assertExists(original);
      write(path.join(javascript, "consumer", name), fs.readFileSync(original, "utf8"));
    }
  }
  checkTypes(typeFiles);
  console.log(`Staged ${classes.size} PolyglotSQLite public classes with checked native declarations.`);
}

function run(executable, args, cwd = repository) {
  const result = spawnSync(executable, args, { cwd, stdio: "inherit" });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${executable} failed with exit code ${result.status}`);
}

function packTest() {
  const packed = path.join(output, "packed-js");
  cleanGenerated(packed);
  const tarballs = path.join(packed, "tarballs");
  fs.mkdirSync(tarballs);
  const args = ["pack", "--ignore-scripts", "--pack-destination", tarballs];
  if (process.platform === "win32") run("cmd.exe", ["/d", "/c", "npm", ...args], libraryPackage);
  else run("npm", args, libraryPackage);
  const archives = fs.readdirSync(tarballs).filter(name => name.endsWith(".tgz"));
  if (archives.length !== 1) throw new Error("Expected exactly one local npm package");
  const installed = path.join(packed, "node_modules/polyglot-sqlite");
  fs.mkdirSync(installed, { recursive: true });
  run("tar", ["-xzf", path.join(tarballs, archives[0]), "--strip-components=1", "-C", installed]);
  fs.cpSync(probePackage, path.join(packed, "node_modules/polyglot-sqlite-probe"), { recursive: true });
  fs.cpSync(path.join(javascript, "consumer"), path.join(packed, "consumer"), { recursive: true });
  write(path.join(packed, "package.json"), '{"private":true,"type":"module"}\n');
  checkTypes([path.join(packed, "consumer/consumer.ts")]);
  run(process.execPath, [path.join(packed, "consumer/javascript.mjs")]);
  console.log("PolyglotSQLite native consumers passed against the locally packed npm artifact.");
}

const command = process.argv[2];
if (command === "build") await stage(false);
else if (command === "tests") await stage(true);
else if (command === "typecheck") checkTypes([path.join(javascript, "consumer/consumer.ts")]);
else if (command === "pack-test") packTest();
else throw new Error("Usage: node build/polyglot-sqlite.mjs build|tests|typecheck|pack-test");
