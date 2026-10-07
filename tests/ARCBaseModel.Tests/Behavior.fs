module ARCBaseModel.Tests.Behavior

open System
open ARCBaseModel
open ARCBaseModel.Tests
open Fable.Pyxpecto

let private same actual expected message =
    Expect.isTrue (Object.ReferenceEquals(actual, expected)) message

let private sampleOf value =
    match value with
    | EntityReference.Sample sample -> sample
    | EntityReference.Data _ -> failwith "Expected a Sample alternative."

let private dataOf value =
    match value with
    | EntityReference.Data data -> data
    | EntityReference.Sample _ -> failwith "Expected a Data alternative."

let private construction = testList "construction" [
    testCase "every discriminator is fixed and domain IDs default to absent" <| fun _ ->
        let annotation = Annotation("temperature")
        let term = DefinedTerm("temperature")
        let termSet = DefinedTermSet("ontology")
        let parameter = FormalParameter()
        let sample = Sample("sample")
        let data = Data("measurements.csv")
        let recipe = Recipe()
        let transformation = Process("measurement")
        let descriptor = Descriptor(EntityReference.Sample sample)
        let organization = Organization("laboratory")
        let agent = Agent("Ada")
        let article = ScholarlyArticle("Results")
        let dataset = Dataset([ "administrative" ], [ "accession:1" ])
        for expected, actual, id in [
            "Annotation", annotation.Type, annotation.Id
            "DefinedTerm", term.Type, term.Id
            "DefinedTermSet", termSet.Type, termSet.Id
            "FormalParameter", parameter.Type, parameter.Id
            "Sample", sample.Type, sample.Id
            "Data", data.Type, data.Id
            "Recipe", recipe.Type, recipe.Id
            "Process", transformation.Type, transformation.Id
            "Descriptor", descriptor.Type, descriptor.Id
            "Organization", organization.Type, organization.Id
            "Agent", agent.Type, agent.Id
            "ScholarlyArticle", article.Type, article.Id
            "Dataset", dataset.Type, dataset.Id
        ] do
            Expect.equal actual expected "Fixed discriminator"
            Expect.isNone id "Constructor must not infer or assign a domain ID"

    testCase "required strings reject null without imposing extra text restrictions" <| fun _ ->
        let invalidConstructors = [
            fun () -> Annotation(null) |> ignore
            fun () -> DefinedTerm(null) |> ignore
            fun () -> DefinedTermSet(null) |> ignore
            fun () -> Sample(null) |> ignore
            fun () -> Data(null) |> ignore
            fun () -> Process(null) |> ignore
            fun () -> Organization(null) |> ignore
            fun () -> Agent(null) |> ignore
            fun () -> ScholarlyArticle(null) |> ignore
        ]
        invalidConstructors |> List.iter (fun construct -> Expect.throws construct "Required value is null")
        Expect.equal (Sample("").Name) "" "An empty name is not silently normalized"
        Expect.equal (Data(" ./file.csv ").Path) " ./file.csv " "Paths remain supplied strings"
        Expect.equal (Agent(" Ada ").Name) " Ada " "Names are not trimmed"

    testCase "descriptor requires a real Sample or Data reference" <| fun _ ->
        Expect.throws
            (fun () -> Descriptor(EntityReference.Sample(Unchecked.defaultof<Sample>)) |> ignore)
            "A Sample case with null payload is not a described entity"
        Expect.throws
            (fun () -> Descriptor(EntityReference.Data(Unchecked.defaultof<Data>)) |> ignore)
            "A Data case with null payload is not a described entity"

    testCase "dataset requires identifiers and a recognized case-sensitive profile" <| fun _ ->
        Expect.throws (fun () -> Dataset([], [ "id" ]) |> ignore) "Missing profiles"
        Expect.throws (fun () -> Dataset([ "administrative" ], []) |> ignore) "Missing identifiers"
        Expect.throws (fun () -> Dataset([ "Administrative" ], [ "id" ]) |> ignore) "Case-sensitive profiles"
        Expect.throws (fun () -> Dataset([ "custom" ], [ "id" ]) |> ignore) "Custom classification alone is not a base profile"
        Expect.throws (fun () -> Dataset(Unchecked.defaultof<seq<string>>, [ "id" ]) |> ignore) "Null profiles"
        Expect.throws (fun () -> Dataset([ "administrative" ], Unchecked.defaultof<seq<string>>) |> ignore) "Null identifiers"
        for profile in [ "administrative"; "process-provenance"; "semantic-designation" ] do
            let dataset = Dataset([ profile; "custom"; profile ], [ "id"; "id" ])
            Suspect.sequenceEqual dataset.ConformsTo [ profile; "custom"; profile ] "Open profiles and duplicates survive"
            Suspect.sequenceEqual dataset.Identifiers [ "id"; "id" ] "Identifiers are not domain IDs or deduplicated"
            Expect.isNone dataset.Id "Dataset identifiers do not imply Id"

    testCase "all combinations of base profiles are representable on one Dataset" <| fun _ ->
        let combinations = [
            [ "administrative" ]; [ "process-provenance" ]; [ "semantic-designation" ]
            [ "administrative"; "process-provenance" ]
            [ "administrative"; "semantic-designation" ]
            [ "process-provenance"; "semantic-designation" ]
            [ "administrative"; "process-provenance"; "semantic-designation" ]
        ]
        for profiles in combinations do
            Suspect.sequenceEqual (Dataset(profiles, [ "id" ]).ConformsTo) profiles "All profile combinations use one entity"

    testCase "construction checks do not add continuous conformance rules" <| fun _ ->
        let dataset = Dataset([ "administrative" ], [ "id" ])
        dataset.ConformsTo.Clear()
        dataset.Identifiers <- ResizeArray()
        Expect.equal dataset.ConformsTo.Count 0 "Live collection mutation remains possible"
        Expect.equal dataset.Identifiers.Count 0 "Replacing a collection does not invoke a validator"
]

let private collections = testList "collections and identity" [
    testCase "constructor containers are copied, references and repeated entries are retained" <| fun _ ->
        let first = Annotation("first")
        let second = Annotation("second")
        let supplied = ResizeArray([ first; second; first ])
        let sample = Sample("sample", additionalProperties = supplied)
        supplied.Clear()
        Expect.equal sample.AdditionalProperties.Count 3 "Stored collection is independent of constructor input"
        same sample.AdditionalProperties[0] first "First annotation is not copied"
        same sample.AdditionalProperties[1] second "Ordering is preserved"
        same sample.AdditionalProperties[2] first "Repeated annotation reference is preserved"
        first.Name <- "changed"
        Expect.equal sample.AdditionalProperties[2].Name "changed" "Shared reference remains live"

    testCase "collection setters copy containers and getters expose live collections" <| fun _ ->
        let sample = Sample("sample")
        let annotation = Annotation("metadata")
        let replacement = ResizeArray([ annotation; annotation ])
        sample.AdditionalProperties <- replacement
        replacement.Add(Annotation("external"))
        Expect.equal sample.AdditionalProperties.Count 2 "Setter copied the container"
        same sample.AdditionalProperties[1] annotation "Setter kept duplicate entity references"
        let live = sample.AdditionalProperties
        live.Add(annotation)
        Expect.equal sample.AdditionalProperties.Count 3 "Getter exposes a live mutable container"

    testCase "empty defaults and string collection containers are independent" <| fun _ ->
        let first = Recipe()
        let second = Recipe()
        first.Components.Add(Annotation("instrument"))
        first.AdditionalTypes.Add("custom")
        Expect.equal second.Components.Count 0 "Each instance owns its default collections"
        Expect.equal first.AdditionalProperties.Count 0 "Different collection properties do not share storage"
        Expect.equal second.AdditionalTypes.Count 0 "Classification defaults are independent"
        let supplied = ResizeArray([ "a"; "a"; "b" ])
        second.AdditionalTypes <- supplied
        supplied.Clear()
        Suspect.sequenceEqual second.AdditionalTypes [ "a"; "a"; "b" ] "String collections preserve order and duplicates"

    testCase "distinct same-ID and same-field instances remain distinct" <| fun _ ->
        let first = Sample("same", id = "sample:1")
        let second = Sample("same", id = "sample:1")
        Expect.isFalse (Object.ReferenceEquals(first, second)) "No interning or ID canonicalization"
        Expect.isFalse (first.Equals(second)) "No domain equality by fields or IDs"
        let descriptor1 = Descriptor(EntityReference.Sample first)
        let descriptor2 = Descriptor(EntityReference.Sample second)
        let dataset = Dataset([ "semantic-designation" ], [ "id" ], descriptors = [ descriptor1; descriptor2; descriptor1 ])
        same (sampleOf dataset.Descriptors[0].Describes) first "First object is retained"
        same (sampleOf dataset.Descriptors[1].Describes) second "Second object is not merged by Id"
        same dataset.Descriptors[2] descriptor1 "Duplicate descriptors survive"

    testCase "optional IDs can be assigned and cleared without identity side effects" <| fun _ ->
        let sample = Sample("sample")
        let descriptor = Descriptor(EntityReference.Sample sample)
        same (MappingProbe.AssignSampleId(sample, Some "sql:1")) sample "ID assignment returns the same object"
        Expect.equal (MappingProbe.ReadSampleId(sample)) (Some "sql:1") "Layer 2 can explicitly use the domain Id"
        same (sampleOf descriptor.Describes) sample "Existing references survive ID mutation"
        MappingProbe.AssignSampleId(sample, None) |> ignore
        Expect.isNone sample.Id "IDs remain clearable"
]

let private domain = testList "linked model" [
    testCase "annotation alternatives and optional values preserve zero and empty text" <| fun _ ->
        let annotation = Annotation("temperature")
        Expect.isNone annotation.Value "Initially absent"
        annotation.Value <- Some(AnnotationValue.Number 0.0)
        match annotation.Value with
        | Some(AnnotationValue.Number value) -> Expect.equal value 0.0 "Zero is a value"
        | _ -> failwith "Numeric zero was lost"
        annotation.Value <- Some(AnnotationValue.Text "")
        match annotation.Value with
        | Some(AnnotationValue.Text value) -> Expect.equal value "" "Empty text is a value"
        | _ -> failwith "Empty text was lost"
        annotation.Value <- None
        annotation.Unit <- Some ""
        Expect.equal annotation.Unit (Some "") "Empty optional string is distinct from None"
        annotation.Unit <- None
        Expect.isNone annotation.Value "Value cleared"
        Expect.isNone annotation.Unit "Unit cleared"

    testCase "annotations and formal parameters can link mutually without copying" <| fun _ ->
        let parameter = FormalParameter(name = "temperature", nameTAN = "https://example.org/temperature")
        let annotation = Annotation("temperature", value = AnnotationValue.Number 23.5, unit = "C", nameTAN = "key", valueTAN = "value", unitTAN = "unit", instanceOf = parameter)
        parameter.DefaultValue <- Some annotation
        same annotation.InstanceOf.Value parameter "InstanceOf retains parameter identity"
        same parameter.DefaultValue.Value annotation "DefaultValue retains annotation identity"
        Expect.equal annotation.NameTAN (Some "key") "Key TAN retained"
        Expect.equal annotation.ValueTAN (Some "value") "Value TAN retained"
        Expect.equal annotation.UnitTAN (Some "unit") "Unit TAN retained"
        Expect.equal parameter.NameTAN (Some "https://example.org/temperature") "Parameter TAN retained"
        parameter.DefaultValue <- None
        Expect.isNone parameter.DefaultValue "Optional default clears"
        same annotation.InstanceOf.Value parameter "Clearing one link does not maintain an automatic backlink"

    testCase "one Dataset combines profiles with independently profiled recursive children" <| fun _ ->
        let sample = Sample("sample")
        let data = Data("data.csv")
        let transformation = Process("measure", input = EntityReference.Sample sample, output = EntityReference.Data data)
        let descriptor = Descriptor(EntityReference.Data data, annotations = [ Annotation("quality", value = AnnotationValue.Text "good") ])
        let grandchild = Dataset([ "administrative" ], [ "grandchild" ])
        let child = Dataset([ "semantic-designation" ], [ "child" ], descriptors = [ descriptor ], hasParts = [ grandchild ])
        let parent = Dataset([ "administrative"; "process-provenance" ], [ "parent" ], processes = [ transformation ], dataFiles = [ data ], hasParts = [ child ])
        Suspect.sequenceEqual parent.HasParts[0].ConformsTo [ "semantic-designation" ] "Child did not inherit parent profiles"
        Suspect.sequenceEqual parent.HasParts[0].HasParts[0].ConformsTo [ "administrative" ] "Mixed profiles nest at arbitrary depths"
        same (dataOf parent.Processes[0].Output.Value) parent.DataFiles[0] "Process and Dataset share the same Data"
        same (dataOf child.Descriptors[0].Describes) data "Cross-profile reference retains identity"
        Expect.equal parent.Descriptors.Count 0 "Descriptor is not automatically registered in ancestors"

    testCase "Data fragments retain their own paths and selectors" <| fun _ ->
        let deepest = Data("other.csv", selector = "row=7", selectorFormat = "https://www.rfc-editor.org/rfc/rfc7111", encodingFormat = "text/csv")
        let fragment = Data("data.csv", selector = "row=2", hasParts = [ deepest ])
        let file = Data("data.csv", hasParts = [ fragment; fragment ])
        same file.HasParts[0] fragment "Fragments preserve identity"
        same file.HasParts[1] fragment "Repeated fragments preserve duplicate references"
        Expect.equal file.HasParts[0].HasParts[0].Path "other.csv" "Nested paths are never inferred from parents"
        Expect.equal deepest.Selector (Some "row=7") "Selector preserved as text"
        Expect.equal deepest.EncodingFormat (Some "text/csv") "Encoding preserved"
        Expect.equal deepest.SelectorFormat (Some "https://www.rfc-editor.org/rfc/rfc7111") "Selector format preserved"

    testCase "process endpoints are singular optional and freely replaceable" <| fun _ ->
        let transformation = Process("transform")
        Expect.isNone transformation.Input "Absent input is valid"
        Expect.isNone transformation.Output "Absent output is valid"
        Expect.isNone transformation.ExecutesRecipe "Absent recipe is valid"
        let sample = Sample("sample")
        let data = Data("data.csv")
        transformation.Input <- Some(EntityReference.Sample sample)
        transformation.Output <- Some(EntityReference.Data data)
        same (sampleOf transformation.Input.Value) sample "Sample input"
        same (dataOf transformation.Output.Value) data "Data output"
        transformation.Input <- Some(EntityReference.Data data)
        transformation.Output <- Some(EntityReference.Sample sample)
        same (dataOf transformation.Input.Value) data "Endpoint alternative can change"
        same (sampleOf transformation.Output.Value) sample "Either endpoint accepts either type"
        transformation.Input <- None
        transformation.Output <- None
        Expect.isNone transformation.Input "Input cleared"
        Expect.isNone transformation.Output "Output cleared"

    testCase "recipe parameters and components retain shared annotations" <| fun _ ->
        let parameter = FormalParameter(name = "temperature")
        let annotation = Annotation("temperature", value = AnnotationValue.Number 42.0, instanceOf = parameter)
        let term = DefinedTerm("measurement")
        let recipe = Recipe(name = "measure", description = "measurement protocol", intendedUse = RecipeIntendedUse.Term term, parameters = [ parameter; parameter ], components = [ annotation ], additionalProperties = [ annotation ], version = "v1", url = "protocol.txt")
        let transformation = Process("measurement", executesRecipe = recipe, parameterValues = [ annotation; annotation ])
        same transformation.ExecutesRecipe.Value recipe "Recipe reference retained"
        same recipe.Parameters[0] parameter "Parameters retained"
        same recipe.Parameters[1] parameter "Duplicate parameter retained"
        same recipe.Components[0] transformation.ParameterValues[1] "Annotations shared across relationships"
        same recipe.AdditionalProperties[0] annotation "Metadata reference retained"
        Expect.equal recipe.Description (Some "measurement protocol") "Description preserved"
        Expect.equal recipe.Version (Some "v1") "Version preserved"
        Expect.equal recipe.Url (Some "protocol.txt") "URLs are not normalized"
        recipe.IntendedUse <- Some(RecipeIntendedUse.Text "custom")
        match recipe.IntendedUse with
        | Some(RecipeIntendedUse.Text text) -> Expect.equal text "custom" "Text alternative is available"
        | _ -> failwith "Expected textual intended use"
        recipe.IntendedUse <- None
        Expect.isNone recipe.IntendedUse "Intended use cleared"

    testCase "administrative entities preserve affiliations authors metadata and dates" <| fun _ ->
        let organization = Organization("Lab", url = "https://example.org")
        let role = DefinedTerm("researcher", identifier = "role:1", tan = "ROLE:1")
        let property = Annotation("note", value = AnnotationValue.Text "supplied")
        let agent = Agent("Ada Lovelace", givenName = "Ada", familyName = "Lovelace", emails = [ "ada@example.org"; "ada@example.org" ], affiliations = [ organization; organization ], identifiers = [ "orcid:1" ], jobTitles = [ role ], additionalProperties = [ property ])
        let status = DefinedTerm("published")
        let article = ScholarlyArticle("A study", authors = [ agent; agent ], identifiers = [ "doi:1"; "pmid:2" ], creativeWorkStatus = status, additionalProperties = [ property ])
        let dataset = Dataset([ "administrative" ], [ "accession:1" ], title = "Study", description = "Description", license = "custom license", datePublished = "2026", dateCreated = "yesterday", dateModified = "today", agents = [ agent ], citations = [ article ], additionalProperties = [ property ])
        same dataset.Agents[0] article.Authors[0] "Dataset agent and author share one object"
        same article.Authors[1] agent "Duplicate authors retained"
        same agent.Affiliations[1] organization "Duplicate affiliations retained"
        same agent.JobTitles[0] role "Role is a term reference"
        same article.CreativeWorkStatus.Value status "Publication status remains a reference"
        same dataset.AdditionalProperties[0] article.AdditionalProperties[0] "Metadata is not copied"
        Suspect.sequenceEqual agent.Emails [ "ada@example.org"; "ada@example.org" ] "Email order and duplicates retained"
        Suspect.sequenceEqual article.Identifiers [ "doi:1"; "pmid:2" ] "All article identifiers retained"
        Expect.equal agent.GivenName (Some "Ada") "Given name retained"
        Expect.equal agent.FamilyName (Some "Lovelace") "Family name retained"
        Expect.equal role.TAN (Some "ROLE:1") "TAN retained"
        Expect.equal role.Identifier (Some "role:1") "Term identifier retained"
        Expect.equal organization.Url (Some "https://example.org") "Organization URL retained"
        Expect.equal dataset.Title (Some "Study") "Title retained"
        Expect.equal dataset.Description (Some "Description") "Description retained"
        Expect.equal dataset.License (Some "custom license") "License remains open text"
        Expect.equal dataset.DatePublished (Some "2026") "Partial dates are not parsed or normalized"
        Expect.equal dataset.DateCreated (Some "yesterday") "Dates remain supplied strings"
        Expect.equal dataset.DateModified (Some "today") "Dates remain supplied strings"
]

let private mapping = testList "transpilable explicit mappings" [
    testCase "numeric alternatives support wildcard and reversed match forms" <| fun _ ->
        for value in [ AnnotationValue.Number 0.0; AnnotationValue.Number 23.5 ] do
            Expect.isTrue (MappingProbe.IsNumber(value)) "Number plus wildcard selects Number"
            Expect.isFalse (MappingProbe.IsText(value)) "Text plus wildcard rejects Number"
            Expect.equal (MappingProbe.ClassifyNumberFirst(value)) "number" "Number-first explicit match"
        for value in [ AnnotationValue.Text "42"; AnnotationValue.Text "" ] do
            Expect.isFalse (MappingProbe.IsNumber(value)) "Number plus wildcard rejects Text"
            Expect.isTrue (MappingProbe.IsText(value)) "Text plus wildcard selects Text"
            Expect.equal (MappingProbe.ClassifyNumberFirst(value)) "text" "Number-first match preserves Text"
        let annotation = MappingProbe.CreateNumericAnnotation()
        Expect.equal (MappingProbe.WriteAnnotation(annotation.Value).Number) (Some 23.5) "F#-created numeric values map to numeric columns"

    testCase "annotation columns distinguish text numbers zero and absence" <| fun _ ->
        for value in [ AnnotationValue.Text "42"; AnnotationValue.Text ""; AnnotationValue.Number 42.0; AnnotationValue.Number 0.0 ] do
            let row = MappingProbe.WriteAnnotation(Some value)
            match value, MappingProbe.ReadAnnotation(row) with
            | AnnotationValue.Text expected, Some(AnnotationValue.Text actual) ->
                Expect.equal actual expected "Text round trip"
                Expect.isNone row.Number "Only text column selected"
            | AnnotationValue.Number expected, Some(AnnotationValue.Number actual) ->
                Expect.equal actual expected "Number round trip"
                Expect.isNone row.Text "Only number column selected"
            | _ -> failwith "Annotation alternative changed during mapping"
        Expect.isNone (MappingProbe.ReadAnnotation(MappingProbe.WriteAnnotation(None))) "Absent annotation round trip"
        Expect.throws (fun () -> MappingProbe.ReadAnnotation(AnnotationColumns(text = "42", number = 42.0)) |> ignore) "Conflicting columns rejected"

    testCase "entity columns preserve selected entity and shared reference" <| fun _ ->
        let sample = Sample("sample")
        let data = Data("data.csv")
        let sampleRow = MappingProbe.WriteEntity(Some(EntityReference.Sample sample))
        let dataRow = MappingProbe.WriteEntity(Some(EntityReference.Data data))
        Expect.isNone sampleRow.Data "Sample selects only sample column"
        Expect.isNone dataRow.Sample "Data selects only data column"
        same (sampleOf (MappingProbe.ReadEntity(sampleRow).Value)) sample "Sample round trip preserves identity"
        same (dataOf (MappingProbe.ReadEntity(dataRow).Value)) data "Data round trip preserves identity"
        Expect.isNone (MappingProbe.ReadEntity(MappingProbe.WriteEntity(None))) "Absent endpoint round trip"
        Expect.throws (fun () -> MappingProbe.ReadEntity(EntityColumns(sample = sample, data = data)) |> ignore) "Conflicting references rejected"

    testCase "intended use columns map text and term objects" <| fun _ ->
        let term = DefinedTerm("measurement")
        let textRow = MappingProbe.WriteIntendedUse(Some(RecipeIntendedUse.Text ""))
        let termRow = MappingProbe.WriteIntendedUse(Some(RecipeIntendedUse.Term term))
        Expect.isNone textRow.Term "Text selects only text column"
        Expect.isNone termRow.Text "Term selects only term column"
        match MappingProbe.ReadIntendedUse(textRow) with
        | Some(RecipeIntendedUse.Text value) -> Expect.equal value "" "Empty text is not absent"
        | _ -> failwith "Expected text alternative"
        match MappingProbe.ReadIntendedUse(termRow) with
        | Some(RecipeIntendedUse.Term value) -> same value term "Term round trip preserves identity"
        | _ -> failwith "Expected term alternative"
        Expect.isNone (MappingProbe.ReadIntendedUse(MappingProbe.WriteIntendedUse(None))) "Absent intended use"
        Expect.throws (fun () -> MappingProbe.ReadIntendedUse(IntendedUseColumns(text = "text", term = term)) |> ignore) "Conflicting intended-use columns"

    testCase "term-set columns map URL strings and term-set objects" <| fun _ ->
        let termSet = DefinedTermSet("ontology", identifier = "https://example.org/ontology")
        let term = DefinedTerm("term", inDefinedTermSet = DefinedTermSetReference.TermSet termSet)
        let termSetRow = MappingProbe.WriteTermSet(term.InDefinedTermSet)
        Expect.isNone termSetRow.Url "Term set selects only reference column"
        match MappingProbe.ReadTermSet(termSetRow) with
        | Some(DefinedTermSetReference.TermSet value) -> same value termSet "Term-set identity preserved"
        | _ -> failwith "Expected term-set alternative"
        term.InDefinedTermSet <- Some(DefinedTermSetReference.Url "https://example.org/ontology")
        let urlRow = MappingProbe.WriteTermSet(term.InDefinedTermSet)
        Expect.isNone urlRow.TermSet "URL selects only text column"
        match MappingProbe.ReadTermSet(urlRow) with
        | Some(DefinedTermSetReference.Url value) -> Expect.equal value "https://example.org/ontology" "URL preserved"
        | _ -> failwith "Expected URL alternative"
        term.InDefinedTermSet <- None
        Expect.isNone (MappingProbe.ReadTermSet(MappingProbe.WriteTermSet(term.InDefinedTermSet))) "Optional relationship cleared"
        Expect.throws (fun () -> MappingProbe.ReadTermSet(TermSetColumns(url = "url", termSet = termSet)) |> ignore) "Conflicting term-set columns"
]

let private extensions = testList "extensions" [
    testCase "dictionary properties and inherited identity" <| fun _ ->
        let sample = Sample("leaf")
        let target = EntityObject("QualityAssessment")
        sample.SetEntityProperty("quality", Entity.Object target)
        sample.SetEntityProperty("zero", Entity.Number 0.)
        sample.SetEntityProperty("quality", Entity.Object target)
        target.SetEntityProperty("back", Entity.Object sample)
        Expect.equal (sample.EntityProperties.Keys |> Seq.sort |> Seq.toArray) [|"quality"; "zero"|] "Replacement preserves key membership"
        let keys = sample.EntityProperties.Keys
        keys.Clear()
        Expect.isTrue (sample.EntityProperties.Contains("quality")) "Key snapshot cannot mutate bag"
        match sample.EntityProperties.Get("quality") with
        | Entity.Object value -> same value target "Preserves object identity"
        | _ -> failwith "Expected object"
        Expect.isNone target.Id "No inferred ID"
        Expect.throws (fun () -> sample.EntityProperties.Add("name", Entity.Text "other")) "Reserved property"
        Expect.throws (fun () -> sample.SetEntityProperty("type", Entity.Text "other")) "Reserved type"
        Expect.throws (fun () -> sample.AddEntityProperty("zero", Entity.Text "other")) "Duplicate key"
        sample.SetEntityProperty("Name", Entity.Text "case sensitive")
        sample.SetEntityProperty("null", Entity.Null(EntityNull()))
        Expect.isTrue (sample.EntityProperties.Contains("null")) "Explicit null is present"
        Expect.throws (fun () -> sample.EntityProperties.Get("missing") |> ignore) "Missing key"
        Expect.isTrue (sample.RemoveEntityProperty("zero")) "Remove existing"
        Expect.isFalse (sample.RemoveEntityProperty("zero")) "Remove missing"

    testCase "value alternatives and base64" <| fun _ ->
        let obj = EntityObject("Custom")
        let values = EntityCollection([Entity.Object obj; Entity.Object obj])
        Expect.equal values.Count 2 "Duplicates retained"
        Expect.throws (fun () -> values.Get(-1) |> ignore) "Negative index"
        Expect.throws (fun () -> values.Set(2, Entity.Text "outside")) "Out of range index"
        let input = ResizeArray<Entity>([Entity.Object obj])
        let copied = EntityCollection(input)
        input.Clear()
        Expect.equal copied.Count 1 "Input container copied"
        let cases = [Entity.Number 0., "number"; Entity.Text "", "text"; Entity.Bool false, "bool";
                     Entity.Object obj, "object"; Entity.Collection values, "collection";
                     Entity.Null(EntityNull()), "null"; Entity.Blob(EntityBlob("AA==")), "blob"]
        for value, expected in cases do Expect.equal (MappingProbe.ClassifyExtension(value)) expected "Distinct alternative"
        Expect.equal (EntityBlob("").Base64) "" "Empty blob"
        for invalid in ["A"; "!!!!"; "A==="; "AB=="; "AA=A"; "AAA "] do
            Expect.throws (fun () -> EntityBlob(invalid) |> ignore) "Invalid base64"
]

let tests = testList "ARCBaseModel" [ construction; collections; domain; mapping; extensions ]
