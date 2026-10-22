module ARCManagement.Tests.Yaml

open System
open ARCBaseModel
open ARCManagement
open ARCManagement.Helper
open ARCtrl
open Fable.Pyxpecto

let private same actual expected = Expect.isTrue (obj.ReferenceEquals(actual, expected)) "Shared entity reference"
let private different actual expected = Expect.isFalse (obj.ReferenceEquals(actual, expected)) "Distinct entity reference"
let private setProperty (target: EntityObject) key value =
    target.Common <- { target.Common with Properties = target.Common.Properties.With(key, value) }
let private header = "type: Dataset\nconformsTo: [process-provenance]\nidentifier: root\n"
let private decode body = DatasetYaml.decode(header + body)
let private occurrences (text: string) (part: string) = (text.Length - text.Replace(part, "").Length) / part.Length
let private failsWith (part: string) (action: unit -> unit) =
    let message = try action (); "" with error -> error.Message
    Expect.isTrue (message.Contains part) ("Expected a failure mentioning '" + part + "', got '" + message + "'")
let private sample (proc: ARCBaseModel.Process) = match proc.Input with Some(EntityReference.Sample v) -> v | _ -> failwith "Expected a Sample input"
let private data (proc: ARCBaseModel.Process) = match proc.Output with Some(EntityReference.Data v) -> v | _ -> failwith "Expected a Data output"
let private process' name id input output = ARCBaseModel.Process(name, ?input = input, ?output = output, id = id)
let private withInput (value: Sample) = Some(EntityReference.Sample value)
let private withOutput (value: Data) = Some(EntityReference.Data value)
let private idRoot () = Dataset(["process-provenance"], ["root"], id = "root-id")
let private readExample path = Path.readFileText path

/// A graph that uses every entity kind and property. Every entity carries an ID except where noted.
let private richGraph () =
    let temperature = Annotation("temperature", value = AnnotationValue.Number 4.0, unit = "degree Celsius", nameTAN = "N1", valueTAN = "N2", unitTAN = "N3", id = "pv:t")
    let note = Annotation("note", value = AnnotationValue.Text "42", id = "pv:n", additionalTypes = ["CharacteristicValue"; "CharacteristicValue"])
    let parameter = FormalParameter(name = "temperature", nameTAN = "N4", defaultValue = temperature, id = "fp1")
    let terms = DefinedTermSet("terms", identifier = "t1", id = "set1")
    let steward = DefinedTerm("steward", identifier = "dt", tan = "T1", inDefinedTermSet = DefinedTermSetReference.TermSet terms, id = "dt1")
    let linked = DefinedTerm("linked", inDefinedTermSet = DefinedTermSetReference.Url "https://example.org/terms", id = "dt2")
    let recipe = Recipe(name = "extraction", parameters = [parameter; parameter], description = "Protein extraction", intendedUse = RecipeIntendedUse.Term steward, additionalProperties = [note], components = [temperature], version = "1", url = "https://example.org/recipe", id = "r1")
    let textRecipe = Recipe(name = "text use", intendedUse = RecipeIntendedUse.Text "storage", id = "r2")
    let leaf = Sample("leaf", additionalProperties = [temperature], id = "s1", additionalTypes = ["Source"])
    let part = Data("raw/part.tsv", selector = "#col=1", selectorFormat = "RFC7111", id = "d2")
    let raw = Data("raw/extract.tsv", encodingFormat = "text/tab-separated-values", hasParts = [part; part], additionalProperties = [note], id = "d1")
    let organization = Organization("Lab", url = "https://example.org", id = "o1", additionalTypes = ["Institute"])
    let agent = Agent("Ada Lovelace", givenName = "Ada", familyName = "Lovelace", emails = ["a@example.org"; "b@example.org"], affiliations = [organization], identifiers = ["orcid"], additionalProperties = [note], jobTitles = [steward; linked], id = "a1")
    let article = ScholarlyArticle("Paper", identifiers = ["doi:1"; "doi:2"], authors = [agent], creativeWorkStatus = steward, additionalProperties = [temperature], id = "art1")
    let descriptor = Descriptor(EntityReference.Data raw, annotations = [temperature; note], id = "desc1")
    let first = ARCBaseModel.Process("measure", input = EntityReference.Sample leaf, output = EntityReference.Data raw, executesRecipe = recipe, parameterValues = [temperature; temperature], id = "p1", additionalTypes = ["LabProcess"])
    let second = ARCBaseModel.Process("measure again", input = EntityReference.Sample leaf, executesRecipe = textRecipe, id = "p2")
    let child = Dataset(["semantic-designation"], ["child"], descriptors = [descriptor], id = "child-id")
    let root = Dataset(["administrative"; "process-provenance"], ["root"; "alternate"], title = "Title", description = "Description", license = "CC-BY-4.0", datePublished = "2026-01-02", dateCreated = "2026-01-01", dateModified = "2026-01-03", hasParts = [child; child], dataFiles = [raw], agents = [agent], citations = [article], processes = [first; second], additionalProperties = [temperature], id = "root-id", additionalTypes = ["Investigation"])
    let custom = EntityObject("Custom", id = "custom-1")
    setProperty custom "owner" (Entity.Object root)
    setProperty root "score" (Entity.Number 0.5)
    setProperty root "custom" (Entity.Object custom)
    root

let private firstProcess (root: Dataset) = Seq.item 0 root.Processes

module private Legacy =
    open ARCManagement.Internal
    let decodeWithSuppliedId () =
        let row = { Id = "r"; Kind = "Sample"; Properties = Map.ofList ["name", Cell.Text "s"]; Extensions = Map.empty }
        let text = Codec.encodeState { Root = "r"; Entities = [row] }
        let legacy = text.Replace("        type: Sample", "        suppliedId: legacy\n        type: Sample")
        match (Codec.decodeState legacy).Entities with
        | [decoded] -> (match Map.tryFind "name" decoded.Properties with Some(Cell.Text v) -> v | _ -> "missing")
        | _ -> "wrong count"

let tests = testList "Detached and managed YAML" [
    testCase "round trip keeps every entity kind property and ID in inline and indexed mode" (fun _ ->
        let source = richGraph ()
        let inlineYaml = DatasetYaml.encode(source)
        let indexedYaml = DatasetYaml.encodeIndexed(source)
        Expect.equal (DatasetYaml.encode(DatasetYaml.decode(inlineYaml))) inlineYaml "Inline text is stable"
        Expect.equal (DatasetYaml.encodeIndexed(DatasetYaml.decode(indexedYaml))) indexedYaml "Indexed text is stable"
        Expect.equal (DatasetYaml.encode(DatasetYaml.decode(indexedYaml))) inlineYaml "Both modes describe the same graph"
        Expect.equal (DatasetYaml.encodeIndexed(DatasetYaml.decode(inlineYaml))) indexedYaml "Both modes describe the same graph"
        Expect.isFalse (inlineYaml.Contains("\nrecipes:")) "Inline mode has no index sections"
        for section in ["\nannotations:"; "\nrecipes:"; "\nagentIndex:"] do
            Expect.isTrue (indexedYaml.Contains section) ("Indexed mode writes " + section)
        Expect.isTrue (indexedYaml.Contains "\"@id\": a1") "Agents are referenced by @id"
        let result = DatasetYaml.decode(indexedYaml)
        Expect.equal result.Id (Some "root-id") "Root ID"
        Expect.equal (Seq.toList result.ConformsTo) ["administrative"; "process-provenance"] "conformsTo order"
        Expect.equal (Seq.toList result.Identifiers) ["root"; "alternate"] "Identifiers keep their order"
        Expect.equal (Seq.toList result.AdditionalTypes) ["Investigation"] "additionalTypes"
        Expect.equal (result.Title, result.License, result.DateCreated) (Some "Title", Some "CC-BY-4.0", Some "2026-01-01") "Dataset text fields"
        let process1 = firstProcess result
        let recipe = process1.ExecutesRecipe.Value
        Expect.equal recipe.Id (Some "r1") "Recipe ID"
        same (Seq.item 0 recipe.Parameters) (Seq.item 1 recipe.Parameters)
        same (Seq.item 0 process1.ParameterValues) (Seq.item 1 process1.ParameterValues)
        same (Seq.item 0 process1.ParameterValues) (Seq.item 0 result.AdditionalProperties)
        same (sample process1) (sample (Seq.item 1 result.Processes))
        same (data process1) (Seq.item 0 result.DataFiles)
        Expect.equal (Seq.toList (Seq.item 0 result.DataFiles).AdditionalTypes) [] "No invented additionalTypes"
        Expect.equal (Seq.toList (sample process1).AdditionalTypes) ["Source"] "Sample additionalTypes"
        let part = Seq.item 0 (Seq.item 0 result.DataFiles).HasParts
        same part (Seq.item 1 (Seq.item 0 result.DataFiles).HasParts)
        Expect.equal (part.Selector, part.SelectorFormat) (Some "#col=1", Some "RFC7111") "Data fields"
        let agent = Seq.item 0 result.Agents
        Expect.equal (Seq.toList agent.Emails) ["a@example.org"; "b@example.org"] "Agent emails"
        Expect.equal (Seq.toList (Seq.item 0 agent.Affiliations).AdditionalTypes) ["Institute"] "Organization additionalTypes"
        same (Seq.item 0 (Seq.item 0 result.Citations).Authors) agent
        same (Seq.item 0 agent.JobTitles) (Seq.item 0 result.Citations).CreativeWorkStatus.Value
        match (Seq.item 1 agent.JobTitles).InDefinedTermSet with
        | Some(DefinedTermSetReference.Url v) -> Expect.equal v "https://example.org/terms" "URL alternative"
        | _ -> failwith "Expected the URL alternative"
        match (Seq.item 0 agent.JobTitles).InDefinedTermSet with
        | Some(DefinedTermSetReference.TermSet v) -> Expect.equal v.Identifier (Some "t1") "Term-set object"
        | _ -> failwith "Expected the term-set alternative"
        match (Seq.item 1 result.Processes).ExecutesRecipe.Value.IntendedUse with
        | Some(RecipeIntendedUse.Text v) -> Expect.equal v "storage" "Text intended use"
        | _ -> failwith "Expected the text alternative"
        match recipe.IntendedUse with
        | Some(RecipeIntendedUse.Term v) -> same v (Seq.item 0 agent.JobTitles)
        | _ -> failwith "Expected the term alternative"
        let child = Seq.item 0 result.HasParts
        same child (Seq.item 1 result.HasParts)
        Expect.equal (Seq.toList (Seq.item 0 child.Descriptors).Annotations |> List.map (fun a -> a.Id)) [Some "pv:t"; Some "pv:n"] "Descriptor annotations"
        match (Seq.item 0 child.Descriptors).Describes with
        | EntityReference.Data v -> same v (Seq.item 0 result.DataFiles)
        | _ -> failwith "Expected a Data descriptor target"
        Expect.equal (Seq.toList (Seq.item 0 recipe.Parameters).DefaultValue.Value.AdditionalTypes) [] "FormalParameter default"
        Expect.equal (Seq.toList (Seq.item 0 result.AdditionalProperties).AdditionalTypes) [] "Annotation additionalTypes"
        Expect.equal (Seq.toList (Seq.item 0 agent.AdditionalProperties).AdditionalTypes) ["CharacteristicValue"; "CharacteristicValue"] "Duplicate additionalTypes"
        match result.EntityProperties.Get("custom") with
        | Entity.Object custom ->
            Expect.equal custom.Type "Custom" "Generic type"
            match custom.EntityProperties.Get("owner") with
            | Entity.Object owner -> same owner result
            | _ -> failwith "Expected the owner"
        | _ -> failwith "Expected an object")

    testCase "entities without IDs repeat inline and decode as distinct equal objects" (fun _ ->
        let note = Annotation("note", value = AnnotationValue.Text "x")
        let source = Dataset.withAdditionalProperties(Dataset(["administrative"], ["root"]), [note; note; Annotation("other")])
        let yaml = DatasetYaml.encode(source)
        Expect.isFalse (yaml.Contains("id:")) "No domain IDs were invented on the wire"
        Expect.equal (occurrences yaml "name: note") 2 "Each occurrence is written"
        let result = DatasetYaml.decode(yaml)
        let first, second = Seq.item 0 result.AdditionalProperties, Seq.item 1 result.AdditionalProperties
        different first second
        Expect.equal (first.Name, first.Value) (second.Name, second.Value) "Equal content"
        Expect.isNone first.Id "Absent ID stays absent"
        Expect.isNone result.Id "Absent root ID stays absent"
        Expect.isNone note.Id "Encoding leaves the source untouched"
        Expect.equal (Seq.length result.AdditionalProperties) 3 "Order and count")

    testCase "order and duplicates of ID-bearing entities are kept" (fun _ ->
        let one, two = Dataset(["administrative"], ["one"], id = "one"), Dataset(["administrative"], ["two"], id = "two")
        let source = Dataset.withHasParts(idRoot (), [two; one; two])
        let result = DatasetYaml.decode(DatasetYaml.encode(source))
        Expect.equal (result.HasParts |> Seq.map (fun v -> v.Id.Value) |> List.ofSeq) ["two"; "one"; "two"] "Order"
        same (Seq.item 0 result.HasParts) (Seq.item 2 result.HasParts)
        different (Seq.item 0 result.HasParts) (Seq.item 1 result.HasParts))

    testCase "cycles through IDs are written as @id references and restored as cycles" (fun _ ->
        let child = Dataset(["administrative"], ["child"], id = "child-id")
        let source = Dataset(["process-provenance"], ["root"], id = "root-id")
        source.State <- { source.State with HasParts = [child; source] }
        child.State <- { child.State with HasParts = [source] }
        let yaml = DatasetYaml.encode(source)
        Expect.isTrue (yaml.Contains("\"@id\": root-id")) "Reference to the root that is being written"
        for text in [yaml; DatasetYaml.encodeIndexed(source)] do
            let result = DatasetYaml.decode(text)
            same (Seq.item 1 result.HasParts) result
            same (Seq.item 0 (Seq.item 0 result.HasParts).HasParts) result
        let parameter = FormalParameter(name = "parameter", id = "fp")
        let annotation = Annotation("value", instanceOf = parameter, id = "an")
        parameter.State <- { parameter.State with DefaultValue = Some annotation }
        let recipe = Recipe(parameters = [parameter], id = "recipe")
        let dataset = Dataset.withProcesses(idRoot (), [ARCBaseModel.Process("p", executesRecipe = recipe, parameterValues = [annotation], id = "p")])
        for text in [DatasetYaml.encode(dataset); DatasetYaml.encodeIndexed(dataset)] do
            let result = DatasetYaml.decode(text)
            let parameter = Seq.item 0 (firstProcess result).ExecutesRecipe.Value.Parameters
            same parameter.DefaultValue.Value.InstanceOf.Value parameter
            same (Seq.item 0 (firstProcess result).ParameterValues) parameter.DefaultValue.Value)

    testCase "a cycle among entities without IDs fails on encode" (fun _ ->
        let source = Dataset(["process-provenance"], ["root"])
        source.State <- { source.State with HasParts = [source] }
        failsWith "cycle" (fun () -> DatasetYaml.encode(source) |> ignore)
        Expect.isNone source.Id "The failed encode leaves the source untouched")

    testCase "equal IDs mean one entity and differing definitions fail" (fun _ ->
        failsWith "Conflicting definitions for entity ID same" (fun () ->
            decode "hasPart:\n  - type: Dataset\n    id: same\n    identifier: a\n  - type: Dataset\n    id: same\n    identifier: b\n" |> ignore)
        let result = decode "hasPart:\n  - type: Dataset\n    id: same\n    identifier: a\n  - type: Dataset\n    id: same\n    identifier: a\n"
        same (Seq.item 0 result.HasParts) (Seq.item 1 result.HasParts)
        let equal = Dataset.withProcesses(idRoot (), [process' "a" "pa" (withInput (Sample("z", id = "dup"))) None; process' "b" "pb" (withInput (Sample("z", id = "dup"))) None])
        let restored = DatasetYaml.decode(DatasetYaml.encode(equal))
        same (sample (Seq.item 0 restored.Processes)) (sample (Seq.item 1 restored.Processes))
        let conflicting = Dataset.withProcesses(idRoot (), [process' "a" "pa" (withInput (Sample("one", id = "dup"))) None; process' "b" "pb" (withInput (Sample("two", id = "dup"))) None])
        failsWith "Conflicting definitions for entity ID dup" (fun () -> DatasetYaml.encode(conflicting) |> ignore))

    testCase "consecutive processes with equal state share one entry" (fun _ ->
        let a, b, c = Sample("a", id = "sa"), Sample("b", id = "sb"), Sample("c", id = "sc")
        let x, y, z = Data("x", id = "dx"), Data("y", id = "dy"), Data("z", id = "dz")
        let grouped = Dataset.withProcesses(idRoot (), [process' "run" "p1" (withInput a) (withOutput x); process' "run" "p2" (withInput b) (withOutput y); process' "run" "p3" (withInput c) (withOutput z)])
        let yaml = DatasetYaml.encode(grouped)
        Expect.equal (occurrences yaml "type: Process") 1 "One entry"
        Expect.equal (occurrences yaml "name: run") 1 "Shared name written once"
        let result = DatasetYaml.decode(yaml)
        Expect.equal (result.Processes |> Seq.map (fun p -> p.Id.Value) |> List.ofSeq) ["p1"; "p2"; "p3"] "Member IDs keep their order"
        Expect.equal (result.Processes |> Seq.map (fun p -> (sample p).Name, (data p).Path) |> List.ofSeq) ["a", "x"; "b", "y"; "c", "z"] "Inputs pair with outputs by position"
        let distinct = Dataset.withProcesses(idRoot (), [process' "run" "p1" (withInput a) None; process' "other" "p2" (withInput b) None; process' "run" "p3" (withInput c) None])
        Expect.equal (occurrences (DatasetYaml.encode(distinct)) "type: Process") 3 "Processes that are not adjacent stay apart"
        let free = Dataset.withProcesses(idRoot (), [process' "run" "p1" None None; process' "run" "p2" None None])
        Expect.equal (occurrences (DatasetYaml.encode(free)) "type: Process") 2 "Processes without endpoints stay apart"
        let shapes = Dataset.withProcesses(idRoot (), [process' "run" "p1" (withInput a) None; process' "run" "p2" (withInput b) (withOutput y)])
        Expect.equal (occurrences (DatasetYaml.encode(shapes)) "type: Process") 2 "Processes with different endpoint shapes stay apart"
        let withoutIds = Dataset.withProcesses(idRoot (), [ARCBaseModel.Process("run", input = EntityReference.Sample a, id = "p1"); ARCBaseModel.Process("run", input = EntityReference.Sample b)])
        Expect.equal (occurrences (DatasetYaml.encode(withoutIds)) "type: Process") 2 "Processes with and without IDs stay apart"
        let anonymous = Dataset.withProcesses(idRoot (), [ARCBaseModel.Process("run", input = EntityReference.Sample a); ARCBaseModel.Process("run", input = EntityReference.Sample b)])
        let anonymousYaml = DatasetYaml.encode(anonymous)
        Expect.equal (occurrences anonymousYaml "type: Process") 1 "Processes without IDs share an entry"
        Expect.equal (Seq.length (DatasetYaml.decode(anonymousYaml).Processes)) 2 "Two processes come back"
        let singles = DatasetYaml.decode(DatasetYaml.encodeIndexed(distinct))
        Expect.equal (singles.Processes |> Seq.map (fun p -> p.Id.Value) |> List.ofSeq) ["p1"; "p2"; "p3"] "Order of ungrouped processes")

    testCase "process entries expand inputs and outputs pairwise" (fun _ ->
        let result = decode "processes:\n  - type: Process\n    id: [p1, p2, p3]\n    name: run\n    inputs:\n      - {type: Sample, name: a}\n      - {type: Sample, name: b}\n      - {type: Sample, name: c}\n    outputs:\n      - {type: Data, path: x}\n"
        let processes = List.ofSeq result.Processes
        Expect.equal (processes |> List.map (fun p -> p.Id.Value)) ["p1"; "p2"; "p3"] "IDs by position"
        Expect.equal (processes |> List.map (fun p -> p.Output.IsSome)) [true; false; false] "A shorter array leaves the endpoint absent"
        Expect.equal (processes |> List.map (fun p -> (sample p).Name)) ["a"; "b"; "c"] "Inputs"
        let single = decode "processes:\n  - {type: Process, id: only, name: run}\n"
        Expect.equal (firstProcess single).Id (Some "only") "Scalar ID on a single process"
        Expect.isTrue ((firstProcess single).Input.IsNone && (firstProcess single).Output.IsNone) "No endpoints"
        failsWith "id" (fun () -> decode "processes:\n  - type: Process\n    id: p\n    name: run\n    inputs:\n      - {type: Sample, name: a}\n      - {type: Sample, name: b}\n" |> ignore)
        failsWith "IDs" (fun () -> decode "processes:\n  - type: Process\n    id: [p]\n    name: run\n    inputs:\n      - {type: Sample, name: a}\n      - {type: Sample, name: b}\n" |> ignore))

    testCase "one-or-many keys are written by count and read in either form" (fun _ ->
        let one = Dataset.withAgents(idRoot (), [Agent("Ada", emails = ["a@example.org"], identifiers = ["i1"], affiliations = [Organization("Lab")], additionalTypes = ["Person"])])
        let oneYaml = DatasetYaml.encode(one)
        for key in ["email: a@example.org"; "identifier: i1"; "affiliation:"; "additionalType: Person"] do
            Expect.isTrue (oneYaml.Contains key) ("Singular form " + key)
        for key in ["emails:"; "identifiers:"; "affiliations:"; "additionalTypes:"] do
            Expect.isFalse (oneYaml.Contains key) ("No plural form " + key)
        let many = Dataset.withAgents(idRoot (), [Agent("Ada", emails = ["a@example.org"; "b@example.org"], identifiers = [], affiliations = [Organization("Lab"); Organization("Lab 2")], additionalTypes = ["Person"; "Staff"])])
        let manyYaml = DatasetYaml.encode(many)
        for key in ["emails: ["; "affiliations:"; "additionalTypes: ["] do
            Expect.isTrue (manyYaml.Contains key) ("Plural form " + key)
        Expect.equal (occurrences manyYaml "identifier") 1 "Only the Dataset writes an identifier, an empty collection writes nothing"
        failsWith "Use only one of" (fun () -> decode "agents:\n  - {type: Agent, name: a, email: x, emails: [y]}\n" |> ignore)
        failsWith "Use only one of" (fun () -> decode "agents:\n  - {type: Agent, name: d, jobTitles: [{type: DefinedTerm, name: t}], jobTitle: {type: DefinedTerm, name: t}}\n" |> ignore)
        let parsed = decode "agents:\n  - {type: Agent, name: a, email: x@example.org, additionalType: One, affiliation: {type: Organization, name: o}}\n  - {type: Agent, name: b, emails: [x@example.org, y@example.org], additionalTypes: [One, Two], affiliations: [{type: Organization, name: o}], identifiers: i2}\n  - {type: Agent, name: c, emails: x@example.org, jobTitle: {type: DefinedTerm, name: t}}\n"
        let agents = List.ofSeq parsed.Agents
        Expect.equal (agents |> List.map (fun a -> Seq.length a.Emails)) [1; 2; 1] "Emails"
        Expect.equal (agents |> List.map (fun a -> Seq.length a.AdditionalTypes)) [1; 2; 0] "additionalTypes"
        Expect.equal (agents |> List.map (fun a -> Seq.length a.Affiliations)) [1; 1; 0] "Affiliations"
        Expect.equal (agents |> List.map (fun a -> Seq.length a.JobTitles)) [0; 0; 1] "Singular jobTitle"
        Expect.equal (Seq.toList (List.item 1 agents).Identifiers) ["i2"] "Scalar under the plural key"
        let identifiers = DatasetYaml.decode("type: Dataset\nconformsTo: [administrative]\nidentifier: root\nidentifiers: [root, other]\n")
        Expect.equal (Seq.toList identifiers.Identifiers) ["root"; "other"] "Dataset identifiers"
        failsWith "identifier" (fun () -> DatasetYaml.decode("type: Dataset\nconformsTo: [administrative]\nidentifier: x\nidentifiers: [root, other]\n") |> ignore)
        let written = DatasetYaml.encode(identifiers)
        Expect.isTrue (written.Contains "identifier: root" && written.Contains "identifiers: [root, other]") "Dataset identifier is the first item"
        Expect.isFalse ((DatasetYaml.encode(Dataset(["administrative"], ["solo"]))).Contains "identifiers") "A single Dataset identifier has no plural key")

    testCase "type aliases and ProcessCore key aliases decode to core entities" (fun _ ->
        let yaml = "@type: schema:Dataset
identifier: demo
labProtocols:
  - \"@type\": bioschemas:LabProtocol
    id: protocol
    name: Extraction
samples:
  - type: bioschemas:Sample
    id: leaf
    name: Leaf
processes:
  - type: bioschemas:LabProcess
    name: run
    inputs:
      - leaf
    outputs:
      - {type: File, path: out.tsv}
      - {type: MediaObject, path: out2.tsv}
    executesProtocol:
      \"@id\": protocol
    parameterValue:
      - {type: schema:PropertyValue, name: temperature, value: 4}
"
        let result = DatasetYaml.decode(yaml)
        Expect.equal (Seq.toList result.ConformsTo) ["process-provenance"] "Inferred profile"
        let processes = List.ofSeq result.Processes
        Expect.equal (processes |> List.map (fun p -> (data p).Path)) ["out.tsv"; "out2.tsv"] "File and MediaObject are Data"
        Expect.equal (processes |> List.map (fun p -> p.Input.IsSome)) [true; false] "A shorter inputs array leaves the input absent"
        Expect.equal (sample (List.head processes)).Name "Leaf" "A plain string in an endpoint position references the sample"
        let recipe = (List.head processes).ExecutesRecipe.Value
        Expect.equal (recipe.Name, recipe.Id) (Some "Extraction", Some "protocol") "executesProtocol and labProtocols"
        same recipe (List.item 1 processes).ExecutesRecipe.Value
        let parameter = Seq.item 0 (List.head processes).ParameterValues
        Expect.equal (parameter.Name, parameter.Value) ("temperature", Some(AnnotationValue.Number 4.0)) "PropertyValue is an Annotation with a plain number")

    testCase "missing conformsTo is inferred and a missing Agent name is derived" (fun _ ->
        let conforms yaml = Seq.toList (DatasetYaml.decode("type: Dataset\nidentifier: x\n" + yaml).ConformsTo)
        Expect.equal (conforms "processes:\n  - {type: Process, name: p}\n") ["process-provenance"] "processes"
        Expect.equal (conforms "descriptors:\n  - {type: Descriptor, describes: {type: Sample, name: s}}\n") ["semantic-designation"] "descriptors"
        Expect.equal (conforms "title: T\n") ["administrative"] "administrative field"
        Expect.equal (conforms "agents: []\n") ["administrative"] "empty administrative collection"
        Expect.equal (conforms "") ["administrative"] "no profile fields"
        Expect.equal (conforms "title: T\nprocesses:\n  - {type: Process, name: p}\n") ["process-provenance"; "administrative"] "combined profiles"
        Expect.equal (conforms "conformsTo: [semantic-designation]\ntitle: T\n") ["semantic-designation"] "An explicit value wins"
        let agents = decode "agents:\n  - {type: Agent, givenName: Ada, familyName: Lovelace}\n  - {type: Agent, givenName: Ada}\n  - {type: Agent, familyName: Lovelace}\n  - {type: Agent}\n  - {type: Agent, name: Explicit, givenName: Ada}\n"
        Expect.equal (agents.Agents |> Seq.map (fun a -> a.Name) |> List.ofSeq) ["Ada Lovelace"; "Ada"; "Lovelace"; ""; "Explicit"] "Agent names")

    testCase "extension keys that collide with a wire key fail on encode" (fun _ ->
        let withExtension (target: EntityObject) key = setProperty target key (Entity.Text "x")
        let agent = Agent("Ada", id = "a")
        withExtension agent "email"
        failsWith "collides" (fun () -> DatasetYaml.encode(Dataset.withAgents(idRoot (), [agent])) |> ignore)
        let owner = Sample("s", id = "s")
        withExtension owner "additionalProperty"
        failsWith "collides" (fun () -> DatasetYaml.encode(Dataset.withProcesses(idRoot (), [process' "p" "p" (withInput owner) None])) |> ignore)
        let proc = ARCBaseModel.Process("p", id = "p")
        withExtension proc "inputs"
        failsWith "collides" (fun () -> DatasetYaml.encode(Dataset.withProcesses(idRoot (), [proc])) |> ignore)
        let root = idRoot ()
        withExtension root "recipes"
        failsWith "collides" (fun () -> DatasetYaml.encode(root) |> ignore)
        let rootIdentifier = idRoot ()
        withExtension rootIdentifier "identifier"
        failsWith "collides" (fun () -> DatasetYaml.encode(rootIdentifier) |> ignore)
        let custom = EntityObject("Custom", id = "c")
        withExtension custom "additionalType"
        let holder = idRoot ()
        setProperty holder "custom" (Entity.Object custom)
        failsWith "collides" (fun () -> DatasetYaml.encode(holder) |> ignore)
        let allowed = Sample("allowed", id = "s2")
        withExtension allowed "inputs"
        let allowedRoot = Dataset.withProcesses(idRoot (), [process' "p" "p" (withInput allowed) None])
        let restored = sample (firstProcess (DatasetYaml.decode(DatasetYaml.encode(allowedRoot))))
        Expect.isTrue (restored.EntityProperties.Contains "inputs") "Keys that are not wire keys of the entity pass")

    testCase "extensions keep their value kinds and shared or distinct typed objects" (fun _ ->
        let source = idRoot ()
        let custom = EntityObject("Custom", id = "custom-1")
        let anonymous = EntityObject("Custom")
        setProperty custom "owner" (Entity.Object source)
        setProperty custom "$ref" (Entity.Text "literal reference-like extension")
        setProperty custom "$id" (Entity.Text "literal ID-like extension")
        let values = EntityCollection([
            Entity.Object custom; Entity.Object custom; Entity.Object anonymous; Entity.Object anonymous
            Entity.Number 0.0; Entity.Number 0.25; Entity.Text "0"; Entity.Text "false"; Entity.Text ""; Entity.Text "null"
            Entity.Bool false; Entity.Bool true; Entity.Null(EntityNull()); Entity.Blob(EntityBlob("AAE="))
            Entity.Collection(EntityCollection([])); Entity.Collection(EntityCollection([Entity.Text "inner"; Entity.Number 1.0]))
        ])
        setProperty source "values" (Entity.Collection values)
        for yaml in [DatasetYaml.encode(source); DatasetYaml.encodeIndexed(source)] do
            let result = DatasetYaml.decode(yaml)
            let values = match result.EntityProperties.Get("values") with Entity.Collection v -> v | _ -> failwith "Expected collection"
            let object index = match values.Get(index) with Entity.Object v -> v | _ -> failwith "Expected object"
            same (object 1) (object 0)
            different (object 2) (object 3)
            Expect.isNone (object 2).Id "Generic typed objects keep an absent ID"
            Expect.equal (object 0).Id (Some "custom-1") "Generic typed objects keep their ID"
            match (object 0).EntityProperties.Get("owner") with Entity.Object v -> same v result | _ -> failwith "Expected owner"
            match (object 0).EntityProperties.Get("$ref") with Entity.Text v -> Expect.equal v "literal reference-like extension" "Literal extension" | _ -> failwith "Expected text"
            match (object 0).EntityProperties.Get("$id") with Entity.Text v -> Expect.equal v "literal ID-like extension" "No reserved serialization field" | _ -> failwith "Expected text"
            match values.Get(4) with Entity.Number v -> Expect.equal v 0.0 "Zero" | _ -> failwith "Expected number"
            match values.Get(5) with Entity.Number v -> Expect.equal v 0.25 "Fraction" | _ -> failwith "Expected number"
            for index, expected in [6, "0"; 7, "false"; 8, ""; 9, "null"] do
                match values.Get(index) with Entity.Text v -> Expect.equal v expected "Text alternative" | _ -> failwith "Expected text"
            match values.Get(10), values.Get(11) with
            | Entity.Bool a, Entity.Bool b -> Expect.equal (a, b) (false, true) "Booleans"
            | _ -> failwith "Expected Booleans"
            match values.Get(12) with Entity.Null _ -> () | _ -> failwith "Expected null"
            match values.Get(13) with Entity.Blob v -> Expect.equal v.Base64 "AAE=" "Binary representation" | _ -> failwith "Expected blob"
            match values.Get(14) with Entity.Collection v -> Expect.equal v.Count 0 "Empty collection" | _ -> failwith "Expected collection"
            match values.Get(15) with
            | Entity.Collection v ->
                Expect.equal v.Count 2 "Nested collection"
                match v.Get(1) with Entity.Number n -> Expect.equal n 1.0 "Nested number" | _ -> failwith "Expected number"
            | _ -> failwith "Expected collection")

    testCase "extension mappings become core entities, typed objects or plain Objects" (fun _ ->
        let result = decode "core:\n  type: Sample\n  name: s\n  id: s1\ntyped:\n  type: DataContext\n  label: L\n  data:\n    type: Data\n    path: p\nplain:\n  alpha: 1\n  beta: text\n  gamma: \"1\"\nflag: true\nnothing: ~\nbytes: !!binary AAE=\nlist:\n  - one\n  - 2\n  - [a]\n"
        let get key = result.EntityProperties.Get(key)
        match get "core" with
        | Entity.Object (:? Sample as v) -> Expect.equal (v.Name, v.Id) ("s", Some "s1") "Core entity"
        | _ -> failwith "Expected a Sample"
        match get "typed" with
        | Entity.Object v ->
            Expect.equal v.Type "DataContext" "Generic type"
            match v.EntityProperties.Get("label"), v.EntityProperties.Get("data") with
            | Entity.Text label, Entity.Object (:? Data as file) -> Expect.equal (label, file.Path) ("L", "p") "Members"
            | _ -> failwith "Expected members"
        | _ -> failwith "Expected an object"
        match get "plain" with
        | Entity.Object v ->
            Expect.equal v.Type "Object" "Typeless mappings are Objects"
            match v.EntityProperties.Get("alpha"), v.EntityProperties.Get("beta"), v.EntityProperties.Get("gamma") with
            | Entity.Number a, Entity.Text b, Entity.Text c -> Expect.equal (a, b, c) (1.0, "text", "1") "Scalar styles"
            | _ -> failwith "Expected scalars"
        | _ -> failwith "Expected an object"
        match get "flag", get "nothing", get "bytes" with
        | Entity.Bool true, Entity.Null _, Entity.Blob blob -> Expect.equal blob.Base64 "AAE=" "Plain scalars"
        | _ -> failwith "Expected Boolean, null and blob"
        match get "list" with
        | Entity.Collection v ->
            Expect.equal v.Count 3 "List"
            match v.Get(0), v.Get(1), v.Get(2) with
            | Entity.Text a, Entity.Number b, Entity.Collection c -> Expect.equal (a, b, c.Count) ("one", 2.0, 1) "List members"
            | _ -> failwith "Expected list members"
        | _ -> failwith "Expected a collection")

    testCase "Annotation values keep numbers, numeric-looking text and absence" (fun _ ->
        let source = Dataset.withAdditionalProperties(idRoot (), [Annotation("zero", value = AnnotationValue.Number 0.0); Annotation("fraction", value = AnnotationValue.Number 0.125); Annotation("text", value = AnnotationValue.Text "0.125"); Annotation("empty", value = AnnotationValue.Text ""); Annotation("absent")])
        let yaml = DatasetYaml.encode(source)
        Expect.isTrue (yaml.Contains("value: \"0.125\"")) "Text is always quoted"
        let result = DatasetYaml.decode(yaml)
        let values = result.AdditionalProperties |> Seq.map (fun a -> a.Value) |> List.ofSeq
        Expect.equal values [Some(AnnotationValue.Number 0.0); Some(AnnotationValue.Number 0.125); Some(AnnotationValue.Text "0.125"); Some(AnnotationValue.Text ""); None] "Alternatives"
        let plain = decode "additionalProperty:\n  - {type: Annotation, name: a, value: 4.5e1}\n  - {type: Annotation, name: b, value: Infinity}\n  - {type: Annotation, name: c, value: 0x10}\n  - {type: Annotation, name: d, value: -.5}\n  - {type: Annotation, name: e, value: true}\n"
        Expect.equal (plain.AdditionalProperties |> Seq.map (fun a -> a.Value.Value) |> List.ofSeq) [AnnotationValue.Number 45.0; AnnotationValue.Text "Infinity"; AnnotationValue.Text "0x10"; AnnotationValue.Number -0.5; AnnotationValue.Text "true"] "Plain scalars are numbers only when they are finite YAML numbers"
        let nonFinite = Dataset.withAdditionalProperties(idRoot (), [Annotation("invalid", value = AnnotationValue.Number Double.NaN)])
        Expect.throws (fun () -> DatasetYaml.encode(nonFinite) |> ignore) "Nonfinite number rejected")

    testCase "malformed documents and unresolved references fail" (fun _ ->
        for yaml in [
            "type: Sample\nname: sample\n"
            "type: Dataset\ntype: Dataset\n"
            "type: Dataset\nconformsTo: [administrative]\n"
            "not: [valid YAML"
            "- not a mapping\n"
            header + "hasPart:\n  - \"@id\": missing\n"
            header + "hasPart:\n  - missing\n"
            header + "agents:\n  - type: Agent\n    name: a\n    id: x\n    \"@id\": y\n"
            header + "annotations:\n  - type: Annotation\n    name: a\n"
            header + "annotations:\n  - \"@id\": a\n"
            header + "recipes: []\nlabProtocols: []\n"
            header + "title:\n  nested: map\n"
            header + "processes:\n  - type: Process\n    name: p\nextra: *alias\n"
            header + "agents:\n  - type: Agent\n    name: a\n    jobTitle: unresolved\n" ] do
            Expect.throws (fun () -> DatasetYaml.decode(yaml) |> ignore) ("Rejected: " + yaml)
        Expect.isNone (decode "").Id "A document without IDs decodes without IDs")

    testCase "indexed and mixed documents resolve references to definitions in any order" (fun _ ->
        let yaml = header + "agents:\n  - \"@id\": a1\n  - type: Agent\n    name: inline\n  - a1\n  - id: a1\nprocesses:\n  - type: Process\n    name: p\n    parameterValue:\n      - \"@id\": pv\n    executesRecipe:\n      \"@id\": r\nannotations:\n  - type: Annotation\n    id: pv\n    \"@id\": pv\n    name: temperature\n  - type: Annotation\n    id: unused\n    \"@id\": unused\n    name: unused\nrecipes:\n  - type: Recipe\n    id: r\n    \"@id\": r\n    name: recipe\n    components:\n      - \"@id\": pv\nagentIndex:\n  - type: Agent\n    id: a1\n    \"@id\": a1\n    name: Ada\n"
        let result = DatasetYaml.decode(yaml)
        let agents = List.ofSeq result.Agents
        Expect.equal (agents |> List.map (fun a -> a.Name)) ["Ada"; "inline"; "Ada"; "Ada"] "References and inline definitions mix"
        same (List.item 0 agents) (List.item 2 agents)
        same (List.item 0 agents) (List.item 3 agents)
        let proc = firstProcess result
        same (Seq.item 0 proc.ParameterValues) (Seq.item 0 proc.ExecutesRecipe.Value.Components)
        Expect.equal (Seq.item 0 proc.ParameterValues).Id (Some "pv") "The index entry supplies the ID")

    testCase "the example documents decode" (fun _ ->
        let minimal = DatasetYaml.decode(readExample "examples/process_core/minimal.yml")
        Expect.equal (minimal.Id, Seq.toList minimal.Identifiers) (Some "dataset:demo", ["demo"]) "Root identity"
        Expect.equal (Seq.toList minimal.ConformsTo) ["process-provenance"; "administrative"] "Inferred profiles"
        let proc = firstProcess minimal
        Expect.equal (proc.Id, proc.Name) (Some "process:extraction", "Extraction") "Process"
        Expect.equal (Seq.toList (sample proc).AdditionalTypes) ["Source"] "Sample additionalType"
        Expect.equal (Seq.item 0 (sample proc).AdditionalProperties).Value (Some(AnnotationValue.Text "Arabidopsis thaliana")) "Characteristic"
        same (data proc) (Seq.item 0 minimal.DataFiles)
        Expect.equal (Seq.length minimal.DataFiles, Seq.length minimal.HasParts) (1, 0) "Data in hasPart is a data file"
        let recipe = proc.ExecutesRecipe.Value
        Expect.equal (recipe.Id, recipe.Name) (Some "protocol:extraction", Some "Protein extraction") "Recipe"
        Expect.equal ((Seq.item 0 recipe.Parameters).Id, (Seq.item 0 recipe.Parameters).Name) (Some "parameter:temperature", Some "temperature") "FormalParameter"
        let value = Seq.item 0 proc.ParameterValues
        Expect.equal (value.Value, value.Unit) (Some(AnnotationValue.Number 4.0), Some "degree Celsius") "ParameterValue"
        let administrative = DatasetYaml.decode(readExample "examples/administrative/dataset_administration.yml")
        Expect.equal (Seq.toList administrative.ConformsTo) ["administrative"] "Administrative profile"
        Expect.equal (administrative.License, administrative.DateCreated) (Some "CC-BY-4.0", Some "2026-06-30") "Quoted dates"
        let agent = Seq.item 0 administrative.Agents
        Expect.equal agent.Name "Ada Lovelace" "Name derived from givenName and familyName"
        Expect.equal (Seq.toList agent.Emails, Seq.toList agent.Identifiers) (["ada@example.org"], ["ORCID:0000-0000-0000-0001"]) "Agent scalars"
        Expect.equal ((Seq.item 0 agent.Affiliations).Name, (Seq.item 0 agent.JobTitles).Name) ("Example Lab", "data steward") "Affiliation and jobTitle"
        let article = Seq.item 0 administrative.Citations
        Expect.equal (article.Headline, (Seq.item 0 article.Authors).Name, article.CreativeWorkStatus.Value.Name) ("Example methods for ARC data stewardship", "Ada Lovelace", "published") "Citation"
        let datamap = DatasetYaml.decode(readExample "examples/datamap/proteomics_data.yml")
        Expect.equal (Seq.toList datamap.Identifiers) ["datamap-proteomics-example"] "Datamap identifier"
        Expect.equal (Seq.length (Seq.item 0 datamap.DataFiles).HasParts) 1 "Nested Data"
        match datamap.EntityProperties.Get("dataContexts") with
        | Entity.Collection v ->
            Expect.equal v.Count 2 "dataContexts stay extension values"
            match v.Get(0) with
            | Entity.Object context -> Expect.equal context.Type "DataContext" "Generic typed object"
            | _ -> failwith "Expected an object"
        | _ -> failwith "Expected a collection"
        let analysis = DatasetYaml.decode(readExample "examples/datamap/metadata_powered_analysis.yml")
        Expect.equal (Seq.length analysis.Processes) 6 "Six processes"
        Expect.isTrue (analysis.Processes |> Seq.forall (fun p -> p.Input.IsSome && p.Output.IsSome)) "Endpoints"
        Expect.equal (Seq.toList analysis.ConformsTo) ["process-provenance"; "administrative"] "Inferred profiles"
        let last = Seq.item 5 analysis.Processes
        Expect.equal ((data last).Selector, (data last).EncodingFormat) (Some "#col=3", Some "text/tab-separated-values") "Data output"
        let firstOutput = match (firstProcess analysis).Output with Some(EntityReference.Sample v) -> v | _ -> failwith "Expected a Sample output"
        let factor = Seq.item 0 firstOutput.AdditionalProperties
        Expect.equal (factor.Value, factor.Unit) (Some(AnnotationValue.Text "35"), Some "degree Celsius") "Quoted numbers stay text")

    testCase "documents written by the ProcessCore encoder decode" (fun _ ->
        for file, indexed in ["processcore-dataset.yml", false; "processcore-dataset-indexed.yml", true] do
            let result = DatasetYaml.decode(readExample ("tests/ARCManagement.Tests/fixtures/" + file))
            Expect.equal (Seq.toList result.Identifiers, result.Title) (["pc-demo"], Some "ProcessCore fixture") "Dataset fields"
            Expect.equal (Seq.toList result.ConformsTo) ["process-provenance"; "administrative"] "Inferred profiles"
            let processes = List.ofSeq result.Processes
            Expect.equal (processes |> List.map (fun p -> p.Name)) ["Extraction"; "Extraction"; "Extraction"; "Measurement"] "A grouped entry expands to one process per input"
            Expect.equal (processes |> List.take 3 |> List.map (fun p -> (sample p).Name)) ["Leaf 1"; "Leaf 2"; "Leaf 3"] "Inputs"
            Expect.equal (processes |> List.take 3 |> List.map (fun p -> match p.Output with Some(EntityReference.Sample v) -> v.Name | _ -> "")) ["Extract 1"; "Extract 2"; "Extract 3"] "Outputs pair with inputs"
            Expect.equal (Seq.toList (sample (List.head processes)).AdditionalTypes) ["Source"] "additionalType"
            let recipe = (List.head processes).ExecutesRecipe.Value
            Expect.equal (recipe.Name, recipe.Version, recipe.Url) (Some "extraction", Some "1.0", Some "https://example.org/extraction") "Recipe"
            Expect.equal ((Seq.item 0 recipe.Parameters).Name, (Seq.item 0 recipe.Components).Name) (Some "temperature", "buffer") "Recipe members"
            match recipe.IntendedUse with
            | Some(RecipeIntendedUse.Term term) -> Expect.equal (term.Name, term.TAN) ("cell lysis", Some "GO:0019835") "Intended use term"
            | _ -> failwith "Expected a term"
            let value = Seq.item 0 (List.head processes).ParameterValues
            Expect.equal (value.Value, value.Unit) (Some(AnnotationValue.Number 4.0), Some "degree Celsius") "Parameter value"
            same (List.item 1 processes).ExecutesRecipe.Value recipe
            same (List.item 2 processes).ExecutesRecipe.Value recipe
            same (Seq.item 0 (List.item 1 processes).ParameterValues) value
            if not indexed then
                let again = DatasetYaml.encode(result)
                Expect.equal (occurrences again "name: Extraction") 1 "The expanded processes group into one entry again"
                Expect.equal (occurrences again "type: Process") 2 "Extraction and Measurement"
            Expect.equal ((data (List.item 3 processes)).Path, (sample (List.item 3 processes)).Name) ("raw/measurement.tsv", "Extract 1") "Measurement"
            let agent = Seq.item 0 result.Agents
            Expect.equal (agent.Name, Seq.toList agent.Emails, Seq.toList agent.Identifiers) ("Ada Lovelace", ["ada@example.org"], ["ORCID:0000-0000-0000-0001"]) "Agent"
            Expect.equal ((Seq.item 0 agent.Affiliations).Name, (Seq.item 0 agent.JobTitles).Name) ("Example Lab", "data steward") "Agent members"
            Expect.equal (Seq.item 0 result.AdditionalProperties).Value (Some(AnnotationValue.Text "demo")) "additionalProperty")

    testCase "session YAML import adopts an independent graph and normal ID policy" (fun _ ->
        let source = Dataset(["process-provenance"], ["root"], id = "supplied-root")
        let child = Dataset(["administrative"], ["child"])
        source.State <- { source.State with HasParts = [child; source] }
        let yaml = DatasetYaml.encode(source)
        use session = Session.createInMemory()
        let first = session.importYaml(yaml)
        let second = session.importYaml(yaml)
        Expect.equal first.Model.Id (Some "supplied-root") "Import keeps supplied IDs"
        Expect.isSome (Seq.item 0 first.Model.HasParts).Id "Import assigns omitted IDs"
        Expect.isNone child.Id "Source remains detached and idless"
        different first.Model second.Model
        same (Seq.item 1 first.Model.HasParts) first.Model
        Expect.notEqual first.ArcId second.ArcId "Every import creates an independent entry"
        Expect.isFalse first.History.CanUndo "Import does not create an edit command"
        Expect.equal (session.listArcs()).Count 2 "No implicit extra entries"
        Expect.isNone session.DatabasePath "Import requires no filesystem repository"
        let repeated = session.importYaml(header + "hasPart:\n  - {type: Dataset, id: same, identifier: a}\n  - {type: Dataset, id: same, identifier: a}\n")
        same (Seq.item 0 repeated.Model.HasParts) (Seq.item 1 repeated.Model.HasParts)
        Expect.equal (session.listArcs()).Count 3 "A repeated ID with equal definitions is one entity"
        failsWith "Conflicting definitions" (fun () -> session.importYaml(header + "hasPart:\n  - {type: Dataset, id: same, identifier: a}\n  - {type: Dataset, id: same, identifier: b}\n") |> ignore)
        Expect.throws (fun () -> session.importYaml("invalid") |> ignore) "Invalid YAML import rejected"
        Expect.equal (session.listArcs()).Count 3 "Failed imports leave the repository unchanged")

    testCase "managed YAML exports the checked root without changing history or registry" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"], ["root"]))
        let sample = graph.Sample.create("standalone")
        let proc = graph.Process.create("attached")
        graph.Dataset.addProcess(graph.Model, proc) |> ignore
        graph.Process.setName(proc, "changed") |> ignore
        graph.History.undo()
        Expect.isTrue graph.History.CanRedo "Redo exists before export"
        let yaml = graph.toYaml()
        Expect.isFalse (yaml.Contains("standalone")) "Standalone registered objects excluded"
        Expect.isTrue (yaml.Contains("id: " + graph.Model.Id.Value)) "Managed IDs are written"
        Expect.isTrue graph.History.CanRedo "Export leaves redo branch intact"
        Expect.equal (graph.Sample.get(sample.Id.Value)).Name "standalone" "Registry unchanged by export"
        let detached = DatasetYaml.decode(yaml)
        Expect.equal (Seq.item 0 detached.Processes).Name "attached" "Exports retained root state"
        Expect.equal detached.Id graph.Model.Id "Assigned domain ID retained"
        graph.History.redo()
        Expect.equal (graph.Process.get(proc.Id.Value)).Name "changed" "History remains usable"
        Expect.equal proc.Name "attached" "Held value is unchanged"
        Expect.isTrue ((graph.toYaml()).Contains("changed")) "Export follows the current state")

    testCase "type aliases apply only where a core kind is expected" (fun _ ->
        let source = idRoot ()
        setProperty source "file" (Entity.Object(EntityObject("File", id = "file-1")))
        setProperty source "media" (Entity.Object(EntityObject("MediaObject")))
        setProperty source "dataset" (Entity.Object(EntityObject("schema:Dataset")))
        let check (result: Dataset) =
            for key, expected in ["file", "File"; "media", "MediaObject"; "dataset", "schema:Dataset"] do
                match result.EntityProperties.Get(key) with
                | Entity.Object v -> Expect.equal v.Type expected ("Generic type " + expected)
                | _ -> failwith "Expected an object"
        check (DatasetYaml.decode(DatasetYaml.encode(source)))
        check (DatasetYaml.decode(DatasetYaml.encodeIndexed(source)))
        use session = Session.createInMemory()
        let graph = session.createArc(source)
        check (DatasetYaml.decode(graph.toYaml()))
        check (session.importYaml(graph.toYaml())).Model
        let typed = decode "mixed:\n  type: File\n  path: not-a-path-of-Data\n"
        match typed.EntityProperties.Get("mixed") with
        | Entity.Object v ->
            Expect.equal v.Type "File" "A File in an extension is a generic object"
            Expect.isTrue (v.EntityProperties.Contains "path") "Its members are extension properties"
        | _ -> failwith "Expected an object"
        Expect.equal (Seq.item 0 (decode "dataFiles:\n  - {type: File, path: p}\n").DataFiles).Path "p" "File is Data where Data is expected"
        Expect.equal (DatasetYaml.decode("type: schema:Dataset\nidentifier: x\nconformsTo: [administrative]\n")).Type "Dataset" "The root accepts the Dataset alias")

    testCase "comments are ignored between mappings and sequence items" (fun _ ->
        let yaml = "# header\ntype: Dataset # trailing\nconformsTo: [administrative]\nidentifier: r\n# between\ntitle: t\nhasPart:\n  - type: Dataset\n    # inside an item\n    conformsTo: [administrative]\n    identifier: a\n  # between items\n  - type: Dataset\n    conformsTo: [administrative]\n    identifier: b\n"
        let result = DatasetYaml.decode(yaml)
        Expect.equal (result.Title, Seq.length result.HasParts) (Some "t", 2) "Comments do not change the content"
        Expect.equal (Seq.item 0 (Seq.item 1 result.HasParts).Identifiers) "b" "Items after a comment are read")

    testCase "a sibling of a grouped process can be written inside another member" (fun _ ->
        for ids in [false; true] do
            let identify (value: string) = if ids then Some value else None
            let second = ARCBaseModel.Process("run", input = EntityReference.Sample(Sample("two")), ?id = identify "p2")
            let one = Sample("one")
            setProperty one "next" (Entity.Object second)
            let first = ARCBaseModel.Process("run", input = EntityReference.Sample one, ?id = identify "p1")
            let source = Dataset.withProcesses(Dataset(["process-provenance"], ["root"]), [first; second])
            let yaml = DatasetYaml.encode(source)
            Expect.isFalse (yaml.Contains "\"@id\"") "A sibling is not a cycle"
            let result = DatasetYaml.decode(yaml)
            Expect.equal (Seq.length result.Processes) 2 "Both processes come back"
            match (sample (firstProcess result)).EntityProperties.Get("next") with
            | Entity.Object (:? ARCBaseModel.Process as v) -> Expect.equal v.Name "run" "The sibling is nested"
            | _ -> failwith "Expected a Process"
        let one = Annotation("recipe")
        let a = Recipe(name = "r", additionalProperties = [one])
        let b = Recipe(name = "r", additionalProperties = [one])
        let distinct = Dataset.withProcesses(Dataset(["process-provenance"], ["root"]), [ARCBaseModel.Process("run", input = EntityReference.Sample(Sample("a")), executesRecipe = a); ARCBaseModel.Process("run", input = EntityReference.Sample(Sample("b")), executesRecipe = b)])
        Expect.equal (occurrences (DatasetYaml.encode(distinct)) "type: Process") 2 "Equal ID-less recipes that are distinct objects do not group"
        let shared = Dataset.withProcesses(Dataset(["process-provenance"], ["root"]), [ARCBaseModel.Process("run", input = EntityReference.Sample(Sample("a")), executesRecipe = a); ARCBaseModel.Process("run", input = EntityReference.Sample(Sample("b")), executesRecipe = a)])
        Expect.equal (occurrences (DatasetYaml.encode(shared)) "type: Process") 1 "A shared recipe object groups")

    testCase "Data in hasPart follows explicit data files and counts for the inferred profile" (fun _ ->
        let result = decode "dataFiles:\n  - {type: Data, path: a}\n  - {type: Data, path: b}\nhasPart:\n  - {type: Data, path: d}\n  - {type: Dataset, identifier: child, conformsTo: [administrative]}\n"
        Expect.equal (result.DataFiles |> Seq.map (fun f -> f.Path) |> List.ofSeq) ["a"; "b"; "d"] "hasPart Data goes last and keeps its order"
        Expect.equal (Seq.length result.HasParts) 1 "Datasets stay parts"
        let inferred yaml = Seq.toList (DatasetYaml.decode("type: Dataset\nidentifier: x\n" + yaml).ConformsTo)
        Expect.equal (inferred "processes:\n  - {type: Process, name: p}\nhasPart:\n  - {type: Data, path: d}\n") ["process-provenance"; "administrative"] "Data from hasPart implies administrative"
        Expect.equal (inferred "hasPart:\n  - {type: Data, path: d}\n") ["administrative"] "Only Data in hasPart"
        Expect.equal (inferred "processes:\n  - {type: Process, name: p}\nhasPart:\n  - {type: Dataset, identifier: c, conformsTo: [administrative]}\n") ["process-provenance"] "Dataset parts add nothing")

    testCase "text that YAML would type implicitly is quoted" (fun _ ->
        let source = Dataset(["administrative"], ["true"; "1"], id = "null", hasParts = [Dataset(["administrative"], ["x"], id = "12")], additionalProperties = [Annotation("n", id = "~", additionalTypes = ["false"])])
        let yaml = DatasetYaml.encode(source)
        for expected in ["id: \"null\""; "id: \"12\""; "id: \"~\""; "identifier: \"true\""; "additionalType: \"false\""] do
            Expect.isTrue (yaml.Contains expected) ("Quoted: " + expected)
        for unexpected in ["id: null"; "id: 12"; "id: ~"; "identifier: true"] do
            Expect.isFalse (yaml.Contains unexpected) ("Not plain: " + unexpected)
        let result = DatasetYaml.decode(yaml)
        Expect.equal (result.Id, Seq.toList result.Identifiers) (Some "null", ["true"; "1"]) "Text round trip"
        Expect.equal (Seq.item 0 result.HasParts).Id (Some "12") "Numeric-looking ID"
        Expect.equal (Seq.item 0 result.AdditionalProperties).Id (Some "~") "Null-looking ID")

    testCase "indexed mode rejects distinct objects that share an ID with differing definitions" (fun _ ->
        let source = Dataset.withAdditionalProperties(idRoot (), [Annotation("one", id = "x"); Annotation("two", id = "x")])
        failsWith "Conflicting definitions for entity ID x" (fun () -> DatasetYaml.encodeIndexed(source) |> ignore)
        failsWith "Conflicting definitions for entity ID x" (fun () -> DatasetYaml.encode(source) |> ignore)
        let equal = Dataset.withAdditionalProperties(idRoot (), [Annotation("one", id = "x"); Annotation("one", id = "x")])
        let result = DatasetYaml.decode(DatasetYaml.encodeIndexed(equal))
        same (Seq.item 0 result.AdditionalProperties) (Seq.item 1 result.AdditionalProperties))

    testCase "a folder whose arc.yml cannot be read opens with the sql source and saves in the new format" (fun _ ->
        let path = Helpers.folder "yaml"
        let created = ARC.create(path, Dataset(["process-provenance"], ["example"]))
        created.save()
        created.close()
        let file = Path.combineNative path ARCtrl.Helper.Layout.ARCFileName
        Path.writeFileText file "type: Dataset\n$ref: legacy\n"
        Expect.throws (fun () -> ARC.openFolder(path, "yml") |> ignore) "The old text is not readable"
        Expect.throws (fun () -> ARC.openFolder(path, "auto") |> ignore) "Automatic recovery needs a readable file"
        use resumed = ARC.openFolder(path, "sql")
        Expect.isTrue resumed.IsDirty "The SQL state is not yet published"
        resumed.save()
        Expect.equal (Seq.toList (DatasetYaml.decode(Path.readFileText file)).Identifiers) ["example"] "The file is rewritten and readable"
        Expect.isFalse resumed.IsDirty "Saved")

    testCase "history rows written with a suppliedId key still decode" (fun _ ->
        Expect.equal (Legacy.decodeWithSuppliedId ()) "s" "The obsolete key is ignored")
]
