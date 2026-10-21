module ARCManagement.Tests.FullModel

open System
open ARCBaseModel
open ARCManagement
open Fable.Pyxpecto

let private same a b = Expect.isTrue (obj.ReferenceEquals(a,b)) "Shared entity reference"
let private rev (g:ManagedGraph) = ARCManagement.Internal.Store.revision g.Context.Repository.Connection g.ArcId

let private checkCollection name (set:seq<'T> -> WriteResult<'Owner>) (add:'T -> WriteResult<'Owner>) (remove:'T -> WriteResult<'Owner>) (items:'Owner -> System.Collections.Generic.IReadOnlyList<'T>) first second =
    let ids values = values |> Seq.map (fun value -> (unbox<EntityObject> (box value)).Id) |> Seq.toList
    let initial = set [first;second;first]
    Expect.equal (ids (items initial.Value)) (ids [first;second;first]) (name+" preserves order and duplicates")
    same ((items initial.Value)[0]) ((items initial.Value)[2])
    let unchanged = set [first;second;first]
    Expect.isFalse unchanged.Changed (name+" identical replacement is a no-op")
    let appended = add second
    Expect.equal (ids (items appended.Value)) (ids [first;second;first;second]) (name+" appends an object")
    let removed = remove first
    Expect.equal (ids (items removed.Value)) (ids [second;first;second]) (name+" removes the first occurrence")
    Expect.equal (items initial.Value).Count 3 (name+" old snapshot unchanged")

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
        let first = graph.Annotation.add(Annotation("value",id="a"))
        let zero = graph.Annotation.setValueNumber(graph.Annotation.get("a"),0.0)
        Expect.equal zero.Value.Value (Some(AnnotationValue.Number 0.0)) "Zero is present"
        let text = graph.Annotation.setValueText(graph.Annotation.get("a"),"")
        Expect.equal text.Value.Value (Some(AnnotationValue.Text "")) "Empty text is present"
        let cleared = graph.Annotation.clearValue(graph.Annotation.get("a"))
        Expect.isNone cleared.Value.Value "Explicit clearing"
        Expect.equal zero.Value.Value (Some(AnnotationValue.Number 0.0)) "Earlier value unchanged"
        Expect.throws (fun () -> graph.Annotation.setValueNumber(graph.Annotation.get("a"),Double.NaN) |> ignore) "Nonfinite number rejected")

    testCase "focused relationships validate type and return the updated snapshot" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"]))
        graph.Sample.add(Sample("sample",id="s")) |> ignore
        graph.Data.add(Data("data.csv",id="d")) |> ignore
        let before = graph.Process.add(Process("p",id="p"))
        let sample = graph.Process.setInputSample(graph.Process.get("p"),graph.Sample.get("s"))
        match sample.Value.Input with Some(EntityReference.Sample v) -> Expect.equal v.Id (Some "s") "Typed Sample" | _ -> failwith "Expected Sample"
        let data = graph.Process.setOutputData(graph.Process.get("p"),graph.Data.get("d"))
        match data.Value.Output with Some(EntityReference.Data v) -> Expect.equal v.Path "data.csv" "Typed Data" | _ -> failwith "Expected Data"
        Expect.isNone before.Value.Input "Old value unchanged"
        Expect.throws (fun () -> graph.Process.setInputSample(graph.Process.get("p"),Sample("wrong kind",id="d")) |> ignore) "Wrong stored target kind rejected"
        let cleared = graph.Process.clearInput(graph.Process.get("p"))
        Expect.isNone cleared.Value.Input "Clear optional relationship"
        let descriptor = graph.Descriptor.create(EntityReference.Sample(graph.Sample.get("s")))
        match descriptor.Value.Describes with EntityReference.Sample v -> Expect.equal v.Id (Some "s") "Required factory target" | _ -> failwith "Expected Sample descriptor"
        Expect.throws (fun () -> graph.Descriptor.create(EntityReference.Sample(Sample("wrong kind",id=graph.Model.Id.Value))) |> ignore) "Descriptor factory rejects Dataset target")

    testCase "required constructor and copied Dataset invariants agree" (fun _ ->
        let value = Dataset(["process-provenance"],["root"])
        Expect.throws (fun () -> Dataset.withIdentifiers(value,[]) |> ignore) "Empty identifiers rejected by copy"
        Expect.throws (fun () -> Dataset.withConformsTo(value,[]) |> ignore) "Empty profiles rejected by copy"
        let renamed = Dataset.withTitle(value,"new")
        same value.Identifiers renamed.Identifiers
        same value.Processes renamed.Processes
        Expect.isNone value.Title "Copy leaves source unchanged")

    testCase "all relationship collections accept typed objects" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"]))
        let root = graph.Model
        let child = graph.Dataset.create("child").Value
        let child2 = graph.Dataset.create("child2").Value
        let data = graph.Data.create("data").Value
        let data2 = graph.Data.create("data2").Value
        let sample = graph.Sample.create("sample").Value
        let process = graph.Process.create("process").Value
        let process2 = graph.Process.create("process2").Value
        let annotation = graph.Annotation.create("annotation").Value
        let annotation2 = graph.Annotation.create("annotation2").Value
        let parameter = graph.FormalParameter.create("parameter").Value
        let parameter2 = graph.FormalParameter.create("parameter2").Value
        let recipe = graph.Recipe.create("recipe").Value
        let descriptor = graph.Descriptor.create(EntityReference.Sample sample).Value
        let descriptor2 = graph.Descriptor.create(EntityReference.Data data).Value
        let agent = graph.Agent.create("agent").Value
        let agent2 = graph.Agent.create("agent2").Value
        let organization = graph.Organization.create("organization").Value
        let organization2 = graph.Organization.create("organization2").Value
        let article = graph.ScholarlyArticle.create("article").Value
        let article2 = graph.ScholarlyArticle.create("article2").Value
        let term = graph.DefinedTerm.create("term").Value
        let term2 = graph.DefinedTerm.create("term2").Value
        checkCollection "Dataset.hasParts" (fun v -> graph.Dataset.setHasParts(root,v)) (fun v -> graph.Dataset.addPart(root,v)) (fun v -> graph.Dataset.removePart(root,v)) (fun v -> v.HasParts) child child2
        checkCollection "Dataset.dataFiles" (fun v -> graph.Dataset.setDataFiles(root,v)) (fun v -> graph.Dataset.addDataFile(root,v)) (fun v -> graph.Dataset.removeDataFile(root,v)) (fun v -> v.DataFiles) data data2
        checkCollection "Dataset.agents" (fun v -> graph.Dataset.setAgents(root,v)) (fun v -> graph.Dataset.addAgent(root,v)) (fun v -> graph.Dataset.removeAgent(root,v)) (fun v -> v.Agents) agent agent2
        checkCollection "Dataset.citations" (fun v -> graph.Dataset.setCitations(root,v)) (fun v -> graph.Dataset.addCitation(root,v)) (fun v -> graph.Dataset.removeCitation(root,v)) (fun v -> v.Citations) article article2
        checkCollection "Dataset.processes" (fun v -> graph.Dataset.setProcesses(root,v)) (fun v -> graph.Dataset.addProcess(root,v)) (fun v -> graph.Dataset.removeProcess(root,v)) (fun v -> v.Processes) process process2
        checkCollection "Dataset.descriptors" (fun v -> graph.Dataset.setDescriptors(root,v)) (fun v -> graph.Dataset.addDescriptor(root,v)) (fun v -> graph.Dataset.removeDescriptor(root,v)) (fun v -> v.Descriptors) descriptor descriptor2
        checkCollection "Dataset.additionalProperties" (fun v -> graph.Dataset.setAdditionalProperties(root,v)) (fun v -> graph.Dataset.addAdditionalProperty(root,v)) (fun v -> graph.Dataset.removeAdditionalProperty(root,v)) (fun v -> v.AdditionalProperties) annotation annotation2
        checkCollection "Data.hasParts" (fun v -> graph.Data.setHasParts(data,v)) (fun v -> graph.Data.addPart(data,v)) (fun v -> graph.Data.removePart(data,v)) (fun v -> v.HasParts) data data2
        checkCollection "Data.additionalProperties" (fun v -> graph.Data.setAdditionalProperties(data,v)) (fun v -> graph.Data.addAdditionalProperty(data,v)) (fun v -> graph.Data.removeAdditionalProperty(data,v)) (fun v -> v.AdditionalProperties) annotation annotation2
        checkCollection "Sample.additionalProperties" (fun v -> graph.Sample.setAdditionalProperties(sample,v)) (fun v -> graph.Sample.addAdditionalProperty(sample,v)) (fun v -> graph.Sample.removeAdditionalProperty(sample,v)) (fun v -> v.AdditionalProperties) annotation annotation2
        checkCollection "Process.parameterValues" (fun v -> graph.Process.setParameterValues(process,v)) (fun v -> graph.Process.addParameterValue(process,v)) (fun v -> graph.Process.removeParameterValue(process,v)) (fun v -> v.ParameterValues) annotation annotation2
        checkCollection "Recipe.parameters" (fun v -> graph.Recipe.setParameters(recipe,v)) (fun v -> graph.Recipe.addParameter(recipe,v)) (fun v -> graph.Recipe.removeParameter(recipe,v)) (fun v -> v.Parameters) parameter parameter2
        checkCollection "Recipe.components" (fun v -> graph.Recipe.setComponents(recipe,v)) (fun v -> graph.Recipe.addComponent(recipe,v)) (fun v -> graph.Recipe.removeComponent(recipe,v)) (fun v -> v.Components) annotation annotation2
        checkCollection "Recipe.additionalProperties" (fun v -> graph.Recipe.setAdditionalProperties(recipe,v)) (fun v -> graph.Recipe.addAdditionalProperty(recipe,v)) (fun v -> graph.Recipe.removeAdditionalProperty(recipe,v)) (fun v -> v.AdditionalProperties) annotation annotation2
        checkCollection "Descriptor.annotations" (fun v -> graph.Descriptor.setAnnotations(descriptor,v)) (fun v -> graph.Descriptor.addAnnotation(descriptor,v)) (fun v -> graph.Descriptor.removeAnnotation(descriptor,v)) (fun v -> v.Annotations) annotation annotation2
        checkCollection "Agent.affiliations" (fun v -> graph.Agent.setAffiliations(agent,v)) (fun v -> graph.Agent.addAffiliation(agent,v)) (fun v -> graph.Agent.removeAffiliation(agent,v)) (fun v -> v.Affiliations) organization organization2
        checkCollection "Agent.jobTitles" (fun v -> graph.Agent.setJobTitles(agent,v)) (fun v -> graph.Agent.addJobTitle(agent,v)) (fun v -> graph.Agent.removeJobTitle(agent,v)) (fun v -> v.JobTitles) term term2
        checkCollection "Agent.additionalProperties" (fun v -> graph.Agent.setAdditionalProperties(agent,v)) (fun v -> graph.Agent.addAdditionalProperty(agent,v)) (fun v -> graph.Agent.removeAdditionalProperty(agent,v)) (fun v -> v.AdditionalProperties) annotation annotation2
        checkCollection "ScholarlyArticle.authors" (fun v -> graph.ScholarlyArticle.setAuthors(article,v)) (fun v -> graph.ScholarlyArticle.addAuthor(article,v)) (fun v -> graph.ScholarlyArticle.removeAuthor(article,v)) (fun v -> v.Authors) agent agent2
        checkCollection "ScholarlyArticle.additionalProperties" (fun v -> graph.ScholarlyArticle.setAdditionalProperties(article,v)) (fun v -> graph.ScholarlyArticle.addAdditionalProperty(article,v)) (fun v -> graph.ScholarlyArticle.removeAdditionalProperty(article,v)) (fun v -> v.AdditionalProperties) annotation annotation2)

    testCase "single relationships and extensions resolve current stored objects" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"]))
        let sample = graph.Sample.create("sample").Value
        let data = graph.Data.create("data").Value
        let process = graph.Process.create("process").Value
        let recipe = graph.Recipe.create("recipe").Value
        let term = graph.DefinedTerm.create("term").Value
        let termset = graph.DefinedTermSet.create("termset").Value
        let annotation = graph.Annotation.create("annotation").Value
        let parameter = graph.FormalParameter.create("parameter").Value
        let article = graph.ScholarlyArticle.create("article").Value
        graph.Sample.setName(sample,"current sample") |> ignore
        let input = graph.Process.setInput(process,EntityReference.Sample sample)
        match input.Value.Input with Some(EntityReference.Sample v) -> Expect.equal v.Name "current sample" "Old target resolves current state" | _ -> failwith "Expected Sample"
        graph.Process.setInputData(process,data) |> ignore
        graph.Process.setOutputSample(process,sample) |> ignore
        let output = graph.Process.setOutput(process,EntityReference.Data data)
        match output.Value.Output with Some(EntityReference.Data v) -> Expect.equal v.Id data.Id "Data alternative" | _ -> failwith "Expected Data"
        Expect.equal (graph.Process.setExecutesRecipe(process,recipe)).Value.ExecutesRecipe.Value.Id recipe.Id "Recipe target"
        Expect.equal (graph.Annotation.setInstanceOf(annotation,parameter)).Value.InstanceOf.Value.Id parameter.Id "Parameter target"
        Expect.equal (graph.FormalParameter.setDefaultValue(parameter,annotation)).Value.DefaultValue.Value.Id annotation.Id "Annotation target and cycle"
        Expect.equal (graph.ScholarlyArticle.setCreativeWorkStatus(article,term)).Value.CreativeWorkStatus.Value.Id term.Id "Term target"
        match (graph.Recipe.setIntendedUseTerm(recipe,term)).Value.IntendedUse with Some(RecipeIntendedUse.Term v) -> Expect.equal v.Id term.Id "Term alternative" | _ -> failwith "Expected Term"
        match (graph.DefinedTerm.setInDefinedTermSetEntity(term,termset)).Value.InDefinedTermSet with Some(DefinedTermSetReference.TermSet v) -> Expect.equal v.Id termset.Id "TermSet alternative" | _ -> failwith "Expected TermSet"
        let descriptor = graph.Descriptor.create(EntityReference.Sample sample).Value
        graph.Descriptor.setDescribesSample(descriptor,sample) |> ignore
        graph.Descriptor.setDescribesData(descriptor,data) |> ignore
        match (graph.Descriptor.setDescribes(descriptor,EntityReference.Sample sample)).Value.Describes with EntityReference.Sample v -> Expect.equal v.Name "current sample" "Descriptor target" | _ -> failwith "Expected Sample"
        graph.Entity.setObjectProperty(graph.Model,"sample",sample) |> ignore
        let extension = graph.Entity.addObjectProperty(graph.Model,"process",process)
        match extension.Value.EntityProperties.Get("process") with Entity.Object v -> Expect.equal v.Id process.Id "Object extension" | _ -> failwith "Expected Object"
        Expect.isTrue (graph.Entity.hasProperty(extension.Value,"sample")) "Property lookup takes an object"
        match graph.Entity.getProperty(extension.Value,"sample") with Entity.Object v -> Expect.equal v.Id sample.Id "Property read takes an object" | _ -> failwith "Expected Object"
        Expect.equal sample.Name "sample" "Target object unchanged")

    testCase "invalid relationship objects fail atomically without adoption" (fun _ ->
        use session = Session.createInMemory()
        let graph = session.createArc(Dataset(["process-provenance"],["root"]))
        let process = graph.Process.create("process").Value
        let sample = graph.Sample.create("sample").Value
        let root = graph.Dataset.addProcess(graph.Model,process).Value
        let other = session.createArc(Dataset(["process-provenance"],["other"]))
        let foreignProcess = other.Process.add(Process("foreign",id=process.Id.Value)).Value
        let foreignSample = other.Sample.add(Sample("foreign",id=sample.Id.Value)).Value
        let before = rev graph
        let reject action = Expect.throws action "Invalid target rejected"; Expect.equal (rev graph) before "No revision/history change"
        reject (fun () -> graph.Dataset.addProcess(root,Process("missing ID")) |> ignore)
        reject (fun () -> graph.Dataset.addProcess(root,Process("unknown",id="unknown")) |> ignore)
        reject (fun () -> graph.Dataset.addProcess(root,Process("wrong stored kind",id=sample.Id.Value)) |> ignore)
        reject (fun () -> graph.Dataset.removeProcess(root,foreignProcess) |> ignore)
        reject (fun () -> graph.Dataset.setProcesses(root,[process;foreignProcess]) |> ignore)
        reject (fun () -> graph.Process.setInput(process,EntityReference.Sample foreignSample) |> ignore)
        reject (fun () -> graph.Descriptor.create(EntityReference.Sample foreignSample) |> ignore)
        reject (fun () -> graph.Entity.setObjectProperty(root,"foreign",foreignProcess) |> ignore)
        reject (fun () -> graph.Entity.setCollectionProperty(root,"foreign",[Entity.Object foreignProcess]) |> ignore)
        let linked = graph.Dataset.addProcess(root,Process("detached fields ignored",id=process.Id.Value))
        Expect.equal linked.Value.Processes[1].Name "process" "Detached typed object selects stored target"
        same linked.Value.Processes[0] linked.Value.Processes[1]
        graph.History.undo() |> ignore
        Expect.equal graph.Model.Processes.Count 1 "Undo object attachment"
        graph.History.redo() |> ignore
        Expect.equal graph.Model.Processes.Count 2 "Redo object attachment")
]
