module ARCManagement.Tests.FullModel

open System
open ARCBaseModel
open ARCManagement
open Fable.Pyxpecto

let private same a b = Expect.isTrue (obj.ReferenceEquals(a,b)) "Shared entity reference"
let private rev (g: ManagedGraph) = g.readModel().Revision

let tests = testList "Immutable complete model" [
    testCase "all profiles retain typed values sharing and optional alternatives" (fun _ ->
        let terms = DefinedTermSet("ontology",identifier="https://example.org/terms")
        let term = DefinedTerm("term",tan="T:1",inDefinedTermSet=DefinedTermSetReference.TermSet terms)
        let annotation = Annotation("quantity",value=AnnotationValue.Number 0.125,unit="mg",id="a")
        let parameter = FormalParameter(name="parameter",defaultValue=annotation)
        let recipe = Recipe(name="recipe",parameters=[parameter;parameter],intendedUse=RecipeIntendedUse.Term term,components=[annotation])
        let sample = Sample("sample",additionalProperties=[annotation],id="s")
        let fragment = Data("results.csv",selector="row=1")
        let data = Data("results.csv",hasParts=[fragment;fragment],id="data")
        let process = Process("measurement",input=EntityReference.Sample sample,output=EntityReference.Data data,executesRecipe=recipe,parameterValues=[annotation])
        let descriptor = Descriptor(EntityReference.Sample sample,annotations=[annotation;annotation])
        let organization = Organization("organization",url="https://example.org")
        let agent = Agent("agent",givenName="",emails=["a@example.org";"a@example.org"],affiliations=[organization;organization],jobTitles=[term])
        let article = ScholarlyArticle("article",authors=[agent;agent],creativeWorkStatus=term)
        let source = Dataset(["process-provenance";"administrative";"semantic-designation"],["dataset"],processes=[process;process],dataFiles=[data],descriptors=[descriptor],agents=[agent],citations=[article])
        use session = Session.createInMemory()
        let graph = session.createArc(source)
        let result = graph.Model
        Expect.equal result.ConformsTo.Count 3 "Combined profiles"
        same result.Processes[0] result.Processes[1]
        let restoredProcess = result.Processes[0]
        let restoredSample = match restoredProcess.Input with Some(EntityReference.Sample v) -> v | _ -> failwith "Sample"
        let restoredData = match restoredProcess.Output with Some(EntityReference.Data v) -> v | _ -> failwith "Data"
        same restoredData result.DataFiles[0]
        same restoredData.HasParts[0] restoredData.HasParts[1]
        same result.Descriptors[0].Annotations[0] restoredSample.AdditionalProperties[0]
        Expect.equal restoredSample.AdditionalProperties[0].Value (Some(AnnotationValue.Number 0.125)) "Numeric alternative"
        let restoredRecipe = restoredProcess.ExecutesRecipe.Value
        same restoredRecipe.Parameters[0] restoredRecipe.Parameters[1]
        same restoredRecipe.Parameters[0].DefaultValue.Value restoredSample.AdditionalProperties[0]
        same result.Agents[0] result.Citations[0].Authors[0]
        same result.Citations[0].Authors[0] result.Citations[0].Authors[1]
        same result.Agents[0].Affiliations[0] result.Agents[0].Affiliations[1]
        Expect.equal result.Agents[0].GivenName (Some "") "Present empty text"
        Expect.isNone source.Id "Detached source unchanged")

    testCase "focused native alternatives preserve numbers text and absence" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"]))
        let first = graph.Annotation.add(rev graph,Annotation("value",id="a"))
        let zero = graph.Annotation.setValueNumber(first.Revision,"a",0.0)
        Expect.equal zero.Value.Value (Some(AnnotationValue.Number 0.0)) "Zero is present"
        let text = graph.Annotation.setValueText(zero.Revision,"a","")
        Expect.equal text.Value.Value (Some(AnnotationValue.Text "")) "Empty text is present"
        let cleared = graph.Annotation.clearValue(text.Revision,"a")
        Expect.isNone cleared.Value.Value "Explicit clearing"
        Expect.equal zero.Value.Value (Some(AnnotationValue.Number 0.0)) "Earlier value unchanged"
        Expect.throws (fun () -> graph.Annotation.setValueNumber(cleared.Revision,"a",Double.NaN) |> ignore) "Nonfinite number rejected")

    testCase "focused relationships validate type and return the updated snapshot" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"]))
        graph.Sample.add(rev graph,Sample("sample",id="s")) |> ignore
        graph.Data.add(rev graph,Data("data.csv",id="d")) |> ignore
        let before = graph.Process.add(rev graph,Process("p",id="p"))
        let sample = graph.Process.setInputSample(before.Revision,"p","s")
        match sample.Value.Input with Some(EntityReference.Sample v) -> Expect.equal v.Id (Some "s") "Typed Sample" | _ -> failwith "Expected Sample"
        let data = graph.Process.setOutputData(sample.Revision,"p","d")
        match data.Value.Output with Some(EntityReference.Data v) -> Expect.equal v.Path "data.csv" "Typed Data" | _ -> failwith "Expected Data"
        Expect.isNone before.Value.Input "Old value unchanged"
        Expect.throws (fun () -> graph.Process.setInputSample(data.Revision,"p","d") |> ignore) "Wrong target kind rejected"
        let cleared = graph.Process.clearInput(data.Revision,"p")
        Expect.isNone cleared.Value.Input "Clear optional relationship"
        let descriptor = graph.Descriptor.create(cleared.Revision,"s")
        match descriptor.Value.Describes with EntityReference.Sample v -> Expect.equal v.Id (Some "s") "Required factory target" | _ -> failwith "Expected Sample descriptor"
        Expect.throws (fun () -> graph.Descriptor.create(descriptor.Revision,graph.Model.Id.Value) |> ignore) "Descriptor factory rejects Dataset target")

    testCase "required constructor and copied Dataset invariants agree" (fun _ ->
        let value = Dataset(["process-provenance"],["root"])
        Expect.throws (fun () -> Dataset.withIdentifiers(value,[]) |> ignore) "Empty identifiers rejected by copy"
        Expect.throws (fun () -> Dataset.withConformsTo(value,[]) |> ignore) "Empty profiles rejected by copy"
        let renamed = Dataset.withTitle(value,"new")
        same value.Identifiers renamed.Identifiers
        same value.Processes renamed.Processes
        Expect.isNone value.Title "Copy leaves source unchanged")
]
