---
title: ARC Workspace Project File
category: Specification
categoryindex: 3
index: 5
---

# ARC Workspace Project File

## Status and scope

This document specifies the ARC workspace project file and its workspace-profile
language.

The project file maps ARC `Dataset` values to registered bidirectional codecs at
workspace-relative anchor paths and may declare named auxiliary files relative
to those anchors. It is storage configuration, not an ARC model serialization.

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**,
**SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **NOT RECOMMENDED**, **MAY**, and
**OPTIONAL** in this document are to be interpreted as described in
[BCP 14](https://www.rfc-editor.org/info/bcp14) when, and only when, they appear
in all capitals.

The non-normative design and implementation plans are
[`plans/project_file.md`](../../plans/project_file.md) and
[`plans/project_file_handling.md`](../../plans/project_file_handling.md).

## 1. Model

```text
project/profile rule
        =
Dataset target + exact codec ID + anchor path
```

A rule selects one of:

- the root Dataset;
- one direct child of the root with an exact identifier; or
- direct children of the root with an exact `additionalType`.

One codec invocation reads or writes each selected Dataset. A selected Dataset
MAY contain nested Datasets. Project selectors do not select or constrain that
deeper nesting.

When direct root children have their own prepared bindings, they are external
children of the root codec invocation. The root codec receives their identifiers
in its codec context and MUST NOT serialize complete inline copies of those
children. Each child binding remains a complete Dataset invocation.

Scientific payloads referenced by [`Data`](shared/Data.md), such as CSV,
Parquet, images, or instrument files, are not managed resources under this
specification.

### 1.1 Terms

**Workspace root**
: The local directory containing `.arc`.

**Project file**
: The storage configuration at `.arc/project.yml`.

**Workspace profile**
: A reusable declarative collection of storage rules.

**Rule**
: A mapping among a rule ID, codec ID, Dataset target, and anchor path.

**Anchor**
: The project-visible primary resource path for one codec invocation.

**Auxiliary file**
: An optional named resource resolved relative to an anchor's directory. It is
  either codec-managed or a project-managed empty file.

**Exact target**
: An `identifier` target selecting one named direct child of the root.

**Type target**
: An `additionalType` target selecting otherwise-unclaimed matching direct
  children.

## 2. Conformance

### 2.1 Project document

A conforming project document:

- is valid YAML;
- has `type: ArcWorkspaceProject`;
- satisfies the structural requirements in this document;
- resolves its referenced workspace profiles and project-local replacements to
  exactly one root rule;
- satisfies all profile, target, codec, path, and collision requirements; and
- contains no unknown fields.

An `ArcWorkspaceProject` has no document version field in this specification.

### 2.2 Workspace-profile document

A conforming workspace profile:

- is valid YAML;
- has `type: ArcWorkspaceProfile`;
- declares an exact `id` and `version`;
- contains at least one conforming rule; and
- contains no unknown fields.

Profile `version` identifies the profile release. This specification does not
define a separate `specVersion`.

A referenced YAML document MUST conform as a workspace-profile document.

## 3. Workspace and path domain

The canonical project path is:

```text
<workspace-root>/.arc/project.yml
```

The parent of `.arc` is the workspace root. Referenced profile documents do not
create nested workspace roots or additional project entry points.

Metadata anchors MUST be inside the workspace root. Local referenced profiles
MUST be inside `.arc`. Manifest filesystem paths use `/` regardless of the host
operating system.

The root project is local. Referenced profiles MAY be local files or HTTP(S)
resources. Project and profile documents are configuration and MUST NOT be
implicitly rewritten or deleted by project handling.

## 4. YAML data model

Field names and identifier comparisons are case-sensitive. Duplicate YAML
mapping keys and unknown fields MUST be rejected.

YAML aliases MAY be resolved by a parser, but validation applies to the expanded
value. YAML tags MUST NOT cause executable behavior or dynamic type loading.

Unless a field says otherwise, strings MUST NOT be implicitly coerced from
numbers, booleans, or null.

Project, profile, rule, and codec IDs MUST:

- begin with an ASCII letter;
- contain only ASCII letters, digits, `.`, `_`, or `-`; and
- contain no more than 128 characters.

Profile versions MUST be quoted strings that begin with an ASCII letter or
digit, contain only ASCII letters, digits, `.`, `_`, `+`, or `-`, and contain no
more than 64 characters.

The JSON Schema draft 2020-12 representations are:

- [project schema](../../schemas/yml/arc-workspace-project.schema.yml); and
- [workspace-profile schema](../../schemas/yml/arc-workspace-profile.schema.yml).

The schemas describe structural constraints. This document remains authoritative
for cross-document, registry, target, resource, and concrete path constraints.

## 5. Project document

### 5.1 Shape

```yaml
type: ArcWorkspaceProject

workspaceProfiles:
  - url: "https://example.org/arc/isa-xlsx-scaffold.yml"

rules: []
```

Fields:

| Field | Type | Required | Default |
|---|---|---:|---|
| `type` | string | yes | none |
| `workspaceProfiles` | sequence of profile references | no | empty |
| `rules` | sequence of rules | no | empty |

At least one profile reference or project-local rule MUST be declared.

After profile expansion and project-local replacement, the effective rules MUST
contain exactly one root rule.

### 5.2 Workspace-profile references

```yaml
workspaceProfiles:
  - file: profiles/local-layout.yml
  - url: "https://example.org/arc/remote-layout.yml"
```

Each entry MUST contain exactly one `file` or `url`. `file` is relative to
`.arc` and MUST remain inside it. `url` MUST be an absolute HTTP(S) URL.

The referenced YAML MUST be an `ArcWorkspaceProfile`. Its declared `id` MUST be
unique in the project. Profiles contribute rules in listed order. Project-local
rules are applied as described below and then follow the retained profile rules.
Order does not resolve target or path conflicts.

### 5.3 Project-local rule replacement

Before rule qualification and cross-rule validation, an implementation MUST
collect the targets of all project-local rules. It MUST omit every
profile-contributed rule with an equal target. It MUST then append the
project-local rules to form the effective rule set.

Target equality means `root` matches `root`, `identifier` matches the same
case-sensitive identifier, and `additionalType` matches the same case-sensitive
type. Targets of different kinds do not match. A local rule is a whole-rule
replacement: the processor MUST NOT inherit or merge the profile rule's codec,
path, files, or other fields. A local target replaces every matching rule
contributed by the referenced profiles. Rules with other targets remain
effective.

Profile rules do not replace one another. Profile-to-profile target and path
conflicts remain errors unless matching project-local targets remove the
conflicting profile rules before validation.

### 5.4 Rule qualification

Project-local rule IDs MUST be unique within `rules`. Their qualified IDs are:

```text
project#<rule-id>
```

Profile rules are qualified as:

```text
<profile-id>#<rule-id>
```

Qualified IDs provide stable rule identities during project resolution and
execution.

## 6. Workspace profiles

### 6.1 Shape

```yaml
type: ArcWorkspaceProfile
id: org.example.layout
version: "1.0"
description: Example layout

rules:
  - id: root
    codec: example.yml
    target: root
    path: metadata.yml
```

Fields:

| Field | Type | Required |
|---|---|---:|
| `type` | string | yes |
| `id` | profile ID | yes |
| `version` | profile version | yes |
| `description` | string | no |
| `rules` | non-empty sequence of rules | yes |

Rule IDs MUST be unique within the profile.

Profiles do not have parameters, codec options, enabled flags, or extension
fields. They cannot override one another. A project may only replace a
profile-contributed rule through the exact-target, whole-rule mechanism in section
5.3.

## 7. Rules

### 7.1 Shape

```yaml
- id: study
  codec: isa.study.xlsx
  target:
    additionalType: Study
  path: "studies/{dataset.identifier}/isa.study.xlsx"
  files:
    - id: datamap
      path: isa.datamap.xlsx
```

Fields:

| Field | Type | Required |
|---|---|---:|
| `id` | rule ID | yes |
| `codec` | codec ID | yes |
| `target` | target | yes |
| `path` | path template | yes |
| `files` | sequence of auxiliary files | no |

The first four fields are required. No other field is allowed.

Every rule is bidirectional. Its codec and path are used for both reading and
writing.

### 7.2 Auxiliary files

```yaml
files:
  - id: datamap
    path: isa.datamap.xlsx
  - id: dataset-placeholder
    path: dataset/.gitkeep
    create: empty
```

Each entry MUST contain an `id` and `path`, and MAY contain
`create: empty`. No other fields are allowed. File IDs MUST satisfy the ID
syntax in section 4 and MUST be unique within the rule.

An auxiliary path is relative to the resolved anchor's directory, uses only
literal safe segments, and MUST NOT contain `{dataset.identifier}` or another
capture. It follows the safety requirements in section 9.4.

Codec-managed auxiliary files, which omit `create`, are OPTIONAL on read. The
filesystem layer supplies each existing file to the codec by logical ID. On
write, the filesystem layer writes only declared IDs returned by the codec. A
codec output with an undeclared ID, or with the ID of a project-managed file,
is an error before any resource from that invocation is written.

An entry with `create: empty` is project-managed. After a successful codec
encoding, the filesystem layer MUST emit it as a zero-byte file. Arbitrary
generated file content is not supported.

## 8. Targets

A target is exactly one of the three forms in this section.

### 8.1 Root

```yaml
target: root
```

The root target selects the ARC root Dataset. The expanded project MUST contain
exactly one root rule.

On read, the root rule MUST resolve to exactly one anchor and that anchor MUST
successfully produce one Dataset for a usable ARC result.

On write, the rule selects the one root Dataset.

### 8.2 Identifier

```yaml
target:
  identifier: special-study
```

An identifier target selects exactly one direct child of the root whose
identifier is equal to the declared value.

On read:

- the rule MUST resolve to exactly one anchor;
- the anchor is required;
- the parsed Dataset identifier MUST equal the declared value; and
- the Dataset is attached directly to the root.

On write, absence of the named direct child is a target error.

Two expanded identifier rules MUST NOT declare the same identifier.

### 8.3 Additional type

```yaml
target:
  additionalType: Study
```

An additional-type target selects zero or more direct children of the root whose
`additionalType` is present and exactly equals the declared case-sensitive
value.

Every parsed Dataset MUST have the declared `additionalType`. A mismatch fails
that resource.

Two expanded additional-type rules MUST NOT declare the same value.

### 8.4 Identifier precedence

All expanded identifier targets are reserved before additional-type selection.

If a direct child has an identifier target and also matches an additional-type
target:

- the identifier rule selects it;
- the additional-type rule MUST exclude it; and
- no duplicate representation is created.

This precedence is independent of rule and profile order.

During read discovery, an additional-type binding whose captured identifier is
reserved by an identifier rule MUST be excluded from that type rule. A resource
at the exact rule's anchor remains required; discovery through the general path
does not satisfy the exact rule.

### 8.5 Dataset depth

These targets select only the root and its direct project-level children. A
selected Dataset MAY contain deeper nested Datasets. A processor MUST preserve
such nesting returned by the selected Dataset's codec and MUST NOT flatten it
because of project target semantics.

On write, only direct root children selected by prepared non-root bindings are
externalized from the root representation. Unselected direct children and
Datasets nested below a selected child remain part of the containing codec
invocation.

## 9. Path templates

### 9.1 Grammar

A path template is a `/`-separated sequence of non-empty segments. A segment is:

- a non-empty literal; or
- a template segment containing `{dataset.identifier}` exactly once, optionally
  preceded or followed by literal text.

The capture MAY occur at most once in the entire path.

Valid:

```text
studies/{dataset.identifier}/isa.study.xlsx
assays/isa.assay_{dataset.identifier}.yml
```

Invalid:

```text
studies/{dataset.identifier}/{dataset.identifier}.xlsx
studies/{parent.identifier}.xlsx
```

Version 1 does not support `{parent.identifier}`, parameter references, globs,
regular expressions, environment expansion, command substitution, URI
templates, or general template languages.

### 9.2 Target-specific requirements

- A root path MAY be literal or contain `{dataset.identifier}`.
- An identifier-target path MAY be literal or contain
  `{dataset.identifier}`.
- An additional-type path MUST contain `{dataset.identifier}` exactly once.

Root and identifier paths resolve to exactly one anchor. Additional-type paths
may match zero or more anchors.

For a root or identifier rule with a capture, discovery of more than one anchor
is an error even if one resource later fails parsing.

### 9.3 Matching and rendering

On read, literal segments and the literal prefix and suffix around a capture
match exactly. The text matched by the capture MUST be non-empty, MUST be a safe
path segment when considered independently, and yields the candidate Dataset
identifier. For example, `assay_alpha.yml` matched by
`assay_{dataset.identifier}.yml` captures `alpha`.

When a capture is present, the parsed Dataset identifier MUST equal the captured
value. An identifier target additionally requires equality with its declared
identifier.

On write, `{dataset.identifier}` renders the selected Dataset identifier. A
rendered identifier MUST be a safe path segment.

### 9.4 Safety

Every anchor and local referenced-document path MUST:

- be relative to its defined base;
- use `/` separators in the document;
- reject absolute, drive-qualified, UNC, and URI forms;
- reject backslashes, NUL, empty, `.`, and `..` segments;
- remain within its base after normalization; and
- reject traversal through a symlink or reparse point outside its base.

Collision identity uses normalized resolved paths and the host filesystem's
effective case comparison.

Auxiliary paths apply these requirements relative to the resolved anchor
directory. Anchors and all auxiliary files participate in static and concrete
collision analysis, including collisions within one rule invocation.

## 10. Codecs and anchors

### 10.1 Exact codec lookup

`codec` is an exact registered capability ID. A processor MUST NOT infer or
replace it based on extension, media type, content, workbook sheets, discovery
order, or another rule.

A missing codec is a project resolution error.

Every codec used by a rule MUST support both reading and writing a complete
Dataset at the same anchor.

### 10.2 Built-in codec IDs

The built-in `CodecRegistry.standard` provides:

```text
isa.investigation.xlsx
isa.study.xlsx
isa.assay.xlsx
isa.workflow.xlsx
isa.run.xlsx
dataset.yml
```

`dataset.yml` reads and writes UTF-8 YAML through the lenient Dataset YAML
parser. It stores `dataContexts` directly in the primary document and does not
produce a separate Datamap resource.

The registry name describes the built-in codec set; it does not designate an
ISA storage profile as the standard ARC representation. The most basic
project-backed representation is one root `dataset.yml` rule writing
`arc.yml`.

### 10.3 Declared resource contract

The filesystem layer MUST read the required primary anchor and every existing
declared auxiliary file before codec invocation. The codec receives primary
content plus a map of auxiliary content keyed by file ID; it does not derive
filesystem paths.

On write, the codec returns required primary content and zero or more
codec-managed auxiliary outputs. The filesystem layer validates all output IDs
before writing that invocation, then writes the primary, returned auxiliary
files, and project-managed empty files.

The codec context includes `ExternalChildIdentifiers`. For a root invocation,
this is the exact set of direct child identifiers represented by prepared
non-root bindings in the current operation; it is empty for child invocations.
A root codec MUST avoid emitting a competing complete inline representation for
those identifiers. The built-in `dataset.yml` codec omits those children from
the root document's top-level `hasPart` while leaving the in-memory Dataset
unchanged.

An absent optional auxiliary file is not an error. Generic project handling
does not automatically delete a stale anchor or auxiliary file omitted from a
later write.

### 10.4 Child assembly

On read, every non-root resource is decoded and validated before the root.
Two decoded non-root resources MUST NOT resolve to the same Dataset identifier,
because neither external resource has precedence.

After a child resource has decoded and passed its identifier and
`additionalType` checks, it is authoritative over an inline direct root child
with the same identifier. The processor MUST remove the inline child and attach
the external child through the normal Dataset graph APIs. It MUST NOT merge
fields or collections from the two values.

## 11. Cross-rule validation

After profile expansion and project-local replacement, a conforming project
MUST reject the effective rule set when it contains:

- zero or more than one root rule;
- duplicate identifier target values;
- duplicate additional-type target values;
- duplicate qualified rule IDs;
- missing or incompatible codecs;
- invalid target/path combinations;
- unsafe templates or rendered anchors; and
- duplicate auxiliary IDs, unsafe auxiliary paths, or unsupported creation
  policies;
- undeclared or project-managed codec output IDs; and
- duplicate Dataset identifiers across decoded non-root resources; and
- statically identical or concretely colliding anchors and auxiliary files.

An identifier target and an additional-type target are not a target conflict.
Identifier precedence makes their selection domains disjoint.

All anchor and auxiliary-file collisions MUST be detected before invoking a
codec for the affected operation. Rule order MUST NOT choose a winner.

## 12. Profile examples

Profiles choose a storage layout and any decorations applied to the ARC data
model. No profile in this section is the universal or preferred ARC layout.
The basic profile stores the complete ARC as one Dataset YAML document. The ISA
profiles are optional layouts for ARCs using ISA decorations.

### 12.1 Basic single-file ARC YAML

```yaml
type: ArcWorkspaceProfile
id: arc.yml
version: "1.0"
description: Basic single-file ARC Dataset YAML

rules:
  - id: arc
    codec: dataset.yml
    target: root
    path: arc.yml
```

With no non-root bindings, `dataset.yml` writes the complete nested Dataset
graph recursively to `arc.yml`, including `dataContexts`.

### 12.2 ISA-XLSX decoration scaffold

One possible profile for an ISA-decorated ARC is provided as the
[ISA-XLSX workspace profile](isa_xlsx_workspace_profile.yml):

```yaml
type: ArcWorkspaceProfile
id: arc.isa.xlsx.scaffold
version: "1.0"
description: ISA-XLSX decoration scaffold

rules:
  - id: investigation
    codec: isa.investigation.xlsx
    target: root
    path: isa.investigation.xlsx

  - id: study
    codec: isa.study.xlsx
    target:
      additionalType: Study
    path: "studies/{dataset.identifier}/isa.study.xlsx"
    files:
      - id: datamap
        path: isa.datamap.xlsx
      - id: resources-placeholder
        path: resources/.gitkeep
        create: empty
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty

  - id: assay
    codec: isa.assay.xlsx
    target:
      additionalType: Assay
    path: "assays/{dataset.identifier}/isa.assay.xlsx"
    files:
      - id: datamap
        path: isa.datamap.xlsx
      - id: dataset-placeholder
        path: dataset/.gitkeep
        create: empty
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty

  - id: workflow
    codec: isa.workflow.xlsx
    target:
      additionalType: Workflow
    path: "workflows/{dataset.identifier}/isa.workflow.xlsx"
    files:
      - id: datamap
        path: isa.datamap.xlsx
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty

  - id: run
    codec: isa.run.xlsx
    target:
      additionalType: Run
    path: "runs/{dataset.identifier}/isa.run.xlsx"
    files:
      - id: datamap
        path: isa.datamap.xlsx
      - id: dataset-placeholder
        path: dataset/.gitkeep
        create: empty
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty
```

The `datamap` files are optional codec-managed resources. The ISA-XLSX codec
emits one only when the selected Dataset has Datamap content. The declared
`dataset-placeholder`, `resources-placeholder`, and `protocols-placeholder`
files are always emitted as empty files.

The profile MAY be referenced by `file` or `url`.

### 12.3 ISA Dataset-YAML decoration scaffold

The same optional ISA decoration layout can use Dataset YAML instead of
workbooks. The complete profile is provided as the
[ISA Dataset-YAML workspace profile](isa_yml_workspace_profile.yml):

```yaml
type: ArcWorkspaceProfile
id: arc.isa.yml.scaffold
version: "1.0"
description: ISA Dataset-YAML decoration scaffold

rules:
  - id: investigation
    codec: dataset.yml
    target: root
    path: isa.investigation.yml

  - id: study
    codec: dataset.yml
    target:
      additionalType: Study
    path: "studies/{dataset.identifier}/isa.study.yml"
    files:
      - id: resources-placeholder
        path: resources/.gitkeep
        create: empty
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty

  - id: assay
    codec: dataset.yml
    target:
      additionalType: Assay
    path: "assays/{dataset.identifier}/isa.assay.yml"
    files:
      - id: dataset-placeholder
        path: dataset/.gitkeep
        create: empty
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty

  - id: workflow
    codec: dataset.yml
    target:
      additionalType: Workflow
    path: "workflows/{dataset.identifier}/isa.workflow.yml"
    files:
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty

  - id: run
    codec: dataset.yml
    target:
      additionalType: Run
    path: "runs/{dataset.identifier}/isa.run.yml"
    files:
      - id: dataset-placeholder
        path: dataset/.gitkeep
        create: empty
      - id: protocols-placeholder
        path: protocols/.gitkeep
        create: empty
```

Every primary file is a Dataset YAML document. Datamap content is stored in its
Dataset's `dataContexts`; this profile therefore has no codec-managed `datamap`
auxiliary files. Its project-managed placeholders match the ISA-XLSX scaffold.

## 13. Examples

### 13.1 URL-hosted scaffold project

```yaml
type: ArcWorkspaceProject
workspaceProfiles:
  - url: "https://example.org/arc/isa-xlsx-scaffold.yml"
```

The URL is illustrative; this specification does not assign the ISA-XLSX
decoration profile a canonical URL.

### 13.2 Project-local replacement of a profile rule

```yaml
type: ArcWorkspaceProject

workspaceProfiles:
  - url: "https://example.org/arc/isa-xlsx-scaffold.yml"

rules:
  - id: yaml-root
    codec: dataset.yml
    target: root
    path: hello.yml
```

The local `yaml-root` rule wholly replaces the profile's `investigation` rule
because both target `root`; their IDs do not need to match. The effective root
is written to `hello.yml`. The profile's unrelated Study, Assay, Workflow, and
Run rules remain effective. No fields are inherited from the replaced rule.

### 13.3 Referenced profile plus local exact target

```yaml
type: ArcWorkspaceProject
workspaceProfiles:
  - file: profiles/base.yml

rules:
  - id: fixed-study
    codec: isa.study.xlsx
    target:
      identifier: calibration-study
    path: special/calibration/isa.study.xlsx
```

If `profiles/base.yml` contributes a general Study rule, `calibration-study` is
excluded from that rule and handled at the fixed path.

### 13.4 Exact target with identifier capture

```yaml
- id: fixed-assay
  codec: isa.assay.xlsx
  target:
    identifier: assay-42
  path: "special/{dataset.identifier}/isa.assay.xlsx"
```

The path must resolve exactly once, and its capture and parsed identifier must
both equal `assay-42`.

### 13.5 Basic single-file rule declared directly by a project

```yaml
type: ArcWorkspaceProject
rules:
  - id: arc
    codec: dataset.yml
    target: root
    path: arc.yml
```

This is the inline-project form of the basic profile in section 12.1. With no
prepared child bindings, `ExternalChildIdentifiers` is empty and `dataset.yml`
serializes the nested Dataset graph recursively.

## 14. Invalid examples

### 14.1 Type target without capture

```yaml
- id: study
  codec: isa.study.xlsx
  target:
    additionalType: Study
  path: studies/isa.study.xlsx
```

A type target may select multiple Datasets and therefore requires the identifier
capture.

### 14.2 Duplicate exact targets

Two expanded rules with:

```yaml
target:
  identifier: study-1
```

are invalid even when their codec or path differs.

### 14.3 Unsupported or repeated capture

```yaml
path: "studies/{dataset.identifier}/{dataset.identifier}.xlsx"
```

Only `{dataset.identifier}` is supported, and it may occur at most once in the
path. Literal text may surround that capture within its segment.
