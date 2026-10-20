module ARCManagement.Tests.Interop

open ARCBaseModel
open ARCManagement

let private require message condition = if not condition then failwith message
let private same a b = obj.ReferenceEquals(a,b)

/// Fixed domain oracle, independent of the runtime that writes the database.
let private fixture () =
    let terms = DefinedTermSet("Terms",identifier="https://example.org/terms",id="terms")
    let term = DefinedTerm("Purpose",identifier="T:1",inDefinedTermSet=DefinedTermSetReference.TermSet terms,id="term")
    let numeric = Annotation("fraction",value=AnnotationValue.Number 0.125,id="number")
    let textual = Annotation("text",value=AnnotationValue.Text "0.125",id="text")
    let zero = Annotation("zero",value=AnnotationValue.Number 0.0,id="zero")
    let parameter = FormalParameter(name="parameter",defaultValue=numeric,id="parameter")
    let sample = Sample("initial",additionalProperties=[numeric;textual;zero;numeric],id="sample",additionalTypes=["custom";"custom"])
    let data = Data("data/a.csv",selector="",encodingFormat="text/csv",id="data")
    let recipe = Recipe(name="Recipe",parameters=[parameter;parameter],intendedUse=RecipeIntendedUse.Term term,id="recipe")
    let process = Process("Measure",input=EntityReference.Sample sample,output=EntityReference.Data data,executesRecipe=recipe,parameterValues=[numeric],id="process")
    let organization = Organization("Lab",url="https://example.org",id="organization")
    let agent = Agent("A Person",emails=["a@example.org";"a@example.org"],affiliations=[organization],jobTitles=[term],id="agent")
    let article = ScholarlyArticle("Result",identifiers=["doi:example"],authors=[agent;agent],creativeWorkStatus=term,id="article")
    let descriptor = Descriptor(EntityReference.Sample sample,annotations=[numeric],id="descriptor")
    let child = Dataset(["administrative"],["child"],title="child")
    let root = Dataset(["process-provenance";"semantic-designation";"administrative"],["fixture";"fixture"],id="root",
                       hasParts=[child;child],processes=[process;process],dataFiles=[data],agents=[agent],citations=[article],descriptors=[descriptor])
    let root = Dataset.withEntityProperty(root,"values",Entity.Collection(EntityCollection([
        Entity.Text "";Entity.Number 0.0;Entity.Bool false;Entity.Null(EntityNull());Entity.Blob(EntityBlob("AAH/"));
        Entity.Collection(EntityCollection([]));Entity.Object sample])))
    let builder = ModelBuilder()
    let draft = builder.cloneGraph(root)
    let childDraft = builder.draftFor(child)
    builder.Dataset.setHasParts(draft,[draft;childDraft;childDraft])
    builder.Data.setHasParts(builder.draftFor(data),[builder.draftFor(data)])
    builder.Annotation.setInstanceOf(builder.draftFor(numeric),builder.draftFor(parameter))
    builder.Dataset.setEntityProperty(draft,"backref",ModelValue.objectValue(draft))
    builder.finish().get(draft)

let write path =
    use session = Session.createFile(path)
    let graph = session.createArc(fixture())
    let named = graph.Sample.setName(graph.Sample.get("sample"),"persisted")
    let titled = graph.Dataset.setTitle(graph.Dataset.get("root"),"")
    let outside = graph.Sample.add(Sample("standalone",id="outside"))
    let changed = graph.Sample.setName(graph.Sample.get("outside"),"redo-value")
    graph.History.undo() |> ignore
    require "Writer did not retain redo history." graph.History.CanRedo

let read path =
    use session = Session.openFile(path)
    let entries = session.listArcs()
    require "Expected one fixture ARC." (entries.Count=1)
    let graph = session.openArc(entries[0].ArcId)
    let observed = graph.readModel()
    let root = observed
    require "Root identity/title differ." (root.Id=Some "root" && root.Title=Some "")
    require "Dataset declarations/identifiers lost ordering or duplicates." (Seq.toList root.Identifiers=["fixture";"fixture"] && root.ConformsTo.Count=3)
    require "Dataset cycle or duplicate child sharing differs." (root.HasParts.Count=3 && same root root.HasParts[0] && same root.HasParts[1] root.HasParts[2])
    require "Generated child identity missing." root.HasParts[1].Id.IsSome
    require "Process duplicates differ." (root.Processes.Count=2 && same root.Processes[0] root.Processes[1])
    let process = root.Processes[0]
    let sample = match process.Input with Some(EntityReference.Sample value) -> value | _ -> failwith "Wrong input alternative."
    let data = match process.Output with Some(EntityReference.Data value) -> value | _ -> failwith "Wrong output alternative."
    require "Updated Sample or duplicate types differ." (sample.Name="persisted" && Seq.toList sample.AdditionalTypes=["custom";"custom"])
    require "Data cycle or empty selector differs." (same data data.HasParts[0] && data.Selector=Some "")
    let numeric = sample.AdditionalProperties[0]
    require "Shared annotation differs." (same numeric sample.AdditionalProperties[3] && same numeric process.ParameterValues[0])
    match numeric.Value with Some(AnnotationValue.Number value) -> require "Fractional number differs." (value=0.125) | _ -> failwith "Number became text."
    match sample.AdditionalProperties[1].Value with Some(AnnotationValue.Text value) -> require "Text scalar differs." (value="0.125") | _ -> failwith "Text became numeric."
    match sample.AdditionalProperties[2].Value with Some(AnnotationValue.Number value) -> require "Numeric zero differs." (value=0.0) | _ -> failwith "Zero became absent."
    let parameter = numeric.InstanceOf |> Option.get
    require "FormalParameter/Annotation cycle differs." (same numeric parameter.DefaultValue.Value)
    require "Recipe shared parameters differ." (same parameter process.ExecutesRecipe.Value.Parameters[0] && same parameter process.ExecutesRecipe.Value.Parameters[1])
    require "Article author sharing differs." (same root.Agents[0] root.Citations[0].Authors[0] && same root.Citations[0].Authors[0] root.Citations[0].Authors[1])
    match root.EntityProperties.Get("backref") with Entity.Object value -> require "Extension cycle differs." (same root value) | _ -> failwith "Object extension became scalar."
    let values = match root.EntityProperties.Get("values") with Entity.Collection values -> values | _ -> failwith "Expected extension collection."
    match values.Get(2),values.Get(3),values.Get(4),values.Get(5),values.Get(6) with
    | Entity.Bool false,Entity.Null _,Entity.Blob blob,Entity.Collection empty,Entity.Object target ->
        require "Extension scalar/blob/collection/reference differs." (blob.Base64="AAH/" && empty.Count=0 && same sample target)
    | _ -> failwith "Extension storage alternatives differ."
    require "Standalone entity absent or list order differs." ((graph.Sample.list() |> Seq.map (fun value -> value.Id.Value) |> Seq.toList)=["sample";"outside"] && (graph.Sample.get("outside")).Name="standalone")
    require "Persisted history unavailable." (graph.History.CanUndo && graph.History.CanRedo)
    let redone = graph.History.redo()
    require "Cross-runtime forward delta failed." ((graph.Sample.get("outside")).Name="redo-value")
    graph.History.undo() |> ignore
    require "Cross-runtime inverse delta failed." ((graph.Sample.get("outside")).Name="standalone")
    require "Previously returned snapshot changed." (sample.Name="persisted" && same root root.HasParts[0])
    let decoded = graph.toYaml() |> DatasetYaml.decode
    require "Root-only export cycle differs." (same decoded decoded.HasParts[0])
    printfn "Management interoperability fixture passed: %s" path

let tryRun (arguments: string array) =
    match arguments with
    | [|"--write-fixture";path|] -> write path;true
    | [|"--read-fixture";path|] -> read path;true
    | _ -> false
