module ARCManagement.Tests.Yaml

open System
open ARCBaseModel
open ARCManagement
open Fable.Pyxpecto

let private root () = Dataset(["process-provenance"], ["root"])
let private same actual expected = Expect.isTrue (obj.ReferenceEquals(actual, expected)) "Shared entity reference"
let private different actual expected = Expect.isFalse (obj.ReferenceEquals(actual, expected)) "Distinct entity reference"
let private setProperty (target: EntityObject) key value =
    target.Common <- { target.Common with Properties = target.Common.Properties.With(key, value) }
let private decode body = DatasetYaml.decode("type: Dataset\nconformsTo: [process-provenance]\nidentifiers: [root]\n" + body)
let private anchored body = decode ("self: &child\n  type: Custom\n" + body)

let tests = testList "Detached and managed YAML" [
    testCase "idless cyclic graphs keep absence references order and duplicates" (fun _ ->
        let source = root()
        let child = Dataset(["administrative"], ["child"])
        source.State <- { source.State with HasParts = [child; child] }
        child.State <- { child.State with HasParts = [source] }
        let collection = source.HasParts
        let yaml = DatasetYaml.encode(source)
        Expect.equal (DatasetYaml.encode(source)) yaml "Local reference names are deterministic"
        Expect.isTrue (yaml.Contains("&o1")) "Local root anchor"
        Expect.isTrue (yaml.Contains("*o1")) "Cycle uses local reference"
        Expect.isFalse (yaml.Contains("id:")) "No domain IDs were invented on the wire"
        same source.HasParts collection
        same (Seq.item 0 source.HasParts) child
        Expect.isNone source.Id "Encoding leaves root ID absent"
        Expect.isNone child.Id "Encoding leaves child ID absent"
        let result = DatasetYaml.decode(yaml)
        different result source
        same (Seq.item 0 result.HasParts) (Seq.item 1 result.HasParts)
        same (Seq.item 0 (Seq.item 0 result.HasParts).HasParts) result
        Expect.isNone result.Id "Decoded root ID remains absent"
        Expect.isNone (Seq.item 0 result.HasParts).Id "Decoded child ID remains absent")

    testCase "equal supplied IDs do not merge detached object instances" (fun _ ->
        let first = Sample("first", id = "same")
        let second = Sample("second", id = "same")
        let a = ARCBaseModel.Process("first", input = EntityReference.Sample first)
        let b = ARCBaseModel.Process("second", input = EntityReference.Sample second)
        let source = Dataset.withProcesses(root(), [a; b; a])
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        same (Seq.item 0 result.Processes) (Seq.item 2 result.Processes)
        let sample (proc: ARCBaseModel.Process) = match proc.Input with Some(EntityReference.Sample v) -> v | _ -> failwith "Expected Sample"
        let restoredFirst, restoredSecond = sample (Seq.item 0 result.Processes), sample (Seq.item 1 result.Processes)
        different restoredFirst restoredSecond
        Expect.equal restoredFirst.Name "first" "First object retained"
        Expect.equal restoredSecond.Name "second" "Second object retained"
        Expect.equal restoredFirst.Id (Some "same") "First supplied ID"
        Expect.equal restoredSecond.Id (Some "same") "Second supplied ID"
        Expect.equal first.Id (Some "same") "Encoding leaves supplied IDs untouched")

    testCase "domain IDs remain independent of local reference names" (fun _ ->
        let source = Dataset(["process-provenance"], ["root"], id = "o2")
        let child = Dataset(["administrative"], ["child"], id = "")
        source.State <- { source.State with HasParts = [child; source] }
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        Expect.equal result.Id (Some "o2") "Domain ID does not resolve local alias"
        Expect.equal (Seq.item 0 result.HasParts).Id (Some "") "Empty supplied ID preserved"
        same (Seq.item 1 result.HasParts) result)

    testCase "legacy id and ref input remains readable with omitted IDs preserved" (fun _ ->
        let result = decode "id: legacy\nhasParts:\n  - type: Dataset\n    conformsTo: [administrative]\n    identifiers: [child]\n  - $ref: legacy\n"
        Expect.equal result.Id (Some "legacy") "Supplied legacy ID"
        Expect.isNone (Seq.item 0 result.HasParts).Id "Legacy omitted ID is not assigned in detached decoding"
        same (Seq.item 1 result.HasParts) result
        let plain = decode ""
        Expect.isNone plain.Id "Unanchored omitted root ID remains absent")

    testCase "extensions retain scalar alternatives and shared recursive object references" (fun _ ->
        let source = root()
        let custom = EntityObject("Custom")
        setProperty custom "owner" (Entity.Object source)
        setProperty custom "$ref" (Entity.Text "literal reference-like extension")
        setProperty custom "$id" (Entity.Text "literal ID-like extension")
        let values = EntityCollection([
            Entity.Object custom; Entity.Object custom; Entity.Number 0.0; Entity.Number 0.25
            Entity.Text "0"; Entity.Text "false"; Entity.Text ""; Entity.Bool false
            Entity.Null(EntityNull()); Entity.Blob(EntityBlob("AAE=")); Entity.Collection(EntityCollection([]))
        ])
        setProperty source "values" (Entity.Collection values)
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        let values = match result.EntityProperties.Get("values") with Entity.Collection v -> v | _ -> failwith "Expected collection"
        let custom = match values.Get(0) with Entity.Object v -> v | _ -> failwith "Expected object"
        match values.Get(1) with Entity.Object v -> same v custom | _ -> failwith "Expected shared object"
        match custom.EntityProperties.Get("owner") with Entity.Object v -> same v result | _ -> failwith "Expected owner"
        match custom.EntityProperties.Get("$ref") with Entity.Text v -> Expect.equal v "literal reference-like extension" "Literal extension" | _ -> failwith "Expected text"
        match custom.EntityProperties.Get("$id") with Entity.Text v -> Expect.equal v "literal ID-like extension" "No reserved serialization field" | _ -> failwith "Expected text"
        match values.Get(2) with Entity.Number v -> Expect.equal v 0.0 "Zero" | _ -> failwith "Expected number"
        match values.Get(3) with Entity.Number v -> Expect.equal v 0.25 "Fraction" | _ -> failwith "Expected number"
        for index, expected in [4, "0"; 5, "false"; 6, ""] do
            match values.Get(index) with Entity.Text v -> Expect.equal v expected "Text alternative" | _ -> failwith "Expected text"
        match values.Get(7) with Entity.Bool v -> Expect.isFalse v "False remains Boolean" | _ -> failwith "Expected Boolean"
        match values.Get(8) with Entity.Null _ -> () | _ -> failwith "Expected null"
        match values.Get(9) with Entity.Blob v -> Expect.equal v.Base64 "AAE=" "Binary representation" | _ -> failwith "Expected blob"
        match values.Get(10) with Entity.Collection v -> Expect.equal v.Count 0 "Empty collection" | _ -> failwith "Expected collection"
        Expect.isNone custom.Id "Generic typed objects keep absent ID")

    testCase "Annotation values preserve zero fraction text and absence" (fun _ ->
        let source = Dataset.withAdditionalProperties(root(), [Annotation("zero", value = AnnotationValue.Number 0.0); Annotation("fraction", value = AnnotationValue.Number 0.125); Annotation("text", value = AnnotationValue.Text "0.125"); Annotation("absent")])
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        Expect.equal (Seq.item 0 result.AdditionalProperties).Value (Some(AnnotationValue.Number 0.0)) "Numeric zero"
        Expect.equal (Seq.item 1 result.AdditionalProperties).Value (Some(AnnotationValue.Number 0.125)) "Fraction"
        Expect.equal (Seq.item 2 result.AdditionalProperties).Value (Some(AnnotationValue.Text "0.125")) "Quoted numeric text"
        Expect.isNone (Seq.item 3 result.AdditionalProperties).Value "Absent value")

    testCase "domain alternative objects and URLs retain their types and cycles" (fun _ ->
        let terms = DefinedTermSet("terms")
        let term = DefinedTerm("term", inDefinedTermSet = DefinedTermSetReference.TermSet terms)
        let url = DefinedTerm("url", inDefinedTermSet = DefinedTermSetReference.Url "https://example.org/terms")
        let parameter = FormalParameter(name = "parameter")
        let annotation = Annotation("value", instanceOf = parameter)
        parameter.State <- { parameter.State with DefaultValue = Some annotation }
        let recipe = Recipe(parameters = [parameter; parameter], intendedUse = RecipeIntendedUse.Term term)
        let proc = ARCBaseModel.Process("process", executesRecipe = recipe, parameterValues = [annotation])
        let source = Dataset.withProcesses(root(), [proc])
        setProperty source "urlTerm" (Entity.Object url)
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        let recipe = (Seq.item 0 result.Processes).ExecutesRecipe.Value
        same (Seq.item 0 recipe.Parameters) (Seq.item 1 recipe.Parameters)
        let annotation = (Seq.item 0 recipe.Parameters).DefaultValue.Value
        same annotation.InstanceOf.Value (Seq.item 0 recipe.Parameters)
        same (Seq.item 0 (Seq.item 0 result.Processes).ParameterValues) annotation
        match recipe.IntendedUse with
        | Some(RecipeIntendedUse.Term v) ->
            match v.InDefinedTermSet with
            | Some(DefinedTermSetReference.TermSet v) -> Expect.equal v.Name "terms" "Term-set object"
            | _ -> failwith "Expected term-set object"
        | _ -> failwith "Expected term intended use"
        match result.EntityProperties.Get("urlTerm") with
        | Entity.Object (:? DefinedTerm as v) ->
            Expect.equal v.InDefinedTermSet (Some(DefinedTermSetReference.Url "https://example.org/terms")) "URL alternative"
        | _ -> failwith "Expected term")

    testCase "unanchored full entities in an anchored document remain separate" (fun _ ->
        let result = decode "hasParts:\n  - &first\n    type: Dataset\n    id: same\n    conformsTo: [administrative]\n    identifiers: [child]\n  - type: Dataset\n    id: same\n    conformsTo: [administrative]\n    identifiers: [child]\n  - *first\n"
        different (Seq.item 0 result.HasParts) (Seq.item 1 result.HasParts)
        same (Seq.item 0 result.HasParts) (Seq.item 2 result.HasParts))

    testCase "malformed required fields and unresolved legacy references fail" (fun _ ->
        for yaml in ["type: Sample\nname: sample\n"; "type: Dataset\nconformsTo: [process-provenance]\nidentifiers: [root]\nhasParts:\n  - $ref: missing\n"; "type: Dataset\ntype: Dataset\n"] do
            Expect.throws (fun () -> DatasetYaml.decode(yaml) |> ignore) "Invalid detached YAML rejected")

    testCase "unknown forward and duplicate local references fail" (fun _ ->
        for body in ["other: *missing\n"; "other: *later\nlater: &later\n  type: Custom\n"; "other: &child\n  type: Custom\n"] do
            Expect.throws (fun () -> anchored body |> ignore) "Invalid entity alias rejected")

    testCase "scalar and collection anchors cannot serve as entity references" (fun _ ->
        for body in ["title: &scalar hello\ncopy: *scalar\n"; "values: &items\n  - hello\ncopy: *items\n"; "title: *child\n"; "additionalTypes:\n  - *child\n"] do
            Expect.throws (fun () -> anchored body |> ignore) "Nonentity aliases rejected")

    testCase "local aliases cannot mix with legacy ref objects or scalar ID references" (fun _ ->
        for body in ["other:\n  $ref: child\n"; "hasParts:\n  - o1\n"; "hasParts:\n  - $ref: o1\n"] do
            Expect.throws (fun () -> anchored body |> ignore) "Mixed reference syntax rejected")

    testCase "encoding invalid detached graphs leaves caller IDs and references intact" (fun _ ->
        let annotation = Annotation("invalid", value = AnnotationValue.Number Double.NaN)
        let source = Dataset.withAdditionalProperties(root(), [annotation])
        Expect.throws (fun () -> DatasetYaml.encode(source) |> ignore) "Nonfinite number rejected"
        Expect.isNone source.Id "Root ID unchanged on failure"
        Expect.isNone annotation.Id "Annotation ID unchanged on failure"
        same (Seq.item 0 source.AdditionalProperties) annotation)

    testCase "session YAML import adopts an independent graph and normal ID policy" (fun _ ->
        let source = root()
        source.State <- { source.State with HasParts = [source] }
        let yaml = DatasetYaml.encode(source)
        use session = Session.createInMemory()
        let first = session.importYaml(yaml)
        let second = session.importYaml(yaml)
        Expect.isSome first.Model.Id "Managed import assigns omitted IDs"
        Expect.isNone source.Id "Source remains detached and idless"
        different first.Model second.Model
        same (Seq.item 0 first.Model.HasParts) first.Model
        Expect.notEqual first.ArcId second.ArcId "Every import creates an independent entry"
        Expect.isFalse first.History.CanUndo "Import does not create an edit command"
        Expect.equal (session.listArcs()).Count 2 "No implicit extra entries"
        Expect.isNone session.DatabasePath "Import requires no filesystem repository"
        let duplicate = Dataset.withHasParts(root(), [Dataset(["administrative"], ["one"], id = "collision"); Dataset(["administrative"], ["two"], id = "collision")])
        let duplicateYaml = DatasetYaml.encode(duplicate)
        Expect.throws (fun () -> session.importYaml(duplicateYaml) |> ignore) "Managed import rejects distinct objects with equal IDs"
        Expect.equal (session.listArcs()).Count 2 "Failed import leaves repository unchanged"
        Expect.throws (fun () -> session.importYaml("invalid") |> ignore) "Invalid YAML import rejected"
        Expect.equal (session.listArcs()).Count 2 "Invalid YAML leaves no entry")

    testCase "managed YAML exports the checked root without changing history or registry" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(root())
        let sample = graph.Sample.create("standalone")
        let proc = graph.Process.create("attached")
        graph.Dataset.addProcess(graph.Model, proc) |> ignore
        graph.Process.setName(proc, "changed") |> ignore
        graph.History.undo()
        Expect.isTrue graph.History.CanRedo "Redo exists before export"
        let yaml = graph.toYaml()
        Expect.isFalse (yaml.Contains("standalone")) "Standalone registered objects excluded"
        Expect.isTrue graph.History.CanRedo "Export leaves redo branch intact"
        same (graph.Sample.get(sample.Id.Value)) sample
        let detached = DatasetYaml.decode(yaml)
        Expect.equal (Seq.item 0 detached.Processes).Name "attached" "Exports retained root state"
        Expect.equal detached.Id graph.Model.Id "Assigned domain ID retained"
        graph.History.redo()
        Expect.equal proc.Name "changed" "History remains usable"
        graph.Model.State <- { graph.Model.State with Title = Some "unsupported direct mutation" }
        Expect.throws (fun () -> graph.toYaml() |> ignore) "Managed export detects direct mutation")
]
