import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import ts from "typescript";
import { build as viteBuild } from "vite";

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const output = path.join(repository, "build/out/arc-session");
const generated = path.join(output, "tests-ts");
const source = path.join(generated, "src/ARCtrl");
const javascript = path.join(output, "js");
const modelPackage = path.join(repository, "build/out/base-model/js/node_modules/arc-base-model");
const sqlitePackage = path.join(repository, "build/out/polyglot-sqlite/js/node_modules/polyglot-sqlite");
const modelTypes = path.join(repository, "build/out/base-model/js/node_modules/arc-base-model/index.d.ts");
const groups = ["Entity", "Dataset", "Process", "Sample", "Data", "Recipe", "Annotation", "FormalParameter",
  "DefinedTerm", "DefinedTermSet", "Descriptor", "Agent", "Organization", "ScholarlyArticle", "History"];
const exports = new Map([
  ["ARC", "ARC.ts"], ["AppliedOperation", "AppliedOperation.ts"],
  ["Session", "ARCSession/Session.ts"], ["ArcInfo", "ARCSession/Session.ts"],
  ...groups.map(name => [`${name}Operations`, `Operations/${name}Operations.ts`]),
]);
const hidden = new Set(["Wrap", "Dispose", "Repository"]);

function write(filename, contents) {
  fs.mkdirSync(path.dirname(filename), { recursive: true });
  fs.writeFileSync(filename, contents, "utf8");
}
function diagnosticsOrThrow(diagnostics) {
  if (diagnostics.length) throw new Error(ts.formatDiagnosticsWithColorAndContext(diagnostics, {
    getCurrentDirectory: () => repository, getCanonicalFileName: filename => filename, getNewLine: () => "\n",
  }));
}
function checkTypes(files, emit = false) {
  const program = ts.createProgram(files, { strict: true, noEmit: !emit, skipLibCheck: false,
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.NodeNext,
    moduleResolution: ts.ModuleResolutionKind.NodeNext, types: [] });
  diagnosticsOrThrow(ts.getPreEmitDiagnostics(program));
  if (emit) diagnosticsOrThrow(program.emit().diagnostics);
}

// Read public signatures from the emitted classes. Never export internal
// constructors, identity maps or compiler-generated helper functions.
function declarations() {
  const modelNames = new Set([...fs.readFileSync(modelTypes, "utf8").matchAll(/export (?:declare )?(?:class|type) (\w+)/g)].map(match => match[1]));
  const names = new Set([...exports.keys(), ...modelNames, "Iterable"]);
  function type(node, parsed) {
    if (!node) throw new Error("Missing public type annotation");
    if (ts.isTypeReferenceNode(node)) {
      const name = node.typeName.getText(parsed);
      const arguments_ = node.typeArguments?.map(argument => type(argument, parsed)) ?? [];
      if (["float64", "int32"].includes(name)) return "number";
      if (name === "Option") return `(${arguments_[0]} | undefined)`;
      if (!names.has(name)) throw new Error(`Unexpected public type ${name}`);
      return name + (arguments_.length ? `<${arguments_.join(", ")}>` : "");
    }
    if (ts.isArrayTypeNode(node)) return `(${type(node.elementType, parsed)})[]`;
    if (ts.isUnionTypeNode(node)) return node.types.map(part => type(part, parsed)).join(" | ");
    if ([ts.SyntaxKind.StringKeyword, ts.SyntaxKind.NumberKeyword, ts.SyntaxKind.BooleanKeyword,
      ts.SyntaxKind.VoidKeyword, ts.SyntaxKind.UndefinedKeyword].includes(node.kind)) return node.getText(parsed);
    throw new Error(`Unexpected public type syntax ${ts.SyntaxKind[node.kind]}`);
  }
  const classes = [];
  for (const [name, filename] of exports) {
    const parsed = ts.createSourceFile(filename, fs.readFileSync(path.join(source, filename), "utf8"), ts.ScriptTarget.Latest, true);
    diagnosticsOrThrow(parsed.parseDiagnostics);
    const node = parsed.statements.find(statement => ts.isClassDeclaration(statement) && statement.name?.text === name);
    if (!node) throw new Error(`Missing generated class ${name}`);
    const members = ["  private constructor();"];
    for (const member of node.members) {
      const memberName = member.name?.getText(parsed);
      if (!memberName || hidden.has(memberName)) continue;
      if (ts.isGetAccessorDeclaration(member)) members.push(`  readonly ${memberName}: ${type(member.type, parsed)};`);
      if (ts.isMethodDeclaration(member)) {
        const parameters = member.parameters.map(parameter => {
          // ARC precedes the public Session in F# compile order. The internal
          // owner interface bridges it; the curated native contract is typed.
          const parameterType = name === "ARC" && memberName === "importFolder" && parameter.name.getText(parsed) === "session"
            ? "Session" : type(parameter.type, parsed);
          return `${parameter.name.getText(parsed)}${parameter.questionToken || parameter.initializer ? "?" : ""}: ${parameterType}`;
        });
        const static_ = member.modifiers?.some(modifier => modifier.kind === ts.SyntaxKind.StaticKeyword) ? "static " : "";
        members.push(`  ${static_}${memberName}(${parameters.join(", ")}): ${type(member.type, parsed)};`);
      }
    }
    classes.push(`export declare class ${name} {\n${members.join("\n")}\n}`);
  }
  return `import type { ${[...modelNames].join(", ")} } from "arc-base-model";\n\n${classes.join("\n\n")}\n`;
}

async function stage() {
  const declaration = declarations();
  const entry = path.join(generated, "arc-session-index.ts");
  write(entry, [...exports].map(([name, filename]) => `export { ${name} } from "./src/ARCtrl/${filename}";`).join("\n") + "\n");
  // All compiled consumers use the existing curated package classes. This also
  // keeps dependency bundles independent when packing the three artifacts.
  const canonical = new Map([["ARCBaseModel", "arc-base-model"], ["PolyglotSQLite", "polyglot-sqlite"]]);
  await viteBuild({ configFile: false, root: repository, publicDir: false, plugins: [{
    name: "arc-session-shared-classes", enforce: "pre", resolveId(specifier, importer) {
      if (!importer || !specifier.startsWith(".")) return null;
      const resolved = path.resolve(path.dirname(importer), specifier);
      for (const [name, packageName] of canonical) {
        const prefix = path.join(generated, "src", name) + path.sep;
        if (resolved.startsWith(prefix)) return {id:packageName, external:true};
      }
      return null;
    },
  }], build: { outDir: javascript, emptyOutDir: true, minify: false, sourcemap: true, target: "es2022",
    rollupOptions: { input: {
      "node_modules/arc-session/index": entry, tests: path.join(generated, "Main.ts"),
    }, external: id => id.startsWith("node:") || ["arc-base-model", "polyglot-sqlite", "better-sqlite3"].includes(id), preserveEntrySignatures: "strict",
    output: { format: "es", entryFileNames: "[name].js", chunkFileNames: "node_modules/arc-session/chunks/[name]-[hash].js" } },
  } });
  write(path.join(javascript, "package.json"), '{"private":true,"type":"module"}\n');
  const directory = path.join(javascript, "node_modules/arc-session");
  write(path.join(directory, "package.json"), JSON.stringify({name:"arc-session", version:"0.0.0", private:true, type:"module",
    exports:{".":{types:"./index.d.ts",import:"./index.js"}},types:"./index.d.ts",
    dependencies:{"arc-base-model":"0.0.0","polyglot-sqlite":"0.0.0"}}, null, 2));
  write(path.join(directory,"index.d.ts"), declaration);
  fs.cpSync(modelPackage, path.join(javascript,"node_modules/arc-base-model"), {recursive:true});
  fs.cpSync(sqlitePackage, path.join(javascript,"node_modules/polyglot-sqlite"), {recursive:true});
  for (const filename of ["Native.ts", "Native.mjs"]) write(path.join(javascript, "consumer", filename), fs.readFileSync(path.join(repository,"tests/ManagementPrototype.Tests",filename),"utf8"));
  checkTypes([path.join(javascript,"consumer/Native.ts")], true);
  console.log(`Staged ${exports.size} ARC toolbox classes and checked native TypeScript declarations.`);
}

function run(executable, args, cwd=repository) {
  const result = spawnSync(executable, args, {cwd,stdio:"inherit"});
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${executable} failed with exit code ${result.status}`);
}
function packTest() {
  const packed = path.join(output,"packed-js");
  if (!path.resolve(packed).startsWith(output + path.sep)) throw new Error("Invalid generated output path");
  fs.rmSync(packed,{recursive:true,force:true});
  const tarballs = path.join(packed,"tarballs");
  fs.mkdirSync(tarballs,{recursive:true});
  for (const name of ["arc-session","arc-base-model","polyglot-sqlite"]) {
    const args = ["pack","--ignore-scripts","--pack-destination",tarballs];
    const directory = path.join(javascript,"node_modules",name);
    if (process.platform === "win32") run("cmd.exe",["/d","/c","npm",...args],directory);
    else run("npm",args,directory);
    const installed = path.join(packed,"node_modules",name);
    fs.mkdirSync(installed,{recursive:true});
    run("tar",["-xzf",path.join(tarballs,`${name}-0.0.0.tgz`),"--strip-components=1","-C",installed]);
  }
  fs.cpSync(path.join(javascript,"consumer"),path.join(packed,"consumer"),{recursive:true});
  write(path.join(packed,"package.json"),'{"private":true,"type":"module"}\n');
  checkTypes([path.join(packed,"consumer/Native.ts")]);
  for (const filename of ["Native.js","Native.mjs"]) run(process.execPath,[path.join(packed,"consumer",filename)]);
  console.log("ARC toolbox native consumers passed against locally packed npm artifacts.");
}

const command = process.argv[2];
if (command === "build") await stage();
else if (command === "typecheck") checkTypes([path.join(javascript,"consumer/Native.ts")]);
else if (command === "pack-test") packTest();
else throw new Error("Usage: node build/arc-session.mjs build|typecheck|pack-test");
