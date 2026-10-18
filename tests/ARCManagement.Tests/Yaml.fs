module ARCManagement.Tests.Yaml

open System
open ARCBaseModel
open ARCManagement
open Fable.Pyxpecto

let private root () = Dataset(["process-provenance"],["root"])
let private same a b = Expect.isTrue (obj.ReferenceEquals(a,b)) "Shared reference"
let private different a b = Expect.isFalse (obj.ReferenceEquals(a,b)) "Distinct reference"
let private decode body = DatasetYaml.decode("type: Dataset\nconformsTo: [process-provenance]\nidentifiers: [root]\n" + body)
let private anchored body = decode("self: &child\n  type: Custom\n" + body)

let tests = testList "Immutable YAML" [
    testCase "idless cyclic builder graphs roundtrip without assigned IDs" (fun _ ->
        let builder = ModelBuilder()
        let a = builder.draft(root())
        let b = builder.draft(Dataset(["administrative"],["child"]))
        builder.Dataset.setHasParts(a,[b;b])
        builder.Dataset.setHasParts(b,[a])
        let source = builder.finish().get(a)
        let yaml = DatasetYaml.encode(source)
        Expect.equal (DatasetYaml.encode(source)) yaml "Deterministic local anchors"
        Expect.isFalse (yaml.Contains("id:")) "No invented IDs"
        let result = DatasetYaml.decode(yaml)
        same result.HasParts[0] result.HasParts[1]
        same result.HasParts[0].HasParts[0] result
        Expect.isNone result.Id "Root absent ID"
        Expect.isNone result.HasParts[0].Id "Child absent ID"
        Expect.isNone source.Id "Encoder leaves source unchanged")

    testCase "detached equal IDs retain distinct instances and repeated aliases" (fun _ ->
        let first = Dataset(["administrative"],["one"],id="same")
        let second = Dataset(["semantic-designation"],["two"],id="same")
        let result = DatasetYaml.decode(DatasetYaml.encode(Dataset.withHasParts(root(),[first;second;first])))
        different result.HasParts[0] result.HasParts[1]
        same result.HasParts[0] result.HasParts[2]
        Expect.equal result.HasParts[0].Id (Some "same") "First supplied ID"
        Expect.equal result.HasParts[1].Id (Some "same") "Second supplied ID")

    testCase "legacy references and local aliases retain omitted IDs" (fun _ ->
        let result = decode "id: legacy\nhasParts:\n  - type: Dataset\n    conformsTo: [administrative]\n    identifiers: [child]\n  - $ref: legacy\n"
        same result.HasParts[1] result
        Expect.isNone result.HasParts[0].Id "Omitted ID retained"
        let local = decode "hasParts:\n  - &first\n    type: Dataset\n    id: same\n    conformsTo: [administrative]\n    identifiers: [child]\n  - type: Dataset\n    id: same\n    conformsTo: [administrative]\n    identifiers: [child]\n  - *first\n"
        different local.HasParts[0] local.HasParts[1]
        same local.HasParts[0] local.HasParts[2])

    testCase "numeric alternatives absence and empty text roundtrip" (fun _ ->
        let source = Dataset.withAdditionalProperties(root(),[Annotation("zero",value=AnnotationValue.Number 0.0);Annotation("fraction",value=AnnotationValue.Number 0.125);Annotation("text",value=AnnotationValue.Text "");Annotation("absent")])
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        Expect.equal result.AdditionalProperties[0].Value (Some(AnnotationValue.Number 0.0)) "Zero"
        Expect.equal result.AdditionalProperties[1].Value (Some(AnnotationValue.Number 0.125)) "Fraction"
        Expect.equal result.AdditionalProperties[2].Value (Some(AnnotationValue.Text "")) "Empty text"
        Expect.isNone result.AdditionalProperties[3].Value "Absence")

    testCase "malformed required fields and unresolved references fail" (fun _ ->
        for yaml in ["type: Sample\nname: sample\n";"type: Dataset\nconformsTo: [process-provenance]\nidentifiers: [root]\nhasParts:\n  - $ref: missing\n";"type: Dataset\ntype: Dataset\n"] do
            Expect.throws (fun () -> DatasetYaml.decode(yaml) |> ignore) "Invalid detached YAML")

    testCase "unknown forward and duplicate local references fail" (fun _ ->
        for body in ["other: *missing\n";"other: *later\nlater: &later\n  type: Custom\n";"other: &child\n  type: Custom\n"] do
            Expect.throws (fun () -> anchored body |> ignore) "Invalid entity alias")

    testCase "scalar and collection anchors are not entity references" (fun _ ->
        for body in ["title: &scalar hello\ncopy: *scalar\n";"values: &items\n  - hello\ncopy: *items\n";"title: *child\n";"additionalTypes:\n  - *child\n"] do
            Expect.throws (fun () -> anchored body |> ignore) "Nonentity alias")

    testCase "local aliases cannot mix legacy reference syntax" (fun _ ->
        for body in ["other:\n  $ref: child\n";"hasParts:\n  - o1\n";"hasParts:\n  - $ref: o1\n"] do
            Expect.throws (fun () -> anchored body |> ignore) "Mixed reference syntax")

    testCase "management import creates independent graphs and rejects ID collisions atomically" (fun _ ->
        let builder = ModelBuilder()
        let token = builder.draft(root())
        builder.Dataset.setHasParts(token,[token])
        let source = builder.finish().get(token)
        let yaml = DatasetYaml.encode(source)
        use session = Session.createInMemory()
        let first,second = session.importYaml(yaml),session.importYaml(yaml)
        different first.Model second.Model
        same first.Model.HasParts[0] first.Model
        Expect.isNone source.Id "Detached source unchanged"
        Expect.isSome first.Model.Id "Persisted copy assigned ID"
        Expect.isFalse first.History.CanUndo "Import not an edit"
        let collision = Dataset.withHasParts(root(),[Dataset(["administrative"],["one"],id="same");Dataset(["administrative"],["two"],id="same")])
        Expect.throws (fun () -> session.importYaml(DatasetYaml.encode(collision)) |> ignore) "Collision"
        Expect.equal (session.listArcs()).Count 2 "No partially inserted ARC")

    testCase "graph cloning edits self cycles without conflicting ID definitions" (fun _ ->
        let builder = ModelBuilder()
        let token = builder.draft(Dataset(["process-provenance"],["root"],id="root"))
        builder.Dataset.setHasParts(token,[token])
        use session = Session.createInMemory()
        let graph = session.createArc(builder.finish().get(token))
        let observed = graph.readModel()
        let edit = ModelBuilder()
        let cloned = edit.cloneGraph(observed.Value)
        edit.Dataset.setTitle(cloned,"edited")
        let saved = graph.Dataset.upsert(observed.Revision,edit.finish().get(cloned),recursive=true)
        same saved.Value.HasParts[0] saved.Value
        Expect.equal saved.Value.Title (Some "edited") "Updated cycle"
        Expect.isNone observed.Value.Title "Old cyclic graph unchanged")
]
