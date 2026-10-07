import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";
import { build as viteBuild } from "vite";

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const output = path.join(repository, "build/out/base-model");
const source = path.join(output, "ts");
const testSource = path.join(output, "tests-ts");
const javascript = path.join(output, "js");
const modelPackage = path.join(javascript, "node_modules/arc-base-model");
const probePackage = path.join(javascript, "node_modules/arc-base-model-probe");
const nativeTests = path.join(repository, "tests/ARCBaseModel.Native");

const modelExports = {
  Entity: ["EntityObject", "EntityPropertyBag", "EntityCollection", "EntityNull", "EntityBlob", "Entity"],
  DefinedTerm: ["DefinedTerm", "DefinedTermSet", "DefinedTermSetReference"],
  Annotation: ["Annotation", "FormalParameter", "AnnotationValue"],
  Entities: ["Sample", "Data", "EntityReference"],
  Recipe: ["Recipe", "RecipeIntendedUse"],
  Process: ["Process"],
  Descriptor: ["Descriptor"],
  Administrative: ["Organization", "Agent", "ScholarlyArticle"],
  Dataset: ["Dataset"],
};
const alternativeNames = new Set([
  "AnnotationValue", "EntityReference", "RecipeIntendedUse", "DefinedTermSetReference", "Entity",
]);
const modelNames = new Set(Object.values(modelExports).flat());
const probeNames = new Set([
  "AnnotationColumns", "EntityColumns", "IntendedUseColumns", "TermSetColumns", "MappingProbe",
]);

function write(filename, text) {
  fs.mkdirSync(path.dirname(filename), { recursive: true });
  fs.writeFileSync(filename, text, "utf8");
}

function assertExists(filename) {
  if (!fs.existsSync(filename)) {
    throw new Error(`Missing generated input ${filename}. Run the Fable build target first.`);
  }
}

function diagnosticsOrThrow(diagnostics) {
  if (!diagnostics.length) return;
  throw new Error(ts.formatDiagnosticsWithColorAndContext(diagnostics, {
    getCurrentDirectory: () => repository,
    getCanonicalFileName: filename => filename,
    getNewLine: () => "\n",
  }));
}

// Emit the checked public surface from Fable's actual signatures. Runtime helpers,
// reflection functions, and private backing fields are intentionally not exported.
function declarations(inputFiles, expectedNames, modelImports = false) {
  const f = ts.factory;
  const publicNames = new Set([...modelNames, ...probeNames, "Iterable"]);
  const foundNames = new Set();
  const statements = [];
  const exportModifier = f.createModifier(ts.SyntaxKind.ExportKeyword);
  const declareModifier = f.createModifier(ts.SyntaxKind.DeclareKeyword);

  function publicType(node) {
    if (!node) throw new Error("Generated public API has a missing type annotation.");
    if (ts.isTypeReferenceNode(node)) {
      if (!ts.isIdentifier(node.typeName)) throw new Error("Unexpected qualified public type.");
      const name = node.typeName.text;
      const args = node.typeArguments?.map(publicType);
      if (name === "Option") {
        if (args?.length !== 1) throw new Error("Unexpected Option signature.");
        return f.createUnionTypeNode([args[0], f.createKeywordTypeNode(ts.SyntaxKind.UndefinedKeyword)]);
      }
      if (name === "float64" || name === "int32") return f.createKeywordTypeNode(ts.SyntaxKind.NumberKeyword);
      if (!publicNames.has(name)) throw new Error(`Unexpected public type ${name}; explicitly review its native representation.`);
      return f.createTypeReferenceNode(name, args);
    }
    if (ts.isUnionTypeNode(node)) return f.createUnionTypeNode(node.types.map(publicType));
    if (ts.isArrayTypeNode(node)) return f.createArrayTypeNode(publicType(node.elementType));
    if (ts.isParenthesizedTypeNode(node)) return f.createParenthesizedType(publicType(node.type));
    if ([ts.SyntaxKind.StringKeyword, ts.SyntaxKind.NumberKeyword,
         ts.SyntaxKind.BooleanKeyword, ts.SyntaxKind.VoidKeyword,
         ts.SyntaxKind.UndefinedKeyword].includes(node.kind)) {
      return f.createKeywordTypeNode(node.kind);
    }
    throw new Error(`Unsupported public type syntax ${ts.SyntaxKind[node.kind]}; review before staging.`);
  }

  function parameter(node) {
    if (!ts.isIdentifier(node.name)) throw new Error("Unexpected destructured public parameter.");
    return f.createParameterDeclaration(
      undefined, node.dotDotDotToken, node.name.text,
      node.questionToken ?? (node.initializer ? f.createToken(ts.SyntaxKind.QuestionToken) : undefined),
      publicType(node.type), undefined,
    );
  }

  for (const filename of inputFiles) {
    assertExists(filename);
    const parsed = ts.createSourceFile(filename, fs.readFileSync(filename, "utf8"), ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
    diagnosticsOrThrow(parsed.parseDiagnostics);
    for (const statement of parsed.statements) {
      if (!ts.isClassDeclaration(statement) && !ts.isTypeAliasDeclaration(statement)) continue;
      const name = statement.name?.text;
      if (!expectedNames.has(name)) continue;
      if (foundNames.has(name)) throw new Error(`Duplicate public type ${name}.`);
      foundNames.add(name);
      if (ts.isTypeAliasDeclaration(statement)) {
        statements.push(f.createTypeAliasDeclaration([exportModifier], name, undefined, publicType(statement.type)));
        continue;
      }
      if (statement.heritageClauses?.some(clause => clause.types.some(t => t.expression.getText(parsed) !== "EntityObject"))) throw new Error(`Unexpected public inheritance on ${name}.`);
      const members = [];
      if (statement.heritageClauses?.length) members.push(f.createGetAccessorDeclaration(undefined, "Type", [], f.createLiteralTypeNode(f.createStringLiteral(name)), undefined));
      for (const member of statement.members) {
        if (ts.isPropertyDeclaration(member) && member.name.getText(parsed).startsWith("_")) continue;
        if (ts.isConstructorDeclaration(member)) {
          members.push(f.createConstructorDeclaration(undefined, member.parameters.map(parameter), undefined));
        } else if (ts.isGetAccessorDeclaration(member)) {
          const property = member.name.getText(parsed);
          let propertyType = publicType(member.type);
          if (property === "Type" && name !== "EntityObject") {
            const body = member.body?.statements;
            const expression = body?.length === 1 && ts.isReturnStatement(body[0]) ? body[0].expression : undefined;
            if (!expression || !ts.isStringLiteral(expression) || expression.text !== name) {
              throw new Error(`${name}.Type is not a verified fixed entity discriminator.`);
            }
            propertyType = f.createLiteralTypeNode(f.createStringLiteral(expression.text));
          }
          members.push(f.createGetAccessorDeclaration(undefined, property, [], propertyType, undefined));
        } else if (ts.isSetAccessorDeclaration(member)) {
          members.push(f.createSetAccessorDeclaration(undefined, member.name.getText(parsed), member.parameters.map(parameter), undefined));
        } else if (ts.isMethodDeclaration(member)) {
          if (["Reserve", "check"].includes(member.name.getText(parsed))) continue;
          const modifiers = member.modifiers?.filter(modifier => modifier.kind === ts.SyntaxKind.StaticKeyword);
          members.push(f.createMethodDeclaration(modifiers, undefined, member.name.getText(parsed), undefined, undefined,
            member.parameters.map(parameter), publicType(member.type), undefined));
        } else {
          throw new Error(`Unexpected public member in ${name}; review the generated API before staging.`);
        }
      }
      statements.push(f.createClassDeclaration([exportModifier, declareModifier], name, undefined, statement.heritageClauses, members));
    }
  }
  for (const name of expectedNames) {
    if (!foundNames.has(name)) throw new Error(`Generated API is missing ${name}.`);
  }
  const printer = ts.createPrinter({ newLine: ts.NewLineKind.LineFeed });
  const result = printer.printList(ts.ListFormat.MultiLine, f.createNodeArray(statements),
    ts.createSourceFile("index.d.ts", "", ts.ScriptTarget.Latest));
  const imports = modelImports ? `import type { ${[...modelNames].join(", ")} } from "arc-base-model";\n\n` : "";
  return "// Derived from the generated Fable signatures by build/base-model.mjs.\n" + imports + result;
}

function packageManifest(name) {
  return JSON.stringify({
    name, version: "0.0.0", private: true, type: "module",
    exports: { ".": { types: "./index.d.ts", import: "./index.js" } },
    types: "./index.d.ts", main: "./index.js",
  }, null, 2) + "\n";
}

function sameRuntimePlugin() {
  const modelPrefix = path.join(testSource, "src/ARCBaseModel") + path.sep;
  return {
    name: "arc-base-model-shared-runtime",
    enforce: "pre",
    resolveId(specifier, importer) {
      if (!importer || !specifier.startsWith(".")) return null;
      const resolved = path.resolve(path.dirname(importer), specifier);
      if (resolved.startsWith(modelPrefix)) {
        const canonical = path.join(source, path.relative(path.join(testSource, "src/ARCBaseModel"), resolved));
        assertExists(canonical);
        return canonical.replaceAll("\\", "/");
      }
      // Model and test compilation use the same pinned copied Fable runtime.
      const runtimePart = resolved.replaceAll("\\", "/").match(/\/fable_modules\/(fable-library-ts\.[^/]+\/.*)$/);
      if (runtimePart) {
        const canonical = path.join(source, "fable_modules", runtimePart[1]);
        if (fs.existsSync(canonical)) return canonical.replaceAll("\\", "/");
      }
      return null;
    },
  };
}

function checkTypes(filenames) {
  const program = ts.createProgram(filenames, {
    strict: true,
    noEmit: true,
    skipLibCheck: false,
    target: ts.ScriptTarget.ES2022,
    module: ts.ModuleKind.NodeNext,
    moduleResolution: ts.ModuleResolutionKind.NodeNext,
    types: [],
  });
  diagnosticsOrThrow(ts.getPreEmitDiagnostics(program));
}

async function stage(includeTests) {
  const inputFiles = Object.keys(modelExports).map(module => path.join(source, `${module}.ts`));
  const modelTypes = declarations(inputFiles, modelNames);
  const modelIndex = path.join(source, "index.ts");
  write(modelIndex, Object.entries(modelExports).map(([module, names]) => {
    const classes = names.filter(name => !alternativeNames.has(name));
    const aliases = names.filter(name => alternativeNames.has(name));
    return `export { ${classes.join(", ")} } from "./${module}.ts";\n`
      + (aliases.length ? `export type { ${aliases.join(", ")} } from "./${module}.ts";\n` : "");
  }).join(""));

  const entries = { "node_modules/arc-base-model/index": modelIndex };
  let probeTypes;
  if (includeTests) {
    const probe = path.join(testSource, "MappingProbe.ts");
    const runner = path.join(testSource, "Main.ts");
    assertExists(runner);
    probeTypes = declarations([probe], probeNames, true);
    const probeIndex = path.join(testSource, "probe-index.ts");
    write(probeIndex, `export { ${[...probeNames].join(", ")} } from "./MappingProbe.ts";\n`);
    entries["node_modules/arc-base-model-probe/index"] = probeIndex;
    entries.tests = runner;
  }

  await viteBuild({
    configFile: false,
    root: repository,
    publicDir: false,
    plugins: [sameRuntimePlugin()],
    build: {
      outDir: javascript,
      emptyOutDir: true,
      minify: false,
      sourcemap: true,
      target: "es2022",
      rollupOptions: {
        input: entries,
        preserveEntrySignatures: "strict",
        output: {
          format: "es",
          entryFileNames: "[name].js",
          chunkFileNames: "node_modules/arc-base-model/chunks/[name]-[hash].js",
        },
      },
    },
  });
  write(path.join(javascript, "package.json"), JSON.stringify({ private: true, type: "module" }, null, 2) + "\n");
  write(path.join(modelPackage, "package.json"), packageManifest("arc-base-model"));
  write(path.join(modelPackage, "index.d.ts"), modelTypes);
  const typeFiles = [path.join(modelPackage, "index.d.ts")];
  if (includeTests) {
    write(path.join(probePackage, "package.json"), packageManifest("arc-base-model-probe"));
    write(path.join(probePackage, "index.d.ts"), probeTypes);
    typeFiles.push(path.join(probePackage, "index.d.ts"));
    for (const filename of ["javascript.mjs", "consumer.ts"]) {
      const original = path.join(nativeTests, filename);
      assertExists(original);
      write(path.join(javascript, "consumer", filename), fs.readFileSync(original, "utf8"));
    }
  }
  checkTypes(typeFiles);
  console.log(`Staged ARCBaseModel JavaScript and checked declarations${includeTests ? ", shared tests, and mapping probe" : ""}.`);
}

const command = process.argv[2];
if (command === "build") {
  await stage(false);
} else if (command === "tests") {
  await stage(true);
} else if (command === "typecheck") {
  const consumer = process.argv[3] ?? path.join(javascript, "consumer/consumer.ts");
  assertExists(consumer);
  checkTypes([consumer]);
  console.log("ARCBaseModel native TypeScript consumer passed strict checking.");
} else {
  throw new Error("Usage: node build/base-model.mjs build|tests|typecheck");
}
