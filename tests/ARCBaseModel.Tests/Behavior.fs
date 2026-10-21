module ARCBaseModel.Tests.Behavior

open System
open ARCBaseModel
open ARCBaseModel.Tests
open Fable.Pyxpecto

let private same actual expected message =
    Expect.isTrue (Object.ReferenceEquals(actual, expected)) message

let private messageOf (action: unit -> unit) =
    try
        action ()
        ""
    with error -> error.Message

// The Fable targets have no ICollection check. Their native consumers assert frozen arrays and tuples.
let private expectReadOnly (collection: obj) =
#if !FABLE_COMPILER
    let check (typed: System.Collections.Generic.ICollection<'T>) (extra: 'T) =
        Expect.isTrue typed.IsReadOnly "Collections are read-only"
        Expect.throws (fun () -> typed.Add(extra)) "Add on a read-only collection"
        match box typed with
        | :? System.Collections.Generic.IList<'T> as indexed ->
            Expect.isFalse (box typed :? Array) "An array allows element assignment"
            Expect.throws (fun () -> indexed.[0] <- extra) "Indexed set on a read-only list"
        | _ -> ()
    match collection with
    | :? System.Collections.Generic.ICollection<Annotation> as typed -> check typed (Annotation("extra"))
    | :? System.Collections.Generic.ICollection<string> as typed -> check typed "extra"
    | _ -> failwithf "Unrecognized collection type %s" (collection.GetType().FullName)
#else
    ignore collection
#endif

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

    testCase "dataset copies apply the constructor validation and leave the source unchanged" <| fun _ ->
        let dataset = Dataset([ "administrative" ], [ "id" ])
        let constructorProfiles = messageOf (fun () -> Dataset([], [ "id" ]) |> ignore)
        let constructorUnrecognized = messageOf (fun () -> Dataset([ "other" ], [ "id" ]) |> ignore)
        let constructorIdentifiers = messageOf (fun () -> Dataset([ "administrative" ], []) |> ignore)
        Expect.notEqual constructorProfiles "" "The constructor rejects empty profiles"
        Expect.equal (messageOf (fun () -> Dataset.withConformsTo(dataset, []) |> ignore)) constructorProfiles "Same message for empty profiles"
        Expect.equal (messageOf (fun () -> Dataset.withConformsTo(dataset, [ "other" ]) |> ignore)) constructorUnrecognized "Same message for unrecognized profiles"
        Expect.equal (messageOf (fun () -> Dataset.withIdentifiers(dataset, []) |> ignore)) constructorIdentifiers "Same message for empty identifiers"
        let copy = Dataset.withConformsTo(dataset, [ "process-provenance"; "custom" ])
        Suspect.sequenceEqual copy.ConformsTo [ "process-provenance"; "custom" ] "A valid replacement is accepted"
        Suspect.sequenceEqual dataset.ConformsTo [ "administrative" ] "The source keeps its profiles"
        same copy.Identifiers dataset.Identifiers "Untouched collections are shared"
]

let private collections = testList "collections and identity" [
    testCase "constructor containers are copied, references and repeated entries are retained" <| fun _ ->
        let first = Annotation("first")
        let second = Annotation("second")
        let supplied = ResizeArray([ first; second; first ])
        let sample = Sample("sample", additionalProperties = supplied)
        supplied.Clear()
        Expect.equal (Seq.length sample.AdditionalProperties) 3 "Stored collection is independent of constructor input"
        same (Seq.item 0 sample.AdditionalProperties) first "First annotation is not copied"
        same (Seq.item 1 sample.AdditionalProperties) second "Ordering is preserved"
        same (Seq.item 2 sample.AdditionalProperties) first "Repeated annotation reference is preserved"
        let renamed = Annotation.withName(first, "changed")
        Expect.equal renamed.Name "changed" "The copy carries the new name"
        Expect.equal (Seq.item 2 sample.AdditionalProperties).Name "first" "Copying a referenced object does not change its holders"

    testCase "collection copies replace the container and getters are read-only" <| fun _ ->
        let sample = Sample("sample")
        let annotation = Annotation("metadata")
        let replacement = ResizeArray([ annotation; annotation ])
        let updated = Sample.withAdditionalProperties(sample, replacement)
        replacement.Add(Annotation("external"))
        Expect.equal (Seq.length updated.AdditionalProperties) 2 "The copy consumed the supplied container"
        same (Seq.item 1 updated.AdditionalProperties) annotation "The copy kept duplicate entity references"
        Expect.equal (Seq.length sample.AdditionalProperties) 0 "The source is unchanged"
        expectReadOnly (box updated.AdditionalProperties)
        let appended = Sample.withAdditionalProperties(updated, Seq.append updated.AdditionalProperties [ annotation ])
        Expect.equal (Seq.length appended.AdditionalProperties) 3 "An appended copy has the extra element"
        Expect.equal (Seq.length updated.AdditionalProperties) 2 "The previous collection is unchanged"
        let renamed = Sample.withName(updated, "renamed")
        same renamed.AdditionalProperties updated.AdditionalProperties "Untouched collections are shared"
        Expect.equal sample.Name "sample" "Name copy leaves the source unchanged"

    testCase "empty defaults and string collection containers are independent" <| fun _ ->
        let first = Recipe()
        let second = Recipe()
        let extended = Recipe.withComponents(first, [ Annotation("instrument") ])
        let typed = EntityObject.withAdditionalTypes(first, [ "custom" ])
        Expect.equal (Seq.length first.Components) 0 "Copies leave the source collections empty"
        Expect.equal (Seq.length second.Components) 0 "Each instance has its own default collections"
        Expect.equal (Seq.length extended.AdditionalProperties) 0 "Different collection properties do not share storage"
        Expect.equal (Seq.length second.AdditionalTypes) 0 "Classification defaults are independent"
        Expect.isTrue (box typed :? Recipe) "withAdditionalTypes returns the concrete class"
        Expect.equal (Seq.length typed.AdditionalTypes) 1 "The copy has the classification"
        expectReadOnly (box typed.AdditionalTypes)
        let supplied = ResizeArray([ "a"; "a"; "b" ])
        let retyped = EntityObject.withAdditionalTypes(second, supplied)
        supplied.Clear()
        Suspect.sequenceEqual retyped.AdditionalTypes [ "a"; "a"; "b" ] "String collections preserve order and duplicates"
        Expect.equal (Seq.length second.AdditionalTypes) 0 "The source classifications are unchanged"

    testCase "distinct same-ID and same-field instances remain distinct" <| fun _ ->
        let first = Sample("same", id = "sample:1")
        let second = Sample("same", id = "sample:1")
        Expect.isFalse (Object.ReferenceEquals(first, second)) "No interning or ID canonicalization"
        Expect.isFalse (first.Equals(second)) "No domain equality by fields or IDs"
        let descriptor1 = Descriptor(EntityReference.Sample first)
        let descriptor2 = Descriptor(EntityReference.Sample second)
        let dataset = Dataset([ "semantic-designation" ], [ "id" ], descriptors = [ descriptor1; descriptor2; descriptor1 ])
        same (sampleOf (Seq.item 0 dataset.Descriptors).Describes) first "First object is retained"
        same (sampleOf (Seq.item 1 dataset.Descriptors).Describes) second "Second object is not merged by Id"
        same (Seq.item 2 dataset.Descriptors) descriptor1 "Duplicate descriptors survive"

    testCase "optional IDs are assigned and cleared on copies without identity side effects" <| fun _ ->
        let sample = Sample("sample")
        let descriptor = Descriptor(EntityReference.Sample sample)
        let assigned = MappingProbe.AssignSampleId(sample, Some "sql:1")
        Expect.isFalse (Object.ReferenceEquals(assigned, sample)) "ID assignment returns a new object"
        Expect.equal (MappingProbe.ReadSampleId(assigned)) (Some "sql:1") "Layer 2 can explicitly use the domain Id"
        Expect.isNone sample.Id "The source keeps its absent Id"
        same (sampleOf descriptor.Describes) sample "Existing references keep pointing at the source"
        let generic = EntityObject.withId(assigned, Some "other")
        Expect.isTrue (box generic :? Sample) "withId returns the same concrete class"
        Expect.equal generic.Id (Some "other") "The replacement Id is set"
        Expect.equal assigned.Id (Some "sql:1") "The source keeps its Id"
        Expect.equal generic.Name "sample" "Other fields are carried over"
        Expect.isNone (EntityObject.withId(assigned, None)).Id "IDs remain clearable"
]

let private domain = testList "linked model" [
    testCase "annotation alternatives and optional values preserve zero and empty text" <| fun _ ->
        let annotation = Annotation("temperature")
        Expect.isNone annotation.Value "Initially absent"
        let zero = Annotation.withValue(annotation, Some(AnnotationValue.Number 0.0))
        match zero.Value with
        | Some(AnnotationValue.Number value) -> Expect.equal value 0.0 "Zero is a value"
        | _ -> failwith "Numeric zero was lost"
        let empty = Annotation.withValue(zero, Some(AnnotationValue.Text ""))
        match empty.Value with
        | Some(AnnotationValue.Text value) -> Expect.equal value "" "Empty text is a value"
        | _ -> failwith "Empty text was lost"
        Expect.isTrue (MappingProbe.IsNumber(zero.Value.Value)) "The earlier copy keeps its number"
        let blankUnit = Annotation.withUnit(empty, Some "")
        Expect.equal blankUnit.Unit (Some "") "Empty optional string is distinct from None"
        let cleared = Annotation.withUnit(Annotation.withValue(blankUnit, None), None)
        Expect.isNone cleared.Value "Value cleared"
        Expect.isNone cleared.Unit "Unit cleared"
        Expect.equal blankUnit.Unit (Some "") "Clearing does not touch the source"
        Expect.isNone annotation.Value "The original is still absent"

    testCase "annotations and formal parameters link to the exact supplied objects" <| fun _ ->
        let parameter = FormalParameter(name = "temperature", nameTAN = "https://example.org/temperature")
        let annotation = Annotation("temperature", value = AnnotationValue.Number 23.5, unit = "C", nameTAN = "key", valueTAN = "value", unitTAN = "unit", instanceOf = parameter)
        let linked = FormalParameter.withDefaultValue(parameter, Some annotation)
        same linked.DefaultValue.Value annotation "DefaultValue retains annotation identity"
        same annotation.InstanceOf.Value parameter "InstanceOf retains parameter identity"
        Expect.isNone parameter.DefaultValue "The source parameter has no default"
        let relinked = Annotation.withInstanceOf(annotation, Some linked)
        same relinked.InstanceOf.Value linked "withInstanceOf links the supplied parameter"
        same annotation.InstanceOf.Value parameter "The source annotation keeps its parameter"
        let renamed = Annotation.withName(annotation, "renamed")
        same linked.DefaultValue.Value annotation "A copy of a referenced object does not change the referencing object"
        Expect.equal renamed.Name "renamed" "The copy has the new name"
        Expect.equal annotation.NameTAN (Some "key") "Key TAN retained"
        Expect.equal annotation.ValueTAN (Some "value") "Value TAN retained"
        Expect.equal annotation.UnitTAN (Some "unit") "Unit TAN retained"
        Expect.equal parameter.NameTAN (Some "https://example.org/temperature") "Parameter TAN retained"
        Expect.isNone (FormalParameter.withDefaultValue(linked, None)).DefaultValue "Optional default clears"
        same linked.DefaultValue.Value annotation "Clearing on a copy leaves the source link"

    testCase "one Dataset combines profiles with independently profiled recursive children" <| fun _ ->
        let sample = Sample("sample")
        let data = Data("data.csv")
        let transformation = Process("measure", input = EntityReference.Sample sample, output = EntityReference.Data data)
        let descriptor = Descriptor(EntityReference.Data data, annotations = [ Annotation("quality", value = AnnotationValue.Text "good") ])
        let grandchild = Dataset([ "administrative" ], [ "grandchild" ])
        let child = Dataset([ "semantic-designation" ], [ "child" ], descriptors = [ descriptor ], hasParts = [ grandchild ])
        let parent = Dataset([ "administrative"; "process-provenance" ], [ "parent" ], processes = [ transformation ], dataFiles = [ data ], hasParts = [ child ])
        Suspect.sequenceEqual (Seq.item 0 parent.HasParts).ConformsTo [ "semantic-designation" ] "Child did not inherit parent profiles"
        Suspect.sequenceEqual (Seq.item 0 (Seq.item 0 parent.HasParts).HasParts).ConformsTo [ "administrative" ] "Mixed profiles nest at arbitrary depths"
        same (dataOf (Seq.item 0 parent.Processes).Output.Value) (Seq.item 0 parent.DataFiles) "Process and Dataset share the same Data"
        same (dataOf (Seq.item 0 child.Descriptors).Describes) data "Cross-profile reference retains identity"
        Expect.equal (Seq.length parent.Descriptors) 0 "Descriptor is not automatically registered in ancestors"

    testCase "Data fragments retain their own paths and selectors" <| fun _ ->
        let deepest = Data("other.csv", selector = "row=7", selectorFormat = "https://www.rfc-editor.org/rfc/rfc7111", encodingFormat = "text/csv")
        let fragment = Data("data.csv", selector = "row=2", hasParts = [ deepest ])
        let file = Data("data.csv", hasParts = [ fragment; fragment ])
        same (Seq.item 0 file.HasParts) fragment "Fragments preserve identity"
        same (Seq.item 1 file.HasParts) fragment "Repeated fragments preserve duplicate references"
        Expect.equal (Seq.item 0 (Seq.item 0 file.HasParts).HasParts).Path "other.csv" "Nested paths are never inferred from parents"
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
        let forward = Process.withOutput(Process.withInput(transformation, Some(EntityReference.Sample sample)), Some(EntityReference.Data data))
        same (sampleOf forward.Input.Value) sample "Sample input"
        same (dataOf forward.Output.Value) data "Data output"
        Expect.isNone transformation.Input "The source input stays absent"
        let swapped = Process.withOutput(Process.withInput(forward, Some(EntityReference.Data data)), Some(EntityReference.Sample sample))
        same (dataOf swapped.Input.Value) data "Endpoint alternative can change"
        same (sampleOf swapped.Output.Value) sample "Either endpoint accepts either type"
        same (sampleOf forward.Input.Value) sample "The earlier copy keeps its alternative"
        let cleared = Process.withOutput(Process.withInput(swapped, None), None)
        Expect.isNone cleared.Input "Input cleared"
        Expect.isNone cleared.Output "Output cleared"
        Expect.isSome swapped.Input "Clearing leaves the source copy"

    testCase "recipe parameters and components retain shared annotations" <| fun _ ->
        let parameter = FormalParameter(name = "temperature")
        let annotation = Annotation("temperature", value = AnnotationValue.Number 42.0, instanceOf = parameter)
        let term = DefinedTerm("measurement")
        let recipe = Recipe(name = "measure", description = "measurement protocol", intendedUse = RecipeIntendedUse.Term term, parameters = [ parameter; parameter ], components = [ annotation ], additionalProperties = [ annotation ], version = "v1", url = "protocol.txt")
        let transformation = Process("measurement", executesRecipe = recipe, parameterValues = [ annotation; annotation ])
        same transformation.ExecutesRecipe.Value recipe "Recipe reference retained"
        same (Seq.item 0 recipe.Parameters) parameter "Parameters retained"
        same (Seq.item 1 recipe.Parameters) parameter "Duplicate parameter retained"
        same (Seq.item 0 recipe.Components) (Seq.item 1 transformation.ParameterValues) "Annotations shared across relationships"
        same (Seq.item 0 recipe.AdditionalProperties) annotation "Metadata reference retained"
        Expect.equal recipe.Description (Some "measurement protocol") "Description preserved"
        Expect.equal recipe.Version (Some "v1") "Version preserved"
        Expect.equal recipe.Url (Some "protocol.txt") "URLs are not normalized"
        let custom = Recipe.withIntendedUse(recipe, Some(RecipeIntendedUse.Text "custom"))
        match custom.IntendedUse with
        | Some(RecipeIntendedUse.Text text) -> Expect.equal text "custom" "Text alternative is available"
        | _ -> failwith "Expected textual intended use"
        same custom.Parameters recipe.Parameters "Untouched collections are shared"
        match recipe.IntendedUse with
        | Some(RecipeIntendedUse.Term original) -> same original term "The source keeps its term"
        | _ -> failwith "The source intended use changed"
        Expect.isNone (Recipe.withIntendedUse(custom, None)).IntendedUse "Intended use cleared"

    testCase "administrative entities preserve affiliations authors metadata and dates" <| fun _ ->
        let organization = Organization("Lab", url = "https://example.org")
        let role = DefinedTerm("researcher", identifier = "role:1", tan = "ROLE:1")
        let property = Annotation("note", value = AnnotationValue.Text "supplied")
        let agent = Agent("Ada Lovelace", givenName = "Ada", familyName = "Lovelace", emails = [ "ada@example.org"; "ada@example.org" ], affiliations = [ organization; organization ], identifiers = [ "orcid:1" ], jobTitles = [ role ], additionalProperties = [ property ])
        let status = DefinedTerm("published")
        let article = ScholarlyArticle("A study", authors = [ agent; agent ], identifiers = [ "doi:1"; "pmid:2" ], creativeWorkStatus = status, additionalProperties = [ property ])
        let dataset = Dataset([ "administrative" ], [ "accession:1" ], title = "Study", description = "Description", license = "custom license", datePublished = "2026", dateCreated = "yesterday", dateModified = "today", agents = [ agent ], citations = [ article ], additionalProperties = [ property ])
        same (Seq.item 0 dataset.Agents) (Seq.item 0 article.Authors) "Dataset agent and author share one object"
        same (Seq.item 1 article.Authors) agent "Duplicate authors retained"
        same (Seq.item 1 agent.Affiliations) organization "Duplicate affiliations retained"
        same (Seq.item 0 agent.JobTitles) role "Role is a term reference"
        same article.CreativeWorkStatus.Value status "Publication status remains a reference"
        same (Seq.item 0 dataset.AdditionalProperties) (Seq.item 0 article.AdditionalProperties) "Metadata is not copied"
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
        let urlTerm = DefinedTerm.withInDefinedTermSet(term, Some(DefinedTermSetReference.Url "https://example.org/ontology"))
        let urlRow = MappingProbe.WriteTermSet(urlTerm.InDefinedTermSet)
        Expect.isNone urlRow.TermSet "URL selects only text column"
        match MappingProbe.ReadTermSet(urlRow) with
        | Some(DefinedTermSetReference.Url value) -> Expect.equal value "https://example.org/ontology" "URL preserved"
        | _ -> failwith "Expected URL alternative"
        let clearedTerm = DefinedTerm.withInDefinedTermSet(urlTerm, None)
        Expect.isNone (MappingProbe.ReadTermSet(MappingProbe.WriteTermSet(clearedTerm.InDefinedTermSet))) "Optional relationship cleared"
        Expect.isSome term.InDefinedTermSet "The source term keeps its term set"
        Expect.throws (fun () -> MappingProbe.ReadTermSet(TermSetColumns(url = "url", termSet = termSet)) |> ignore) "Conflicting term-set columns"
]

let private extensions = testList "extensions" [
    testCase "extension property copies preserve alternatives and reject reserved names" <| fun _ ->
        let sample = Sample("leaf")
        let target = EntityObject("QualityAssessment")
        let values = EntityCollection([ Entity.Number 1.0 ])
        let alternatives = [
            "object", Entity.Object target
            "collection", Entity.Collection values
            "number", Entity.Number 0.
            "text", Entity.Text ""
            "bool", Entity.Bool false
            "null", Entity.Null(EntityNull())
            "blob", Entity.Blob(EntityBlob("AA=="))
        ]
        let filled = alternatives |> List.fold (fun (current: Sample) (key, value) -> EntityObject.withProperty(current, key, value)) sample
        Expect.isTrue (box filled :? Sample) "withProperty returns the same concrete class"
        Expect.equal (filled.EntityProperties.Keys |> Seq.sort |> Seq.toArray) [| "blob"; "bool"; "collection"; "null"; "number"; "object"; "text" |] "Every alternative is stored"
        match filled.EntityProperties.Get("object"), filled.EntityProperties.Get("collection") with
        | Entity.Object value, Entity.Collection collection ->
            same value target "Preserves object identity"
            same collection values "Preserves collection identity"
        | _ -> failwith "Expected object and collection"
        Expect.isTrue (filled.EntityProperties.Contains("null")) "Explicit null is present"
        Expect.isFalse (filled.EntityProperties.Contains("missing")) "Absent key"
        Expect.throws (fun () -> filled.EntityProperties.Get("missing") |> ignore) "Missing key"
        Expect.equal (sample.EntityProperties.Keys |> Seq.length) 0 "The source bag is unchanged"
        Expect.isNone target.Id "No inferred ID"
        let replaced = EntityObject.withProperty(filled, "number", Entity.Text "now text")
        Expect.equal (MappingProbe.ClassifyExtension(filled.EntityProperties.Get("number"))) "number" "The earlier copy keeps the old value"
        Expect.equal (MappingProbe.ClassifyExtension(replaced.EntityProperties.Get("number"))) "text" "Replacement changes the alternative"
        Expect.equal (replaced.EntityProperties.Keys |> Seq.length) 7 "Replacement preserves key membership"
        let removed = EntityObject.withoutProperty(replaced, "number")
        Expect.isFalse (removed.EntityProperties.Contains("number")) "Removal drops the key"
        Expect.isTrue (replaced.EntityProperties.Contains("number")) "Removal leaves the source"
        Expect.isTrue (box removed :? Sample) "withoutProperty returns the same concrete class"
        for reserved in [ "type"; "id"; "additionalTypes"; "name" ] do
            Expect.throws (fun () -> EntityObject.withProperty(sample, reserved, Entity.Text "other") |> ignore) "Reserved property"
        Expect.isTrue (EntityObject.withProperty(sample, "Name", Entity.Text "case sensitive").EntityProperties.Contains("Name")) "Names are case-sensitive"
        expectReadOnly (box filled.EntityProperties.Keys)

    testCase "value alternatives, immutable collections and base64" <| fun _ ->
        let obj = EntityObject("Custom")
        let values = EntityCollection([Entity.Object obj; Entity.Object obj])
        Expect.equal values.Count 2 "Duplicates retained"
        match values.Get(1) with
        | Entity.Object value -> same value obj "Elements keep their identity"
        | _ -> failwith "Expected object"
        Expect.throws (fun () -> values.Get(-1) |> ignore) "Negative index"
        Expect.throws (fun () -> values.Get(2) |> ignore) "Out of range index"
        let input = ResizeArray<Entity>([Entity.Object obj])
        let copied = EntityCollection(input)
        input.Clear()
        Expect.equal copied.Count 1 "Input container copied"
        let enumerations = ref 0
        let counted = Seq.map (fun value -> enumerations.Value <- enumerations.Value + 1; value) [ Entity.Text "a"; Entity.Text "b" ]
        let consumed = EntityCollection(counted)
        Expect.equal consumed.Count 2 "Both elements are read"
        Expect.equal enumerations.Value 2 "The input sequence is consumed once"
        Expect.equal (MappingProbe.ClassifyExtension(consumed.Get(0))) "text" "Elements are kept in order"
        let cases = [Entity.Number 0., "number"; Entity.Text "", "text"; Entity.Bool false, "bool";
                     Entity.Object obj, "object"; Entity.Collection values, "collection";
                     Entity.Null(EntityNull()), "null"; Entity.Blob(EntityBlob("AA==")), "blob"]
        for value, expected in cases do Expect.equal (MappingProbe.ClassifyExtension(value)) expected "Distinct alternative"
        Expect.equal (EntityBlob("").Base64) "" "Empty blob"
        for invalid in ["A"; "!!!!"; "A==="; "AB=="; "AA=A"; "AAA "] do
            Expect.throws (fun () -> EntityBlob(invalid) |> ignore) "Invalid base64"
]

let private names (items: seq<Annotation>) = items |> Seq.map (fun item -> item.Name) |> List.ofSeq

// Each row checks that the copy has the new value and the original keeps the old one.
let private check (get: 'a -> 'b) (original: 'a) (copy: 'a) (oldValue: 'b) (newValue: 'b) =
    Expect.equal (get copy) newValue "The copy has the new value"
    Expect.equal (get original) oldValue "The original keeps the old value"

let private copies = testList "typed copies" [
    testCase "typed copy methods replace one property and leave the original unchanged" <| fun _ ->
        let before = [ Annotation("before") ]
        let after = [ Annotation("after") ]
        let organization = Organization("lab")
        check (fun (x: Organization) -> x.Name) organization (Organization.withName(organization, "lab2")) "lab" "lab2"
        let agent = Agent("a", givenName = "g", familyName = "f", emails = [ "e" ], identifiers = [ "i" ], additionalProperties = before)
        check (fun (x: Agent) -> x.Name) agent (Agent.withName(agent, "b")) "a" "b"
        check (fun (x: Agent) -> x.GivenName) agent (Agent.withGivenName(agent, Some "g2")) (Some "g") (Some "g2")
        check (fun (x: Agent) -> x.FamilyName) agent (Agent.withFamilyName(agent, Some "f2")) (Some "f") (Some "f2")
        check (fun (x: Agent) -> List.ofSeq x.Emails) agent (Agent.withEmails(agent, [ "e2"; "e2" ])) [ "e" ] [ "e2"; "e2" ]
        check (fun (x: Agent) -> List.ofSeq x.Identifiers) agent (Agent.withIdentifiers(agent, [ "i2" ])) [ "i" ] [ "i2" ]
        check (fun (x: Agent) -> names x.AdditionalProperties) agent (Agent.withAdditionalProperties(agent, after)) [ "before" ] [ "after" ]
        let article = ScholarlyArticle("h", identifiers = [ "i" ], additionalProperties = before)
        check (fun (x: ScholarlyArticle) -> x.Headline) article (ScholarlyArticle.withHeadline(article, "h2")) "h" "h2"
        check (fun (x: ScholarlyArticle) -> List.ofSeq x.Identifiers) article (ScholarlyArticle.withIdentifiers(article, [ "i2" ])) [ "i" ] [ "i2" ]
        check (fun (x: ScholarlyArticle) -> names x.AdditionalProperties) article (ScholarlyArticle.withAdditionalProperties(article, after)) [ "before" ] [ "after" ]
        let annotation = Annotation("n", nameTAN = "n1", unitTAN = "u1")
        check (fun (x: Annotation) -> x.NameTAN) annotation (Annotation.withNameTAN(annotation, Some "n2")) (Some "n1") (Some "n2")
        check (fun (x: Annotation) -> x.UnitTAN) annotation (Annotation.withUnitTAN(annotation, Some "u2")) (Some "u1") (Some "u2")
        let parameter = FormalParameter(nameTAN = "n1")
        check (fun (x: FormalParameter) -> x.NameTAN) parameter (FormalParameter.withNameTAN(parameter, Some "n2")) (Some "n1") (Some "n2")
        let dataset = Dataset([ "administrative" ], [ "id" ], license = "l1", datePublished = "p1", dateCreated = "c1", dateModified = "m1", additionalProperties = before)
        check (fun (x: Dataset) -> x.License) dataset (Dataset.withLicense(dataset, Some "l2")) (Some "l1") (Some "l2")
        check (fun (x: Dataset) -> x.DatePublished) dataset (Dataset.withDatePublished(dataset, Some "p2")) (Some "p1") (Some "p2")
        check (fun (x: Dataset) -> x.DateCreated) dataset (Dataset.withDateCreated(dataset, Some "c2")) (Some "c1") (Some "c2")
        check (fun (x: Dataset) -> x.DateModified) dataset (Dataset.withDateModified(dataset, Some "m2")) (Some "m1") (Some "m2")
        check (fun (x: Dataset) -> names x.AdditionalProperties) dataset (Dataset.withAdditionalProperties(dataset, after)) [ "before" ] [ "after" ]
        let termSet = DefinedTermSet("s", identifier = "i1")
        check (fun (x: DefinedTermSet) -> x.Name) termSet (DefinedTermSet.withName(termSet, "s2")) "s" "s2"
        check (fun (x: DefinedTermSet) -> x.Identifier) termSet (DefinedTermSet.withIdentifier(termSet, Some "i2")) (Some "i1") (Some "i2")
        let term = DefinedTerm("t", identifier = "i1")
        check (fun (x: DefinedTerm) -> x.Name) term (DefinedTerm.withName(term, "t2")) "t" "t2"
        check (fun (x: DefinedTerm) -> x.Identifier) term (DefinedTerm.withIdentifier(term, Some "i2")) (Some "i1") (Some "i2")
        let data = Data("p1", selectorFormat = "s1", encodingFormat = "e1", additionalProperties = before)
        check (fun (x: Data) -> x.Path) data (Data.withPath(data, "p2")) "p1" "p2"
        check (fun (x: Data) -> x.SelectorFormat) data (Data.withSelectorFormat(data, Some "s2")) (Some "s1") (Some "s2")
        check (fun (x: Data) -> x.EncodingFormat) data (Data.withEncodingFormat(data, Some "e2")) (Some "e1") (Some "e2")
        check (fun (x: Data) -> names x.AdditionalProperties) data (Data.withAdditionalProperties(data, after)) [ "before" ] [ "after" ]
        let transformation = Process("n")
        check (fun (x: Process) -> x.Name) transformation (Process.withName(transformation, "n2")) "n" "n2"
        let recipe = Recipe(name = "n", description = "d", additionalProperties = before, version = "v1", url = "u1")
        check (fun (x: Recipe) -> x.Name) recipe (Recipe.withName(recipe, Some "n2")) (Some "n") (Some "n2")
        check (fun (x: Recipe) -> x.Description) recipe (Recipe.withDescription(recipe, Some "d2")) (Some "d") (Some "d2")
        check (fun (x: Recipe) -> names x.AdditionalProperties) recipe (Recipe.withAdditionalProperties(recipe, after)) [ "before" ] [ "after" ]
        check (fun (x: Recipe) -> x.Version) recipe (Recipe.withVersion(recipe, Some "v2")) (Some "v1") (Some "v2")
        check (fun (x: Recipe) -> x.Url) recipe (Recipe.withUrl(recipe, Some "u2")) (Some "u1") (Some "u2")

    testCase "typed copy methods reject null replacements and receivers of another class" <| fun _ ->
        let sample = Sample("sample")
        Expect.throws (fun () -> Sample.withName(sample, null) |> ignore) "Required scalar"
        Expect.throws (fun () -> Sample.withAdditionalProperties(sample, Unchecked.defaultof<seq<Annotation>>) |> ignore) "Collection"
        let descriptor = Descriptor(EntityReference.Sample sample)
        Expect.throws (fun () -> Descriptor.withDescribes(descriptor, Unchecked.defaultof<EntityReference>) |> ignore) "Descriptor target"
        Expect.throws (fun () -> Sample.withName(Unchecked.defaultof<Sample>, "n") |> ignore) "Null receiver"
        // On .NET the unbox itself fails. The Fable targets reach the receiver check in the copy.
        Expect.throws (fun () -> Sample.withName(unbox<Sample> (box (Data("p"))), "n") |> ignore) "Receiver of another class"
]

let tests = testList "ARCBaseModel" [ construction; collections; domain; mapping; extensions; copies ]
