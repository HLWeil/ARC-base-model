// Syntax adaptation only. All mutation, validation and transaction logic stays
// in the shared implementation. Proxies retain the canonical class prototypes.
const exposed = new WeakMap();
const underlying = new WeakMap();
const groupNames = new Set(["Entity", "Dataset", "Process", "Sample", "Data", "Recipe", "Annotation",
  "FormalParameter", "DefinedTerm", "DefinedTermSet", "Descriptor", "Agent", "Organization", "ScholarlyArticle", "History"]);

function raw(value) { return underlying.get(value) ?? value; }
function option(value, key) {
  if (value === undefined) return false;
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new TypeError(`Expected { ${key}?: boolean }`);
  for (const name of Reflect.ownKeys(value)) if (name !== key) throw new TypeError(`Unknown option: ${String(name)}`);
  const descriptor = Object.getOwnPropertyDescriptor(value, key);
  if (descriptor && !("value" in descriptor)) throw new TypeError("Options must contain ordinary data properties");
  const flag = descriptor?.value;
  if (descriptor && typeof flag !== "boolean") throw new TypeError(`${key} must be a boolean`);
  return flag ?? false;
}

function wrap(value, kind) {
  if (value === undefined || value === null) return value;
  if (exposed.has(value)) return exposed.get(value);
  const methods = new Map();
  const proxy = new Proxy(value, {
    get(target, key, receiver) {
      if (groupNames.has(key)) return wrap(Reflect.get(target, key, target), "group");
      if (key === "ManagedGraph") return wrap(Reflect.get(target, key, target), "graph");
      const member = Reflect.get(target, key, target);
      if (typeof member !== "function") return member;
      if (!methods.has(key)) methods.set(key, (...args) => {
        if (kind === "group" && (key === "upsert" || key === "delete")) {
          if (args.length > 3) throw new TypeError("Unexpected arguments");
          args[2] = option(args[2], key === "upsert" ? "recursive" : "cascade");
        }
        const result = Reflect.apply(member, target, args.map(raw));
        return kind === "session" && ["createArc", "openArc", "importYaml"].includes(key)
          ? wrap(result, "graph") : result;
      });
      return methods.get(key);
    },
  });
  exposed.set(value, proxy);
  underlying.set(proxy, value);
  return proxy;
}

export function nativeClass(implementation, kind) {
  return new Proxy(implementation, {
    get(target, key) {
      const member = Reflect.get(target, key, target);
      const factories = kind === "session" ? ["createInMemory", "createFile", "openFile"]
        : ["bind", "fromGraph", "create", "openFolder", "importFolder"];
      if (!factories.includes(key)) return member;
      return (...args) => wrap(Reflect.apply(member, target, args.map(raw)), kind);
    },
    construct() { throw new TypeError("Use the public factory methods"); },
  });
}
