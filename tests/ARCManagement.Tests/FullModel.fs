module ARCManagement.Tests.FullModel

open ARCManagement.Helper

open System
open ARCManagement.Internal
open ARCBaseModel
open ARCtrl
open Fable.Pyxpecto

let private folder () = Helpers.folder "fullmodel"
let private withSql path action =
    use sql = PolyglotSQLite.Sqlite.OpenFile(path)
    action sql

let private same actual expected = Expect.isTrue (Object.ReferenceEquals(actual,expected)) "Canonical shared reference"
let private fixture () =
    let ontology = DefinedTermSet("Ontology",identifier="ontology",additionalTypes=["set";"set"])
    let term = DefinedTerm("Term",identifier="T:1",tan="TAN",inDefinedTermSet=DefinedTermSetReference.TermSet ontology,additionalTypes=["term"])
    let parameter = FormalParameter(name="Parameter",nameTAN="parameter-tan",additionalTypes=["parameter"])
    let number = Annotation("Number",value=AnnotationValue.Number 0.25,unit="unit",nameTAN="name-tan",valueTAN="value-tan",unitTAN="unit-tan",instanceOf=parameter,additionalTypes=["annotation"])
    parameter.State <- { parameter.State with DefaultValue = Some number }
    let text = Annotation("Text",value=AnnotationValue.Text "42")
    let absent = Annotation("Absent")
    let sample = Sample("Sample",additionalProperties=[number;text;number;absent],additionalTypes=["sample"])
    let fragment = Data("results.csv",selector="row=1",selectorFormat="selector-format",encodingFormat="text/csv",additionalProperties=[text],additionalTypes=["fragment"])
    let data = Data("results.csv",hasParts=[fragment;fragment],additionalProperties=[number],additionalTypes=["data"])
    let recipe = Recipe(name="Recipe",parameters=[parameter;parameter],description="description",intendedUse=RecipeIntendedUse.Term term,additionalProperties=[text],components=[number;number],version="1",url="recipe-url",additionalTypes=["recipe"])
    let proc = ARCBaseModel.Process("Process",input=EntityReference.Sample sample,output=EntityReference.Data data,executesRecipe=recipe,parameterValues=[number;text;number],additionalTypes=["process"])
    let sampleDescriptor = Descriptor(EntityReference.Sample sample,annotations=[text;number;text],additionalTypes=["descriptor"])
    let dataDescriptor = Descriptor(EntityReference.Data fragment,annotations=[number])
    let organization = Organization("Organization",url="organization-url",additionalTypes=["organization"])
    let agent = Agent("Agent",givenName="Given",familyName="Family",emails=["a@example.org";"a@example.org"],affiliations=[organization;organization],identifiers=["agent-id";"agent-id"],additionalProperties=[text],jobTitles=[term;term],additionalTypes=["agent"])
    let article = ScholarlyArticle("Headline",identifiers=["article-id";"article-id"],authors=[agent;agent],creativeWorkStatus=term,additionalProperties=[number],additionalTypes=["article"])
    let child = Dataset(["semantic-designation"],["child"],descriptors=[sampleDescriptor;dataDescriptor])
    let root = Dataset(["process-provenance";"semantic-designation";"administrative"],["root";"root"],title="Title",description="Description",license="License",datePublished="published",dateCreated="created",dateModified="modified",hasParts=[child;child],dataFiles=[data;fragment;data],agents=[agent;agent],citations=[article;article],processes=[proc;proc],descriptors=[sampleDescriptor;dataDescriptor],additionalProperties=[number;text;absent;number],additionalTypes=["root";"root"])
    root

let tests = testList "Full base-model management" [
    testCase "adopts all thirteen types and persists every property with shared references" (fun _ ->
        let path = folder()
        let supplied = fixture()
        let arc = ARC.create(path,supplied)
        Expect.isNone supplied.Id "Supplied root keeps its absent ID"
        Expect.isSome arc.Model.Id "Stored root carries an ID"
        Expect.equal (Seq.length (arc.Recipe.list())) 1 "Recipe registered"
        Expect.equal (Seq.length (arc.Annotation.list())) 3 "Shared annotations registered once"
        Expect.equal (Seq.length (arc.FormalParameter.list())) 1 "Parameter registered"
        Expect.equal (Seq.length (arc.DefinedTerm.list())) 1 "Term registered"
        Expect.equal (Seq.length (arc.DefinedTermSet.list())) 1 "Ontology registered"
        Expect.equal (Seq.length (arc.Agent.list())) 1 "Agent registered"
        Expect.equal (Seq.length (arc.Organization.list())) 1 "Organization registered"
        Expect.equal (Seq.length (arc.ScholarlyArticle.list())) 1 "Article registered"
        Expect.equal (Seq.length (arc.Descriptor.list())) 2 "Both endpoint alternatives"
        Expect.equal (Seq.length (arc.Data.list())) 2 "Nested fragments registered"
        Expect.isFalse arc.HasSessionOnlyObjects "All registered objects are reachable"
        arc.save()
        let yaml = Path.readFileText(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)
        Expect.isFalse (yaml.Contains("!<!!")) "No malformed built-in tag syntax"
        Expect.isTrue (yaml.Contains("value: \"42\"")) "Numeric-looking text is quoted"
        arc.close()
        for source in ["sql";"yml"] do
            let resumed = ARC.openFolder(path,source)
            let root = resumed.Model
            Expect.equal (Seq.length root.ConformsTo) 3 "Profiles"
            Expect.equal (Seq.toList root.Identifiers) ["root";"root"] "Ordered duplicate identifiers"
            Expect.equal root.Title (Some "Title") "Title"
            Expect.equal root.Description (Some "Description") "Description"
            Expect.equal root.License (Some "License") "License"
            Expect.equal root.DatePublished (Some "published") "Date"
            Expect.equal root.DateCreated (Some "created") "Date"
            Expect.equal root.DateModified (Some "modified") "Date"
            same (Seq.item 0 root.HasParts) (Seq.item 1 root.HasParts)
            same (Seq.item 0 root.Processes) (Seq.item 1 root.Processes)
            same (Seq.item 0 root.DataFiles) (Seq.item 2 root.DataFiles)
            same (Seq.item 0 (Seq.item 0 root.DataFiles).HasParts) (Seq.item 1 root.DataFiles)
            same (Seq.item 0 (Seq.item 0 root.DataFiles).HasParts) (Seq.item 1 (Seq.item 0 root.DataFiles).HasParts)
            let proc = (Seq.item 0 root.Processes)
            let recipe = proc.ExecutesRecipe.Value
            let parameter = (Seq.item 0 recipe.Parameters)
            let number = parameter.DefaultValue.Value
            same number.InstanceOf.Value parameter
            same (Seq.item 0 proc.ParameterValues) number
            same (Seq.item 0 recipe.Components) number
            same (Seq.item 0 recipe.Parameters) (Seq.item 1 recipe.Parameters)
            Expect.equal recipe.Description (Some "description") "Recipe description"
            Expect.equal recipe.Version (Some "1") "Version"
            Expect.equal recipe.Url (Some "recipe-url") "URL"
            Expect.equal number.Value (Some(AnnotationValue.Number 0.25)) "Fraction remains numeric"
            Expect.equal number.Unit (Some "unit") "Unit"
            Expect.equal number.NameTAN (Some "name-tan") "Name TAN"
            Expect.equal number.ValueTAN (Some "value-tan") "Value TAN"
            Expect.equal number.UnitTAN (Some "unit-tan") "Unit TAN"
            Expect.equal (Seq.item 1 root.AdditionalProperties).Value (Some(AnnotationValue.Text "42")) "Text remains text"
            Expect.isNone (Seq.item 2 root.AdditionalProperties).Value "Absence remains absent"
            match proc.Output with Some(EntityReference.Data v) -> same v (Seq.item 0 root.DataFiles) | _ -> failwith "Expected Data output"
            let agent = (Seq.item 0 root.Agents)
            same (Seq.item 0 root.Agents) (Seq.item 1 root.Agents)
            same (Seq.item 0 (Seq.item 0 root.Citations).Authors) agent
            same (Seq.item 0 (Seq.item 0 root.Citations).Authors) (Seq.item 1 (Seq.item 0 root.Citations).Authors)
            same (Seq.item 0 agent.Affiliations) (Seq.item 1 agent.Affiliations)
            Expect.equal (Seq.toList agent.Emails) ["a@example.org";"a@example.org"] "Emails"
            Expect.equal agent.GivenName (Some "Given") "Given name"
            Expect.equal agent.FamilyName (Some "Family") "Family name"
            Expect.equal (Seq.item 0 agent.Affiliations).Url (Some "organization-url") "Organization URL"
            same (Seq.item 0 agent.JobTitles) (Seq.item 0 root.Citations).CreativeWorkStatus.Value
            match recipe.IntendedUse with
            | Some(RecipeIntendedUse.Term term) ->
                same term (Seq.item 0 agent.JobTitles)
                Expect.equal term.TAN (Some "TAN") "Term TAN"
                match term.InDefinedTermSet with Some(DefinedTermSetReference.TermSet set) -> Expect.equal set.Identifier (Some "ontology") "Full term set" | _ -> failwith "Expected term set"
            | _ -> failwith "Expected term"
            same (Seq.item 0 root.Descriptors) (Seq.item 0 (Seq.item 0 root.HasParts).Descriptors)
            match (Seq.item 1 root.Descriptors).Describes with EntityReference.Data v -> same v (Seq.item 1 root.DataFiles) | _ -> failwith "Expected Data descriptor"
            Expect.isFalse resumed.HasSessionOnlyObjects "All shared references remain reachable"
            resumed.close() )
    testCase "value replacement covers every entity group through undo redo and resume" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        arc.save()
        let before = Path.readFileText(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)
        let root = arc.Model
        let proc = (arc.Process.list() |> Seq.head)
        let sample = (arc.Sample.list() |> Seq.head)
        let data = (arc.Data.list() |> Seq.head)
        let recipe = (arc.Recipe.list() |> Seq.head)
        let annotation = (arc.Annotation.list() |> Seq.head)
        let parameter = (arc.FormalParameter.list() |> Seq.head)
        let term = (arc.DefinedTerm.list() |> Seq.head)
        let termset = (arc.DefinedTermSet.list() |> Seq.head)
        let descriptor = (arc.Descriptor.list() |> Seq.head)
        let agent = (arc.Agent.list() |> Seq.head)
        let organization = (arc.Organization.list() |> Seq.head)
        let article = (arc.ScholarlyArticle.list() |> Seq.head)
        let sampleProperties = sample.AdditionalProperties
        let newDataset = Dataset(["administrative"],["replacement"],id=root.Id.Value)
        arc.Dataset.upsert(newDataset) |> ignore
        arc.Process.upsert(ARCBaseModel.Process("Replacement",id=proc.Id.Value)) |> ignore
        arc.Sample.upsert(Sample("Replacement",id=sample.Id.Value)) |> ignore
        arc.Data.upsert(Data("new.csv",id=data.Id.Value)) |> ignore
        arc.Recipe.upsert(Recipe(name="Replacement",id=recipe.Id.Value)) |> ignore
        arc.Annotation.upsert(Annotation("Replacement",value=AnnotationValue.Text "",id=annotation.Id.Value)) |> ignore
        arc.FormalParameter.upsert(FormalParameter(id=parameter.Id.Value)) |> ignore
        arc.DefinedTerm.upsert(DefinedTerm("Replacement",inDefinedTermSet=DefinedTermSetReference.Url "ontology-url",id=term.Id.Value)) |> ignore
        arc.DefinedTermSet.upsert(DefinedTermSet("Replacement",id=termset.Id.Value)) |> ignore
        arc.Descriptor.upsert(Descriptor(EntityReference.Data data,id=descriptor.Id.Value)) |> ignore
        arc.Agent.upsert(Agent("Replacement",id=agent.Id.Value)) |> ignore
        arc.Organization.upsert(Organization("Replacement",id=organization.Id.Value)) |> ignore
        arc.ScholarlyArticle.upsert(ScholarlyArticle("Replacement",id=article.Id.Value)) |> ignore
        Expect.equal (arc.Model.Id) root.Id "The root stays the root"
        Expect.equal (arc.Process.get(proc.Id.Value)).Name "Replacement" "Replacement is stored"
        Expect.equal (Seq.length sampleProperties) 4 "Previously returned collection is unchanged"
        Expect.equal (Seq.length (arc.Sample.get(sample.Id.Value)).AdditionalProperties) 0 "Full replacement clears collections"
        Expect.equal (Seq.length root.HasParts) 2 "Previously returned root is unchanged"
        Expect.equal (Seq.length arc.Model.HasParts) 0 "Detached upsert clears relationships"
        Expect.equal (arc.Annotation.get(annotation.Id.Value)).Value (Some(AnnotationValue.Text "")) "Empty text is present"
        Expect.isNone (arc.Recipe.get(recipe.Id.Value)).IntendedUse "Replacement clears alternatives"
        Expect.isNone (arc.FormalParameter.get(parameter.Id.Value)).DefaultValue "Replacement clears reference"
        Expect.isTrue arc.HasSessionOnlyObjects "Detached entities remain registered"
        for _ in 1..13 do arc.History.undo()
        arc.save()
        Expect.equal (Path.readFileText(Path.combineNative path ARCtrl.Helper.Layout.ARCFileName)) before "All thirteen before-images restored"
        for _ in 1..13 do arc.History.redo()
        Expect.equal (arc.Sample.get(sample.Id.Value)).Name "Replacement" "Replacement applied again on redo"
        Expect.equal sample.Name "Sample" "Previously returned value is unchanged"
        arc.close()
        let resumed = ARC.openFolder(path,"sql")
        Expect.equal (resumed.Sample.get(sample.Id.Value)).Name "Replacement" "Upserts survive SQL recovery"
        for _ in 1..13 do resumed.History.undo()
        Expect.equal (Seq.length resumed.Model.HasParts) 2 "Persisted history restores full graph"
        same (Seq.item 0 resumed.Model.Agents) (Seq.item 0 (Seq.item 0 resumed.Model.Citations).Authors)
        resumed.close())

    testCase "typed alternatives and ordered association edits are reversible" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let root = arc.Model
        let proc = (Seq.item 0 root.Processes)
        let data = (Seq.item 0 root.DataFiles)
        let sample = (arc.Sample.list() |> Seq.head)
        let number = (Seq.item 0 root.AdditionalProperties)
        let recipe = proc.ExecutesRecipe.Value
        let term = (arc.DefinedTerm.list() |> Seq.head)
        let termset = (arc.DefinedTermSet.list() |> Seq.head)
        let originalComponents = recipe.Components
        let currentProcess () = arc.Process.get(proc.Id.Value)
        let currentNumber () = arc.Annotation.get(number.Id.Value)
        let currentRecipe () = arc.Recipe.get(recipe.Id.Value)
        let currentTerm () = arc.DefinedTerm.get(term.Id.Value)
        match (arc.Process.setInput(proc, Some(EntityReference.Data data))).Input with
        | Some(EntityReference.Data actual) -> Expect.equal actual.Id data.Id "Data endpoint"
        | _ -> failwith "Data endpoint"
        arc.History.undo()
        match (currentProcess()).Input with
        | Some(EntityReference.Sample actual) -> Expect.equal actual.Id sample.Id "Sample endpoint"
        | _ -> failwith "Sample endpoint"
        arc.History.redo()
        Expect.isNone (arc.Process.setInput(currentProcess(), None)).Input "Absent input"
        arc.Process.setOutput(currentProcess(), Some(EntityReference.Sample sample)) |> ignore
        arc.Process.setOutput(currentProcess(), None) |> ignore
        arc.History.undo()
        match (currentProcess()).Output with
        | Some(EntityReference.Sample actual) -> Expect.equal actual.Id sample.Id "Sample output"
        | _ -> failwith "Sample output"
        Expect.equal (arc.Annotation.setValue(number, Some(AnnotationValue.Number 0.0))).Value (Some(AnnotationValue.Number 0.0)) "Zero is present"
        Expect.equal (arc.Annotation.setValue(number, Some(AnnotationValue.Text "42"))).Value (Some(AnnotationValue.Text "42")) "Text alternative"
        Expect.isNone (arc.Annotation.setValue(number, None)).Value "Absent value"
        arc.History.undo()
        Expect.equal (currentNumber()).Value (Some(AnnotationValue.Text "42")) "Text restored"
        Expect.equal (arc.Recipe.setIntendedUse(recipe, Some(RecipeIntendedUse.Text term.Id.Value))).IntendedUse (Some(RecipeIntendedUse.Text term.Id.Value)) "Text matching an ID stays text"
        arc.Recipe.setIntendedUse(recipe, Some(RecipeIntendedUse.Term term)) |> ignore
        Expect.equal (arc.DefinedTerm.setInDefinedTermSet(term, Some(DefinedTermSetReference.Url termset.Id.Value))).InDefinedTermSet (Some(DefinedTermSetReference.Url termset.Id.Value)) "URL matching an ID stays URL"
        arc.DefinedTerm.setInDefinedTermSet(currentTerm(), Some(DefinedTermSetReference.TermSet termset)) |> ignore
        arc.Recipe.setComponents(recipe,[number;number;number]) |> ignore
        Expect.equal (Seq.length originalComponents) 2 "Previously returned collection is unchanged"
        Expect.equal (Seq.length (arc.Recipe.removeComponent(currentRecipe(),number)).Components) 2 "Remove one duplicate occurrence"
        arc.History.undo()
        Expect.equal (Seq.length (currentRecipe()).Components) 3 "Duplicate order restored"
        arc.Dataset.addPart(root,(Seq.item 0 root.HasParts)) |> ignore
        Expect.equal (Seq.length arc.Model.HasParts) 3 "Duplicate Dataset membership allowed"
        arc.Dataset.removePart(root,(Seq.item 0 root.HasParts)) |> ignore
        Expect.equal (Seq.length arc.Model.HasParts) 2 "Only one membership removed"
        arc.save()
        arc.close()
        let resumed = ARC.openFolder(path,"yml")
        Expect.equal (Seq.length resumed.Model.HasParts) 2 "Duplicate references survive YAML"
        let value = (Seq.item 0 resumed.Model.AdditionalProperties)
        Expect.equal value.Value (Some(AnnotationValue.Text "42")) "Alternative survives YAML"
        resumed.close())

    testCase "shared containment deletion preserves other owners and required references reject deletion" (fun _ ->
        let arc = ARC.create(folder(),fixture())
        let shared = (Seq.item 0 arc.Model.HasParts)
        let owner = arc.Dataset.create("other")
        arc.Dataset.addPart(owner,shared) |> ignore
        arc.Dataset.addPart(arc.Model,owner) |> ignore
        arc.Dataset.delete(owner)
        Expect.equal (arc.Dataset.get(shared.Id.Value)).Id shared.Id "Shared child survives"
        Expect.equal (Seq.length arc.Model.HasParts) 2 "Other memberships retained"
        arc.History.undo()
        Expect.equal (Seq.item 2 arc.Model.HasParts).Id owner.Id "Owner restored at its position"
        let sample = (arc.Sample.list() |> Seq.head)
        Expect.throws (fun () -> arc.Sample.delete(sample)) "Descriptor requires its target"
        Expect.equal (arc.Sample.get(sample.Id.Value)).Id sample.Id "Sample stays stored"
        arc.close())

    testCase "registration failure assigns no IDs and undo removes the inserted rows" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let value = Recipe(name="New",components=[Annotation("New component")])
        let stored = arc.Recipe.add(value)
        Expect.isNone value.Id "Supplied recipe keeps its absent ID"
        Expect.isNone (Seq.item 0 value.Components).Id "Supplied component keeps its absent ID"
        let recipeId, annotationId = stored.Id.Value, (Seq.item 0 stored.Components).Id.Value
        arc.History.undo()
        Expect.throws (fun () -> arc.Recipe.get(recipeId) |> ignore) "Inserted recipe undone"
        Expect.throws (fun () -> arc.Annotation.get(annotationId) |> ignore) "Inserted component undone"
        arc.History.redo()
        Expect.equal (arc.Recipe.get(recipeId)).Id stored.Id "Redo restores the same ID"
        Expect.equal (arc.Annotation.get(annotationId)).Name "New component" "Redo restores the component"
        let bad = Recipe(name="Bad",components=[Annotation("Pending"); Annotation("Collision",id=arc.Model.Id.Value)])
        Expect.throws (fun () -> arc.Recipe.add(bad) |> ignore) "Nested cross-type collision rejected"
        Expect.isNone bad.Id "No root ID assigned"
        Expect.isNone (Seq.item 0 bad.Components).Id "No nested ID assigned"
        arc.Recipe.setComponents(stored,[]) |> ignore
        arc.close()
        let resumed = ARC.openFolder(path,"sql")
        resumed.History.undo()
        let restored = resumed.Recipe.get(recipeId)
        Expect.equal (Seq.length restored.Components) 1 "Collection history survives restart"
        Expect.equal (Seq.item 0 restored.Components).Id (resumed.Annotation.get(annotationId)).Id "Component link restored"
        resumed.close())

    testCase "recursive Dataset and Data graphs preserve identity through SQL and YAML" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let child = (Seq.item 0 arc.Model.HasParts)
        let data = (Seq.item 0 arc.Model.DataFiles)
        arc.Dataset.addPart(child,arc.Model) |> ignore
        arc.Data.addPart(data,data) |> ignore
        arc.save()
        arc.close()
        for source in ["sql";"yml"] do
            let resumed = ARC.openFolder(path,source)
            same (Seq.item 0 (Seq.item 0 resumed.Model.HasParts).HasParts) resumed.Model
            same (Seq.item 2 (Seq.item 0 resumed.Model.DataFiles).HasParts) (Seq.item 0 resumed.Model.DataFiles)
            Expect.isFalse resumed.HasSessionOnlyObjects "Reachability terminates on cycles"
            resumed.close())

    testCase "SQL failures roll back rich upserts and incompatible repository versions are rejected" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let recipe = (Seq.item 0 arc.Model.Processes).ExecutesRecipe.Value
        let original = recipe.Components
        use sql = PolyglotSQLite.Sqlite.OpenFile(arc.DatabasePath)
        sql.ExecuteScript("CREATE TRIGGER reject_recipe BEFORE UPDATE ON recipe WHEN NEW.name='Rejected' BEGIN SELECT RAISE(ABORT,'injected'); END;")
        let component = Annotation("Pending")
        let replacement = Recipe(name="Rejected",components=[component],id=recipe.Id.Value)
        Expect.throws (fun () -> arc.Recipe.upsert(replacement) |> ignore) "SQL rejects the planned graph"
        let current = arc.Recipe.get(recipe.Id.Value)
        Expect.equal current.Name (Some "Recipe") "Stored values unchanged"
        Expect.isTrue (Seq.forall2 (fun (a: Annotation) (b: Annotation) -> a.Id = b.Id) current.Components original) "Stored collection contents unchanged"
        Expect.equal (Seq.length current.Components) 2 "Stored references preserved"
        Expect.isNone component.Id "Supplied input keeps its absent ID"
        Expect.equal (Seq.length (arc.Annotation.list())) 3 "No partial registration"
        Expect.equal (sql.Scalar("SELECT count(*) FROM history").Value.AsInteger()) 0L "No history appended"
        Expect.equal (sql.Query("PRAGMA foreign_key_check")).Count 0 "No dangling references"
        Expect.equal (sql.Scalar("PRAGMA integrity_check").Value.AsText()) "ok" "Session integrity"
        sql.Execute("DROP TRIGGER reject_recipe")
        arc.save()
        arc.close()
        sql.Execute("UPDATE repository SET schema_version=1")
        sql.Close()
        Expect.throws (fun () -> ARC.openFolder(path,"sql") |> ignore) "No silent session migration"
        Expect.throws (fun () -> ARC.openFolder(path,"auto") |> ignore) "Automatic open preserves incompatible SQL"
        Expect.throws (fun () -> ARC.openFolder(path,"yml") |> ignore) "Explicit IO selection does not replace an incompatible repository"
        use imported = ARCManagement.Session.createInMemory()
        let resumed = ARC.importFolder(imported,path)
        Expect.equal (Seq.length (Seq.item 0 resumed.Model.Processes).ExecutesRecipe.Value.Components) 2 "Independent import reads saved graph"
        resumed.close())

    testCase "adding an undone value reuses its supplied ID and a value without ID gets a new one" (fun _ ->
        let arc = ARC.create(folder(),fixture())
        let data = Data("standalone.csv")
        let first = arc.Data.add(data)
        arc.History.undo()
        Expect.isNone data.Id "Supplied input is unchanged"
        let second = arc.Data.add(data)
        Expect.notEqual second.Id first.Id "An absent ID is assigned afresh"
        Expect.isFalse arc.History.CanRedo "Registration starts a new branch"
        Expect.equal (arc.Data.get(second.Id.Value)).Path "standalone.csv" "Stored copy resolves"
        Expect.isTrue arc.HasSessionOnlyObjects "Registry remains consistent"
        arc.History.undo()
        let again = arc.Data.add(second)
        Expect.equal again.Id second.Id "A stored value keeps its supplied ID when added again"
        arc.close())
    testCase "every setter and collection member returns the updated stored value" (fun _ ->
        let arc = ARC.create(folder(),Dataset(["process-provenance";"semantic-designation";"administrative"],["root"]))
        let len (values: seq<'T>) = Seq.length values
        let note = arc.Annotation.create("note")
        let note2 = arc.Annotation.create("note2")
        let org = arc.Organization.create("org")
        let term = arc.DefinedTerm.create("term")
        let sample = arc.Sample.create("sample")
        let data = arc.Data.create("d.csv")
        let part = arc.Data.create("part")
        // Annotation, FormalParameter, Organization, DefinedTerm and DefinedTermSet
        Expect.equal (arc.Annotation.setName(note,"renamed")).Name "renamed" "Annotation name"
        Expect.equal (arc.Annotation.setValue(note,Some(AnnotationValue.Number 1.5))).Value (Some(AnnotationValue.Number 1.5)) "Annotation value"
        Expect.equal (arc.Annotation.setUnit(note,Some "u")).Unit (Some "u") "Annotation unit"
        Expect.equal (arc.Annotation.setNameTAN(note,Some "n")).NameTAN (Some "n") "Annotation name TAN"
        Expect.equal (arc.Annotation.setValueTAN(note,Some "v")).ValueTAN (Some "v") "Annotation value TAN"
        Expect.equal (arc.Annotation.setUnitTAN(note,Some "t")).UnitTAN (Some "t") "Annotation unit TAN"
        let parameter = arc.FormalParameter.create("parameter")
        Expect.equal (arc.FormalParameter.setName(parameter,Some "p")).Name (Some "p") "Parameter name"
        Expect.equal (arc.FormalParameter.setNameTAN(parameter,Some "pt")).NameTAN (Some "pt") "Parameter name TAN"
        Expect.equal (arc.FormalParameter.setDefaultValue(parameter,Some note)).DefaultValue.Value.Id note.Id "Parameter default"
        Expect.equal (arc.Annotation.setInstanceOf(note,Some parameter)).InstanceOf.Value.Id parameter.Id "Annotation instance"
        Expect.equal (arc.Organization.setName(org,"org2")).Name "org2" "Organization name"
        Expect.equal (arc.Organization.setUrl(org,Some "http://org")).Url (Some "http://org") "Organization URL"
        Expect.equal (arc.DefinedTerm.setName(term,"term2")).Name "term2" "Term name"
        Expect.equal (arc.DefinedTerm.setIdentifier(term,Some "T:1")).Identifier (Some "T:1") "Term identifier"
        Expect.equal (arc.DefinedTerm.setTAN(term,Some "tan")).TAN (Some "tan") "Term TAN"
        Expect.equal (arc.DefinedTerm.setInDefinedTermSet(term,Some(DefinedTermSetReference.Url "set"))).InDefinedTermSet (Some(DefinedTermSetReference.Url "set")) "Term set"
        let termset = arc.DefinedTermSet.create("set")
        Expect.equal (arc.DefinedTermSet.setName(termset,"set2")).Name "set2" "Term set name"
        Expect.equal (arc.DefinedTermSet.setIdentifier(termset,Some "S")).Identifier (Some "S") "Term set identifier"
        // Agent
        let agent = arc.Agent.create("agent")
        Expect.equal (arc.Agent.setName(agent,"agent2")).Name "agent2" "Agent name"
        Expect.equal (arc.Agent.setGivenName(agent,Some "g")).GivenName (Some "g") "Given name"
        Expect.equal (arc.Agent.setFamilyName(agent,Some "f")).FamilyName (Some "f") "Family name"
        Expect.equal (Seq.toList (arc.Agent.setEmails(agent,["e";"e"])).Emails) ["e";"e"] "Emails"
        Expect.equal (len (arc.Agent.setAffiliations(agent,[org;org])).Affiliations) 2 "Affiliations"
        Expect.equal (len (arc.Agent.addAffiliation(agent,org)).Affiliations) 3 "Affiliation added"
        Expect.equal (len (arc.Agent.removeAffiliation(agent,org)).Affiliations) 2 "Affiliation removed"
        Expect.equal (Seq.toList (arc.Agent.setIdentifiers(agent,["i"])).Identifiers) ["i"] "Agent identifiers"
        Expect.equal (len (arc.Agent.setAdditionalProperties(agent,[note])).AdditionalProperties) 1 "Agent properties"
        Expect.equal (len (arc.Agent.addAdditionalProperty(agent,note2)).AdditionalProperties) 2 "Agent property added"
        Expect.equal (len (arc.Agent.removeAdditionalProperty(agent,note)).AdditionalProperties) 1 "Agent property removed"
        Expect.equal (len (arc.Agent.setJobTitles(agent,[term])).JobTitles) 1 "Job titles"
        Expect.equal (len (arc.Agent.addJobTitle(agent,term)).JobTitles) 2 "Job title added"
        Expect.equal (len (arc.Agent.removeJobTitle(agent,term)).JobTitles) 1 "Job title removed"
        // ScholarlyArticle
        let article = arc.ScholarlyArticle.create("article")
        Expect.equal (arc.ScholarlyArticle.setHeadline(article,"headline")).Headline "headline" "Headline"
        Expect.equal (Seq.toList (arc.ScholarlyArticle.setIdentifiers(article,["doi"])).Identifiers) ["doi"] "Article identifiers"
        Expect.equal (len (arc.ScholarlyArticle.setAuthors(article,[agent])).Authors) 1 "Authors"
        Expect.equal (len (arc.ScholarlyArticle.addAuthor(article,agent)).Authors) 2 "Author added"
        Expect.equal (len (arc.ScholarlyArticle.removeAuthor(article,agent)).Authors) 1 "Author removed"
        Expect.equal (arc.ScholarlyArticle.setCreativeWorkStatus(article,Some term)).CreativeWorkStatus.Value.Id term.Id "Status"
        Expect.equal (len (arc.ScholarlyArticle.setAdditionalProperties(article,[note])).AdditionalProperties) 1 "Article properties"
        Expect.equal (len (arc.ScholarlyArticle.addAdditionalProperty(article,note2)).AdditionalProperties) 2 "Article property added"
        Expect.equal (len (arc.ScholarlyArticle.removeAdditionalProperty(article,note)).AdditionalProperties) 1 "Article property removed"
        // Descriptor
        let descriptor = arc.Descriptor.create(EntityReference.Sample sample)
        match (arc.Descriptor.setDescribes(descriptor,EntityReference.Data data)).Describes with
        | EntityReference.Data v -> Expect.equal v.Id data.Id "Describes"
        | _ -> failwith "Expected Data"
        Expect.equal (len (arc.Descriptor.setAnnotations(descriptor,[note])).Annotations) 1 "Annotations"
        Expect.equal (len (arc.Descriptor.addAnnotation(descriptor,note2)).Annotations) 2 "Annotation added"
        Expect.equal (len (arc.Descriptor.removeAnnotation(descriptor,note)).Annotations) 1 "Annotation removed"
        // Recipe
        let recipe = arc.Recipe.create("recipe")
        Expect.equal (arc.Recipe.setName(recipe,Some "r")).Name (Some "r") "Recipe name"
        Expect.equal (len (arc.Recipe.setParameters(recipe,[parameter])).Parameters) 1 "Parameters"
        Expect.equal (len (arc.Recipe.addParameter(recipe,parameter)).Parameters) 2 "Parameter added"
        Expect.equal (len (arc.Recipe.removeParameter(recipe,parameter)).Parameters) 1 "Parameter removed"
        Expect.equal (arc.Recipe.setDescription(recipe,Some "d")).Description (Some "d") "Recipe description"
        Expect.equal (arc.Recipe.setIntendedUse(recipe,Some(RecipeIntendedUse.Text "use"))).IntendedUse (Some(RecipeIntendedUse.Text "use")) "Intended use"
        Expect.equal (len (arc.Recipe.setAdditionalProperties(recipe,[note])).AdditionalProperties) 1 "Recipe properties"
        Expect.equal (len (arc.Recipe.addAdditionalProperty(recipe,note2)).AdditionalProperties) 2 "Recipe property added"
        Expect.equal (len (arc.Recipe.removeAdditionalProperty(recipe,note)).AdditionalProperties) 1 "Recipe property removed"
        Expect.equal (len (arc.Recipe.setComponents(recipe,[note])).Components) 1 "Components"
        Expect.equal (len (arc.Recipe.addComponent(recipe,note2)).Components) 2 "Component added"
        Expect.equal (len (arc.Recipe.removeComponent(recipe,note)).Components) 1 "Component removed"
        Expect.equal (arc.Recipe.setVersion(recipe,Some "1")).Version (Some "1") "Version"
        Expect.equal (arc.Recipe.setUrl(recipe,Some "u")).Url (Some "u") "Recipe URL"
        // Sample and Data
        Expect.equal (arc.Sample.setName(sample,"sample2")).Name "sample2" "Sample name"
        Expect.equal (len (arc.Sample.setAdditionalProperties(sample,[note])).AdditionalProperties) 1 "Sample properties"
        Expect.equal (len (arc.Sample.addAdditionalProperty(sample,note2)).AdditionalProperties) 2 "Sample property added"
        Expect.equal (len (arc.Sample.removeAdditionalProperty(sample,note)).AdditionalProperties) 1 "Sample property removed"
        Expect.equal (arc.Data.setPath(data,"e.csv")).Path "e.csv" "Path"
        Expect.equal (arc.Data.setSelector(data,Some "row=1")).Selector (Some "row=1") "Selector"
        Expect.equal (arc.Data.setSelectorFormat(data,Some "f")).SelectorFormat (Some "f") "Selector format"
        Expect.equal (arc.Data.setEncodingFormat(data,Some "text/csv")).EncodingFormat (Some "text/csv") "Encoding format"
        Expect.equal (len (arc.Data.setHasParts(data,[part])).HasParts) 1 "Data parts"
        Expect.equal (len (arc.Data.addPart(data,part)).HasParts) 2 "Data part added"
        Expect.equal (len (arc.Data.removePart(data,part)).HasParts) 1 "Data part removed"
        Expect.equal (len (arc.Data.setAdditionalProperties(data,[note])).AdditionalProperties) 1 "Data properties"
        Expect.equal (len (arc.Data.addAdditionalProperty(data,note2)).AdditionalProperties) 2 "Data property added"
        Expect.equal (len (arc.Data.removeAdditionalProperty(data,note)).AdditionalProperties) 1 "Data property removed"
        // Process
        let proc = arc.Process.create("process")
        Expect.equal (arc.Process.setName(proc,"process2")).Name "process2" "Process name"
        Expect.isSome (arc.Process.setInput(proc,Some(EntityReference.Sample sample))).Input "Input"
        Expect.isSome (arc.Process.setOutput(proc,Some(EntityReference.Data data))).Output "Output"
        Expect.equal (arc.Process.setExecutesRecipe(proc,Some recipe)).ExecutesRecipe.Value.Id recipe.Id "Recipe"
        Expect.equal (len (arc.Process.setParameterValues(proc,[note])).ParameterValues) 1 "Parameter values"
        Expect.equal (len (arc.Process.addParameterValue(proc,note2)).ParameterValues) 2 "Parameter value added"
        Expect.equal (len (arc.Process.removeParameterValue(proc,note)).ParameterValues) 1 "Parameter value removed"
        // Dataset
        let root = arc.Model
        Expect.equal (Seq.toList (arc.Dataset.setConformsTo(root,["process-provenance";"administrative"])).ConformsTo) ["process-provenance";"administrative"] "Profiles"
        Expect.equal (Seq.toList (arc.Dataset.setIdentifiers(root,["a";"b"])).Identifiers) ["a";"b"] "Identifiers"
        Expect.equal (arc.Dataset.setTitle(root,Some "t")).Title (Some "t") "Title"
        Expect.equal (arc.Dataset.setDescription(root,Some "d")).Description (Some "d") "Description"
        Expect.equal (arc.Dataset.setLicense(root,Some "l")).License (Some "l") "License"
        Expect.equal (arc.Dataset.setDatePublished(root,Some "p")).DatePublished (Some "p") "Published"
        Expect.equal (arc.Dataset.setDateCreated(root,Some "c")).DateCreated (Some "c") "Created"
        Expect.equal (arc.Dataset.setDateModified(root,Some "m")).DateModified (Some "m") "Modified"
        let child, child2 = arc.Dataset.create("child"), arc.Dataset.create("child2")
        Expect.equal (len (arc.Dataset.setHasParts(root,[child])).HasParts) 1 "Parts"
        Expect.equal (len (arc.Dataset.addPart(root,child2)).HasParts) 2 "Part added"
        Expect.equal (len (arc.Dataset.removePart(root,child)).HasParts) 1 "Part removed"
        Expect.equal (len (arc.Dataset.setDataFiles(root,[data])).DataFiles) 1 "Data files"
        Expect.equal (len (arc.Dataset.addDataFile(root,part)).DataFiles) 2 "Data file added"
        Expect.equal (len (arc.Dataset.removeDataFile(root,data)).DataFiles) 1 "Data file removed"
        Expect.equal (len (arc.Dataset.setAgents(root,[agent])).Agents) 1 "Agents"
        Expect.equal (len (arc.Dataset.addAgent(root,agent)).Agents) 2 "Agent added"
        Expect.equal (len (arc.Dataset.removeAgent(root,agent)).Agents) 1 "Agent removed"
        Expect.equal (len (arc.Dataset.setCitations(root,[article])).Citations) 1 "Citations"
        Expect.equal (len (arc.Dataset.addCitation(root,article)).Citations) 2 "Citation added"
        Expect.equal (len (arc.Dataset.removeCitation(root,article)).Citations) 1 "Citation removed"
        Expect.equal (len (arc.Dataset.setProcesses(root,[proc])).Processes) 1 "Processes"
        Expect.equal (len (arc.Dataset.addProcess(root,proc)).Processes) 2 "Process added"
        Expect.equal (len (arc.Dataset.removeProcess(root,proc)).Processes) 1 "Process removed"
        Expect.equal (len (arc.Dataset.setDescriptors(root,[descriptor])).Descriptors) 1 "Descriptors"
        Expect.equal (len (arc.Dataset.addDescriptor(root,descriptor)).Descriptors) 2 "Descriptor added"
        Expect.equal (len (arc.Dataset.removeDescriptor(root,descriptor)).Descriptors) 1 "Descriptor removed"
        Expect.equal (len (arc.Dataset.setAdditionalProperties(root,[note])).AdditionalProperties) 1 "Dataset properties"
        Expect.equal (len (arc.Dataset.addAdditionalProperty(root,note2)).AdditionalProperties) 2 "Dataset property added"
        Expect.equal (len (arc.Dataset.removeAdditionalProperty(root,note)).AdditionalProperties) 1 "Dataset property removed"
        // Entity
        let thing = arc.Entity.create("Thing")
        Expect.equal (Seq.toList (arc.Entity.setAdditionalTypes(thing,["x"])).AdditionalTypes) ["x"] "Additional types"
        Expect.equal ((arc.Entity.setProperty(thing,"k",Entity.Number 1.0)).EntityProperties.Get("k")) (Entity.Number 1.0) "Set property"
        Expect.isTrue ((arc.Entity.addProperty(thing,"j",Entity.Text "x")).EntityProperties.Contains("j")) "Add property"
        Expect.isFalse ((arc.Entity.removeProperty(thing,"k")).EntityProperties.Contains("k")) "Remove property"
        arc.Entity.delete(thing)
        Expect.throws (fun () -> arc.Entity.get(thing.Id.Value) |> ignore) "Entity deleted"
        arc.close())

    testCase "group kind and cross-kind links are enforced" (fun _ ->
        let arc = ARC.create(folder(),fixture())
        let sample = arc.Sample.list() |> Seq.head
        let data = arc.Data.list() |> Seq.head
        let proc = arc.Process.list() |> Seq.head
        let commands = arc.History.CanUndo
        let wrongProcess = ARCBaseModel.Process("p", id = sample.Id.Value)
        Expect.throws (fun () -> arc.Process.setName(wrongProcess, "renamed") |> ignore) "A subject stored as another kind is rejected"
        Expect.throws (fun () -> arc.Process.delete(wrongProcess)) "Deleting through the wrong group is rejected"
        Expect.throws (fun () -> arc.Entity.setAdditionalTypes(EntityObject("Thing", id = sample.Id.Value), ["x"]) |> ignore) "The Entity group checks the stored kind"
        Expect.equal (arc.Sample.get(sample.Id.Value)).Name "Sample" "The sample is unchanged"
        Expect.throws (fun () -> arc.Process.setInput(proc, Some(EntityReference.Data(Data("f", id = sample.Id.Value)))) |> ignore) "A Data reference to a stored Sample is rejected"
        Expect.throws (fun () -> arc.Descriptor.setDescribes(arc.Descriptor.list() |> Seq.head, EntityReference.Sample(Sample("s", id = data.Id.Value))) |> ignore) "A Sample reference to a stored Data is rejected"
        Expect.throws (fun () -> arc.Process.upsert(ARCBaseModel.Process("p2", id = "p-new", input = EntityReference.Data(Data("f", id = sample.Id.Value)))) |> ignore) "A nested link of another kind is rejected"
        Expect.throws (fun () -> arc.Entity.setProperty(arc.Model, "link", Entity.Object(EntityObject("Thing", id = sample.Id.Value))) |> ignore) "An extension link of another kind is rejected"
        Expect.equal arc.History.CanUndo commands "Failed commands add no history"
        arc.close())

    testCase "recursive upsert rejects conflicts and leaves cyclic graphs unchanged" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let undoable = arc.History.CanUndo
        let first, second = Sample("one", id = "shared"), Sample("two", id = "shared")
        let proc = ARCBaseModel.Process("p", id = "p1", input = EntityReference.Sample first, output = EntityReference.Sample second)
        let samples = Seq.length (arc.Sample.list())
        Expect.throws (fun () -> arc.Process.upsert(proc, true) |> ignore) "Conflicting definitions rejected"
        Expect.throws (fun () -> arc.Process.get("p1") |> ignore) "Nothing was written"
        Expect.equal (Seq.length (arc.Sample.list())) samples "No partial insertion"
        Expect.equal arc.History.CanUndo undoable "No history entry"
        let child = arc.Dataset.create("child")
        arc.Dataset.addPart(arc.Model, child) |> ignore
        arc.Dataset.addPart(child, arc.Model) |> ignore
        let rows = arc.DatabasePath
        let count table = withSql rows (fun sql -> sql.Query("SELECT count(*) FROM " + table).[0].Get(0).AsInteger())
        let revision = withSql rows (fun sql -> sql.Query("SELECT revision FROM arc").[0].Get(0).AsInteger())
        let history = count "history"
        arc.Dataset.upsert(arc.Model, true) |> ignore
        Expect.equal (count "history") history "The cyclic upsert writes no history"
        Expect.equal (withSql rows (fun sql -> sql.Query("SELECT revision FROM arc").[0].Get(0).AsInteger())) revision "The revision is unchanged"
        arc.close())
]
