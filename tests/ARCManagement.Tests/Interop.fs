module ARCManagement.Tests.Interop

open ARCBaseModel
open ARCManagement
open ARCManagement.Helper
open ARCtrl
open PolyglotSQLite
open Fable.Pyxpecto

// Every runtime writes this fixture and every runtime reads every fixture. The expected content is built by the same
// code in each runtime, so the reader compares the stored graphs with graphs it constructs itself.

let private setProperty (target: EntityObject) key value =
    target.Common <- { target.Common with Properties = target.Common.Properties.With(key, value) }
let private property (target: EntityObject) key = target.EntityProperties.Get(key)
let private same actual expected = Expect.isTrue (obj.ReferenceEquals(actual, expected)) "Shared entity reference"
let private number message (expected: float) (value: AnnotationValue option) =
    match value with
    | Some(AnnotationValue.Number v) -> Expect.equal v expected message
    | _ -> failwith (message + ": expected a number")
let private text message (expected: string) (value: AnnotationValue option) =
    match value with
    | Some(AnnotationValue.Text v) -> Expect.equal v expected message
    | _ -> failwith (message + ": expected text")

/// Uses every entity kind and every alternative. Every entity carries an ID, so the encoded text is reproducible.
let private graph (sampleName: string) (title: string) =
    let temperature = Annotation("temperature", value = AnnotationValue.Number 0.25, unit = "degree Celsius", nameTAN = "N1", valueTAN = "N2", unitTAN = "N3", id = "pv:temperature")
    let zero = Annotation("zero", value = AnnotationValue.Number 0.0, id = "pv:zero")
    let note = Annotation("note", value = AnnotationValue.Text "42", id = "pv:note", additionalTypes = ["CharacteristicValue"; "CharacteristicValue"])
    let blank = Annotation("blank", value = AnnotationValue.Text "", id = "pv:blank")
    let absent = Annotation("absent", id = "pv:absent")
    let parameter = FormalParameter(name = "temperature", nameTAN = "N4", id = "fp1")
    let looped = Annotation("looped", value = AnnotationValue.Number 3.0, instanceOf = parameter, id = "pv:looped")
    parameter.State <- { parameter.State with DefaultValue = Some looped }
    let terms = DefinedTermSet("terms", identifier = "t1", id = "set1")
    let steward = DefinedTerm("steward", identifier = "dt", tan = "T1", inDefinedTermSet = DefinedTermSetReference.TermSet terms, id = "dt1")
    let linked = DefinedTerm("linked", inDefinedTermSet = DefinedTermSetReference.Url "https://example.org/terms", id = "dt2")
    let recipe = Recipe(name = "extraction", parameters = [parameter; parameter], description = "Protein extraction", intendedUse = RecipeIntendedUse.Term steward, additionalProperties = [note], components = [temperature], version = "1", url = "https://example.org/recipe", id = "r1")
    let textRecipe = Recipe(name = "text use", intendedUse = RecipeIntendedUse.Text "storage", id = "r2")
    let leaf = Sample(sampleName, additionalProperties = [temperature; zero], id = "s1", additionalTypes = ["Source"])
    let part = Data("raw/part.tsv", selector = "#col=1", selectorFormat = "RFC7111", id = "d2")
    let raw = Data("raw/extract.tsv", encodingFormat = "text/tab-separated-values", hasParts = [part; part], additionalProperties = [note], id = "d1")
    let organization = Organization("Lab", url = "https://example.org", id = "o1", additionalTypes = ["Institute"])
    let agent = Agent("Ada Lovelace", givenName = "Ada", familyName = "Lovelace", emails = ["a@example.org"; "a@example.org"], affiliations = [organization; organization], identifiers = ["orcid"], additionalProperties = [note], jobTitles = [steward; linked], id = "a1")
    let article = ScholarlyArticle("Paper", identifiers = ["doi:1"; "doi:2"], authors = [agent; agent], creativeWorkStatus = steward, additionalProperties = [temperature], id = "art1")
    let sampleDescriptor = Descriptor(EntityReference.Sample leaf, annotations = [temperature; note; temperature], id = "desc1")
    let dataDescriptor = Descriptor(EntityReference.Data raw, annotations = [zero], id = "desc2")
    let first = ARCBaseModel.Process("measure", input = EntityReference.Sample leaf, output = EntityReference.Data raw, executesRecipe = recipe, parameterValues = [temperature; temperature], id = "p1", additionalTypes = ["LabProcess"])
    let second = ARCBaseModel.Process("measure again", input = EntityReference.Data part, output = EntityReference.Sample leaf, executesRecipe = textRecipe, id = "p2")
    let child = Dataset(["semantic-designation"], ["child"], descriptors = [sampleDescriptor; dataDescriptor], id = "child-id")
    let root = Dataset(["administrative"; "process-provenance"], ["root"; "root"], title = title, description = "Description", license = "CC-BY-4.0", datePublished = "2026-01-02", dateCreated = "2026-01-01", dateModified = "2026-01-03", hasParts = [child; child], dataFiles = [raw; part; raw], agents = [agent; agent], citations = [article], processes = [first; second], additionalProperties = [temperature; zero; note; blank; absent; temperature; looped], id = "root-id", additionalTypes = ["Investigation"; "Investigation"])
    // The object reaches the root through an extension, which closes a second cycle.
    let custom = EntityObject("Custom", id = "custom-1", additionalTypes = ["Tagged"; "Tagged"])
    setProperty custom "owner" (Entity.Object root)
    setProperty root "text" (Entity.Text "0")
    setProperty root "score" (Entity.Number 0.5)
    setProperty root "zero" (Entity.Number 0.0)
    setProperty root "flag" (Entity.Bool false)
    setProperty root "nothing" (Entity.Null(EntityNull()))
    setProperty root "bytes" (Entity.Blob(EntityBlob("AAE=")))
    setProperty root "list" (Entity.Collection(EntityCollection([Entity.Text "inner"; Entity.Number 1.0; Entity.Bool true; Entity.Object custom; Entity.Collection(EntityCollection([]))])))
    setProperty root "custom" (Entity.Object custom)
    setProperty leaf "custom" (Entity.Object custom)
    root

// Both entries start with the stand-alone add, which stays in history. Entry 0 then undoes its only edit.
// Entry 1 keeps the first edit and undoes the second.
let private stored index = if index = 0 then graph "leaf" "Title" else graph "edited leaf" "Title"
let private redone index = if index = 0 then graph "edited leaf" "Title" else graph "edited leaf" "Edited title"

/// Not reachable from the root, so it stays in the registry as a session-only entity.
let private standalone () = Sample("standalone", id = "standalone-1")

/// The entity IDs in registry order that every runtime must have stored for both entries.
let private expectedEntityIds () =
    use session = Session.createInMemory()
    let arc = session.createArc(graph "leaf" "Title")
    arc.Sample.add(standalone ()) |> ignore
    let ids = arc.Entity.list() |> Seq.map (fun entity -> entity.Id.Value) |> List.ofSeq
    session.close()
    ids

let private workspaceTitle = "Workspace title"
let private workspaceNumbers = [37.0; 0.25; 1e21; 1e-7]

/// Numbers whose text differs between runtimes, as Annotation values and as extension numbers.
let private workspaceGraph () =
    let annotations = workspaceNumbers |> List.mapi (fun index value -> Annotation("n" + string index, value = AnnotationValue.Number value, id = "wa" + string index))
    let root = Dataset(["administrative"], ["workspace"], title = workspaceTitle, additionalProperties = annotations, id = "workspace-root")
    workspaceNumbers |> List.iteri (fun index value -> setProperty root ("x" + string index) (Entity.Number value))
    root

let write (path: string) =
    use session = Session.createFile path
    let first = session.createArc(graph "leaf" "Title")
    first.Sample.add(standalone ()) |> ignore
    first.Sample.setName(first.Sample.get("s1"), "edited leaf") |> ignore
    first.History.undo()
    let second = session.createArc(graph "leaf" "Title")
    second.Sample.add(standalone ()) |> ignore
    second.Sample.setName(second.Sample.get("s1"), "edited leaf") |> ignore
    second.Dataset.setTitle(second.Model, Some "Edited title") |> ignore
    second.History.undo()
    session.close()
    let workspacePath = path + ".workspace"
    if Path.pathExists workspacePath then Path.deleteDirectory workspacePath
    let arc = ARC.create(workspacePath, workspaceGraph ())
    arc.save()
    arc.close()

/// Checks sharing, cycles, alternatives and number types that the encoded text alone would not pin down.
let private verify (root: Dataset) =
    Expect.equal root.Id (Some "root-id") "Root ID"
    Expect.equal (Seq.toList root.Identifiers) ["root"; "root"] "Duplicate identifiers"
    Expect.equal (Seq.toList root.AdditionalTypes) ["Investigation"; "Investigation"] "Duplicate types"
    same (Seq.item 0 root.HasParts) (Seq.item 1 root.HasParts)
    let child = Seq.item 0 root.HasParts
    let properties = root.AdditionalProperties
    Expect.equal (Seq.length properties) 7 "Annotation count"
    same (Seq.item 0 properties) (Seq.item 5 properties)
    number "Fraction" 0.25 (Seq.item 0 properties).Value
    number "Zero" 0.0 (Seq.item 1 properties).Value
    text "Numeric-looking text" "42" (Seq.item 2 properties).Value
    text "Empty text" "" (Seq.item 3 properties).Value
    Expect.isNone (Seq.item 4 properties).Value "Absent value"
    number "Whole number" 3.0 (Seq.item 6 properties).Value
    let looped = Seq.item 6 properties
    let parameter = looped.InstanceOf.Value
    same parameter.DefaultValue.Value looped
    let first, second = Seq.item 0 root.Processes, Seq.item 1 root.Processes
    let recipe = first.ExecutesRecipe.Value
    same (Seq.item 0 recipe.Parameters) parameter
    same (Seq.item 1 recipe.Parameters) parameter
    same (Seq.item 0 first.ParameterValues) (Seq.item 0 properties)
    same (Seq.item 1 first.ParameterValues) (Seq.item 0 properties)
    let leaf = match first.Input with Some(EntityReference.Sample v) -> v | _ -> failwith "Expected a Sample input"
    let raw = match first.Output with Some(EntityReference.Data v) -> v | _ -> failwith "Expected a Data output"
    let part = match second.Input with Some(EntityReference.Data v) -> v | _ -> failwith "Expected a Data input"
    match second.Output with
    | Some(EntityReference.Sample v) -> same v leaf
    | _ -> failwith "Expected a Sample output"
    same raw (Seq.item 0 root.DataFiles)
    same raw (Seq.item 2 root.DataFiles)
    same part (Seq.item 1 root.DataFiles)
    same part (Seq.item 0 raw.HasParts)
    same part (Seq.item 1 raw.HasParts)
    let term =
        match recipe.IntendedUse with
        | Some(RecipeIntendedUse.Term v) -> v
        | _ -> failwith "Expected a term"
    match term.InDefinedTermSet with
    | Some(DefinedTermSetReference.TermSet set) -> Expect.equal set.Identifier (Some "t1") "Term set alternative"
    | _ -> failwith "Expected a term set"
    match second.ExecutesRecipe.Value.IntendedUse with
    | Some(RecipeIntendedUse.Text v) -> Expect.equal v "storage" "Text alternative"
    | _ -> failwith "Expected text"
    let agent = Seq.item 0 root.Agents
    same agent (Seq.item 1 root.Agents)
    same (Seq.item 0 agent.JobTitles) term
    same (Seq.item 0 agent.Affiliations) (Seq.item 1 agent.Affiliations)
    match (Seq.item 1 agent.JobTitles).InDefinedTermSet with
    | Some(DefinedTermSetReference.Url v) -> Expect.equal v "https://example.org/terms" "URL alternative"
    | _ -> failwith "Expected a URL"
    let article = Seq.item 0 root.Citations
    same (Seq.item 0 article.Authors) agent
    same (Seq.item 1 article.Authors) agent
    same article.CreativeWorkStatus.Value term
    match (Seq.item 0 child.Descriptors).Describes with
    | EntityReference.Sample v -> same v leaf
    | _ -> failwith "Expected a Sample descriptor"
    match (Seq.item 1 child.Descriptors).Describes with
    | EntityReference.Data v -> same v raw
    | _ -> failwith "Expected a Data descriptor"
    match property root "text" with Entity.Text v -> Expect.equal v "0" "Extension text" | _ -> failwith "Expected text"
    match property root "score" with Entity.Number v -> Expect.equal v 0.5 "Extension fraction" | _ -> failwith "Expected a number"
    match property root "zero" with Entity.Number v -> Expect.equal v 0.0 "Extension zero" | _ -> failwith "Expected a number"
    match property root "flag" with Entity.Bool v -> Expect.isFalse v "Extension Boolean" | _ -> failwith "Expected a Boolean"
    match property root "nothing" with Entity.Null _ -> () | _ -> failwith "Expected null"
    match property root "bytes" with Entity.Blob v -> Expect.equal v.Base64 "AAE=" "Extension blob" | _ -> failwith "Expected a blob"
    let custom = match property root "custom" with Entity.Object v -> v | _ -> failwith "Expected an object"
    Expect.equal custom.Id (Some "custom-1") "Extension object ID"
    Expect.equal (Seq.toList custom.AdditionalTypes) ["Tagged"; "Tagged"] "Generic object types"
    match property custom "owner" with Entity.Object v -> same v root | _ -> failwith "Expected the root"
    match property leaf "custom" with Entity.Object v -> same v custom | _ -> failwith "Expected the shared object"
    match property root "list" with
    | Entity.Collection values ->
        Expect.equal values.Count 5 "Nested collection"
        match values.Get(0), values.Get(1), values.Get(2) with
        | Entity.Text a, Entity.Number b, Entity.Bool c ->
            Expect.equal (a, b, c) ("inner", 1.0, true) "Nested scalars"
        | _ -> failwith "Expected nested scalars"
        match values.Get(3) with Entity.Object v -> same v custom | _ -> failwith "Expected the shared object"
        match values.Get(4) with Entity.Collection v -> Expect.equal v.Count 0 "Empty nested collection" | _ -> failwith "Expected a collection"
    | _ -> failwith "Expected a collection"

let private compare message (expected: Dataset) (actual: Dataset) =
    Expect.equal (DatasetYaml.encode actual) (DatasetYaml.encode expected) message
    verify actual

let private inspect path =
    use session = Session.openFile path
    let entries = session.listArcs() |> Seq.toList
    Expect.equal entries.Length 2 "Two ARC entries"
    let expectedIds = expectedEntityIds ()
    Expect.isFalse ((List.item 0 entries).ArcId = (List.item 1 entries).ArcId) "Distinct entry IDs"
    for index, info in List.indexed entries do
        Expect.isFalse (System.String.IsNullOrEmpty info.ArcId) "Entry ID"
        Expect.equal info.RootEntityId "root-id" "Root entity ID"
        Expect.isNone info.Folder "No bound folder"
        let graph = session.openArc(info.ArcId)
        compare "Stored model" (stored index) graph.Model
        Expect.equal (graph.Entity.list() |> Seq.map (fun entity -> entity.Id.Value) |> List.ofSeq) expectedIds "Entity registry order"
        Expect.isTrue graph.HasSessionOnlyObjects "The stand-alone entity is session-only"
        Expect.isTrue graph.History.CanUndo "The stand-alone add can be undone"
        Expect.isTrue graph.History.CanRedo "The undone command leaves a redo branch"
    session.close()

// The edits run on a copy so the fixture stays unchanged for the next reader.
let private edit path =
    let copy = path + ".edit"
    if Path.pathExists copy then Path.deleteFile copy
    Path.writeFileBinary copy (Path.readFileBinary path)
    use session = Session.openFile copy
    for index, info in session.listArcs() |> Seq.toList |> List.indexed do
        let graph = session.openArc(info.ArcId)
        graph.History.redo()
        compare "Redone model" (redone index) graph.Model
        Expect.isFalse graph.History.CanRedo "The redo branch is consumed"
        Expect.isTrue graph.History.CanUndo "CanUndo after redo"
        graph.History.undo()
        compare "Model after undo" (stored index) graph.Model
        Expect.isTrue graph.History.CanRedo "Redo is available again"
    session.close()
    Path.deleteFile copy

let rec private copyFolder (source: string) (destination: string) =
    Path.createDirectory destination
    for file in Path.getSubFiles source do
        Path.writeFileBinary (Path.combineNative destination (Array.last (Path.split file))) (Path.readFileBinary file)
    for directory in Path.getSubDirectories source do
        copyFolder directory (Path.combineNative destination (Array.last (Path.split directory)))

let private checkWorkspaceNumbers (root: Dataset) =
    let values = root.AdditionalProperties |> Seq.map (fun a -> match a.Value with Some(AnnotationValue.Number v) -> v | _ -> nan) |> List.ofSeq
    Expect.equal values workspaceNumbers "Annotation numbers"
    let extensions = workspaceNumbers |> List.mapi (fun index _ -> match property root ("x" + string index) with Entity.Number v -> v | _ -> nan)
    Expect.equal extensions workspaceNumbers "Extension numbers"

// The copy is bound to a new folder, so the binding in its database follows. Opening it with "auto" then
// decides from the stored saved graph, which another runtime wrote.
let private inspectWorkspace (path: string) =
    let copy = Helpers.folder "interop"
    copyFolder (path + ".workspace") copy
    let database = ARCtrl.Internal.Workspace.path copy
    let rebound = Session.openFile database
    rebound.Repository.Connection.WithTransaction(fun () ->
        rebound.Repository.Connection.Execute("UPDATE arc SET folder=$folder", [SqlParameter("folder", SqlValue.Text copy)])) |> ignore
    rebound.close()
    let arc = ARC.openFolder(copy, "auto")
    Expect.isFalse arc.IsDirty "A foreign save is not an edit"
    Expect.isFalse arc.HasSessionOnlyObjects "Nothing is session-only"
    Expect.equal arc.Model.Title (Some workspaceTitle) "Saved title"
    checkWorkspaceNumbers arc.Model
    arc.close()
    let file = Path.combineNative copy ARCtrl.Helper.Layout.ARCFileName
    let original = Path.readFileText file
    let edited = original.Replace("title: " + workspaceTitle, "title: Edited " + workspaceTitle)
    Expect.notEqual edited original "The title line is edited"
    Path.writeFileText file edited
    let reopened = ARC.openFolder(copy, "auto")
    Expect.equal reopened.Model.Title (Some("Edited " + workspaceTitle)) "The edited file is adopted"
    checkWorkspaceNumbers reopened.Model
    reopened.close()
    Path.deleteDirectory copy

let read (path: string) =
    inspect path
    edit path
    inspectWorkspace path

let tryRun (arguments: string array) =
    match arguments with
    | [|"--write-fixture"; path|] -> write path; true
    | [|"--read-fixture"; path|] -> read path; true
    | _ -> false
