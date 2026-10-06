module ManagementPrototype.Tests.FullModel

open System
open ARCtrl.Internal
open ARCBaseModel
open ARCtrl
open Fable.Pyxpecto

let private folder () =
    let path = Files.fullPath(Files.combine "build/out/management-prototype/full-model" (Guid.NewGuid().ToString("N")))
    Files.mkdir path
    path
let private same actual expected = Expect.isTrue (Object.ReferenceEquals(actual,expected)) "Canonical shared reference"
let private fixture () =
    let ontology = DefinedTermSet("Ontology",identifier="ontology",additionalTypes=["set";"set"])
    let term = DefinedTerm("Term",identifier="T:1",tan="TAN",inDefinedTermSet=DefinedTermSetReference.TermSet ontology,additionalTypes=["term"])
    let parameter = FormalParameter(name="Parameter",nameTAN="parameter-tan",additionalTypes=["parameter"])
    let number = Annotation("Number",value=AnnotationValue.Number 0.25,unit="unit",nameTAN="name-tan",valueTAN="value-tan",unitTAN="unit-tan",instanceOf=parameter,additionalTypes=["annotation"])
    parameter.DefaultValue <- Some number
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
        same arc.Model supplied
        Expect.equal (arc.Recipe.list()).Count 1 "Recipe registered"
        Expect.equal (arc.Annotation.list()).Count 3 "Shared annotations registered once"
        Expect.equal (arc.FormalParameter.list()).Count 1 "Parameter registered"
        Expect.equal (arc.DefinedTerm.list()).Count 1 "Term registered"
        Expect.equal (arc.DefinedTermSet.list()).Count 1 "Ontology registered"
        Expect.equal (arc.Agent.list()).Count 1 "Agent registered"
        Expect.equal (arc.Organization.list()).Count 1 "Organization registered"
        Expect.equal (arc.ScholarlyArticle.list()).Count 1 "Article registered"
        Expect.equal (arc.Descriptor.list()).Count 2 "Both endpoint alternatives"
        Expect.equal (arc.Data.list()).Count 2 "Nested fragments registered"
        Expect.isFalse arc.HasSessionOnlyObjects "All registered objects are reachable"
        arc.save()
        let yaml = Files.read(Files.combine path "arc.yml")
        Expect.isTrue (yaml.Contains("!!float")) "Numeric wire alternative explicit"
        Expect.isTrue (yaml.Contains("!!str")) "Text wire alternative explicit"
        arc.close()
        for source in ["sql";"yml"] do
            let resumed = ARC.openFolder(path,source)
            let root = resumed.Model
            Expect.equal root.ConformsTo.Count 3 "Profiles"
            Expect.equal (Seq.toList root.Identifiers) ["root";"root"] "Ordered duplicate identifiers"
            Expect.equal root.Title (Some "Title") "Title"
            Expect.equal root.Description (Some "Description") "Description"
            Expect.equal root.License (Some "License") "License"
            Expect.equal root.DatePublished (Some "published") "Date"
            Expect.equal root.DateCreated (Some "created") "Date"
            Expect.equal root.DateModified (Some "modified") "Date"
            same root.HasParts[0] root.HasParts[1]
            same root.Processes[0] root.Processes[1]
            same root.DataFiles[0] root.DataFiles[2]
            same root.DataFiles[0].HasParts[0] root.DataFiles[1]
            same root.DataFiles[0].HasParts[0] root.DataFiles[0].HasParts[1]
            let proc = root.Processes[0]
            let recipe = proc.ExecutesRecipe.Value
            let parameter = recipe.Parameters[0]
            let number = parameter.DefaultValue.Value
            same number.InstanceOf.Value parameter
            same proc.ParameterValues[0] number
            same recipe.Components[0] number
            same recipe.Parameters[0] recipe.Parameters[1]
            Expect.equal recipe.Description (Some "description") "Recipe description"
            Expect.equal recipe.Version (Some "1") "Version"
            Expect.equal recipe.Url (Some "recipe-url") "URL"
            Expect.equal number.Value (Some(AnnotationValue.Number 0.25)) "Fraction remains numeric"
            Expect.equal number.Unit (Some "unit") "Unit"
            Expect.equal number.NameTAN (Some "name-tan") "Name TAN"
            Expect.equal number.ValueTAN (Some "value-tan") "Value TAN"
            Expect.equal number.UnitTAN (Some "unit-tan") "Unit TAN"
            Expect.equal root.AdditionalProperties[1].Value (Some(AnnotationValue.Text "42")) "Text remains text"
            Expect.isNone root.AdditionalProperties[2].Value "Absence remains absent"
            match proc.Output with Some(EntityReference.Data v) -> same v root.DataFiles[0] | _ -> failwith "Expected Data output"
            let agent = root.Agents[0]
            same root.Agents[0] root.Agents[1]
            same root.Citations[0].Authors[0] agent
            same root.Citations[0].Authors[0] root.Citations[0].Authors[1]
            same agent.Affiliations[0] agent.Affiliations[1]
            Expect.equal (Seq.toList agent.Emails) ["a@example.org";"a@example.org"] "Emails"
            Expect.equal agent.GivenName (Some "Given") "Given name"
            Expect.equal agent.FamilyName (Some "Family") "Family name"
            Expect.equal agent.Affiliations[0].Url (Some "organization-url") "Organization URL"
            same agent.JobTitles[0] root.Citations[0].CreativeWorkStatus.Value
            match recipe.IntendedUse with
            | Some(RecipeIntendedUse.Term term) ->
                same term agent.JobTitles[0]
                Expect.equal term.TAN (Some "TAN") "Term TAN"
                match term.InDefinedTermSet with Some(DefinedTermSetReference.TermSet set) -> Expect.equal set.Identifier (Some "ontology") "Full term set" | _ -> failwith "Expected term set"
            | _ -> failwith "Expected term"
            same root.Descriptors[0] root.HasParts[0].Descriptors[0]
            match root.Descriptors[1].Describes with EntityReference.Data v -> same v root.DataFiles[1] | _ -> failwith "Expected Data descriptor"
            Expect.isFalse resumed.HasSessionOnlyObjects "All shared references remain reachable"
            resumed.close() )
    testCase "value replacement covers every entity group and retains canonical instances through undo redo and resume" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        arc.save()
        let before = Files.read(Files.combine path "arc.yml")
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
        arc.Dataset.set(newDataset)
        arc.Process.set(ARCBaseModel.Process("Replacement",id=proc.Id.Value))
        arc.Sample.set(Sample("Replacement",id=sample.Id.Value))
        arc.Data.set(Data("new.csv",id=data.Id.Value))
        arc.Recipe.set(Recipe(name="Replacement",id=recipe.Id.Value))
        arc.Annotation.set(Annotation("Replacement",value=AnnotationValue.Text "",id=annotation.Id.Value))
        arc.FormalParameter.set(FormalParameter(id=parameter.Id.Value))
        arc.DefinedTerm.set(DefinedTerm("Replacement",inDefinedTermSet=DefinedTermSetReference.Url "ontology-url",id=term.Id.Value))
        arc.DefinedTermSet.set(DefinedTermSet("Replacement",id=termset.Id.Value))
        arc.Descriptor.set(Descriptor(EntityReference.Data data,id=descriptor.Id.Value))
        arc.Agent.set(Agent("Replacement",id=agent.Id.Value))
        arc.Organization.set(Organization("Replacement",id=organization.Id.Value))
        arc.ScholarlyArticle.set(ScholarlyArticle("Replacement",id=article.Id.Value))
        same arc.Model root
        same (arc.Process.get(proc.Id.Value)) proc
        same sample.AdditionalProperties sampleProperties
        Expect.equal sample.AdditionalProperties.Count 0 "Full replacement clears collections"
        Expect.equal root.HasParts.Count 0 "Detached upsert clears relationships"
        Expect.equal annotation.Value (Some(AnnotationValue.Text "")) "Empty text is present"
        Expect.isNone recipe.IntendedUse "Replacement clears alternatives"
        Expect.isNone parameter.DefaultValue "Replacement clears reference"
        Expect.isTrue arc.HasSessionOnlyObjects "Detached entities remain registered"
        for _ in 1..13 do arc.History.undo()
        arc.save()
        Expect.equal (Files.read(Files.combine path "arc.yml")) before "All thirteen before-images restored"
        for _ in 1..13 do arc.History.redo()
        Expect.equal sample.Name "Replacement" "Canonical instance updated on redo"
        arc.close()
        let resumed = ARC.openFolder(path,"sql")
        Expect.equal (resumed.Sample.get(sample.Id.Value)).Name "Replacement" "Upserts survive SQL recovery"
        for _ in 1..13 do resumed.History.undo()
        Expect.equal resumed.Model.HasParts.Count 2 "Persisted history restores full graph"
        same resumed.Model.Agents[0] resumed.Model.Citations[0].Authors[0]
        resumed.close())

    testCase "typed alternatives and ordered association edits are reversible" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let root = arc.Model
        let proc = root.Processes[0]
        let data = root.DataFiles[0]
        let sample = (arc.Sample.list() |> Seq.head)
        let number = root.AdditionalProperties[0]
        let recipe = proc.ExecutesRecipe.Value
        let term = (arc.DefinedTerm.list() |> Seq.head)
        let termset = (arc.DefinedTermSet.list() |> Seq.head)
        let originalComponents = recipe.Components
        arc.Process.setInputData(proc,data) |> ignore
        match proc.Input with Some(EntityReference.Data actual) -> same actual data | _ -> failwith "Data endpoint"
        arc.History.undo()
        match proc.Input with Some(EntityReference.Sample actual) -> same actual sample | _ -> failwith "Sample endpoint"
        arc.History.redo()
        arc.Process.clearInput(proc) |> ignore
        Expect.isNone proc.Input "Absent input"
        arc.Process.setOutputSample(proc,sample) |> ignore
        arc.Process.clearOutput(proc) |> ignore
        arc.History.undo()
        match proc.Output with Some(EntityReference.Sample actual) -> same actual sample | _ -> failwith "Sample output"
        arc.Annotation.setValueNumber(number,0.0) |> ignore
        Expect.equal number.Value (Some(AnnotationValue.Number 0.0)) "Zero is present"
        arc.Annotation.setValueText(number,"42") |> ignore
        Expect.equal number.Value (Some(AnnotationValue.Text "42")) "Text alternative"
        arc.Annotation.clearValue(number) |> ignore
        Expect.isNone number.Value "Absent value"
        arc.History.undo()
        Expect.equal number.Value (Some(AnnotationValue.Text "42")) "Text restored"
        arc.Recipe.setIntendedUseText(recipe,term.Id.Value) |> ignore
        Expect.equal recipe.IntendedUse (Some(RecipeIntendedUse.Text term.Id.Value)) "Text matching an ID stays text"
        arc.Recipe.setIntendedUseTerm(recipe,term) |> ignore
        arc.DefinedTerm.setInDefinedTermSetUrl(term,termset.Id.Value) |> ignore
        Expect.equal term.InDefinedTermSet (Some(DefinedTermSetReference.Url termset.Id.Value)) "URL matching an ID stays URL"
        arc.DefinedTerm.setInDefinedTermSetEntity(term,termset) |> ignore
        arc.Recipe.setComponents(recipe,[number;number;number]) |> ignore
        same recipe.Components originalComponents
        arc.Recipe.removeComponent(recipe,number) |> ignore
        Expect.equal recipe.Components.Count 2 "Remove one duplicate occurrence"
        arc.History.undo()
        Expect.equal recipe.Components.Count 3 "Duplicate order restored"
        arc.Dataset.addPart(root,root.HasParts[0]) |> ignore
        Expect.equal root.HasParts.Count 3 "Duplicate Dataset membership allowed"
        Expect.throws (fun () -> arc.Dataset.movePart(root.HasParts[0],root) |> ignore) "Ambiguous move does not collapse duplicates"
        arc.Dataset.removePart(root,root.HasParts[0]) |> ignore
        Expect.equal root.HasParts.Count 2 "Only one membership removed"
        arc.save()
        arc.close()
        let resumed = ARC.openFolder(path,"yml")
        Expect.equal resumed.Model.HasParts.Count 2 "Duplicate references survive YAML"
        let value = resumed.Model.AdditionalProperties[0]
        Expect.equal value.Value (Some(AnnotationValue.Text "42")) "Alternative survives YAML"
        resumed.close())

    testCase "shared containment deletion preserves other owners and required references reject deletion" (fun _ ->
        let arc = ARC.create(folder(),fixture())
        let shared = arc.Model.HasParts[0]
        let owner = arc.Dataset.create("other")
        arc.Dataset.addPart(owner,shared) |> ignore
        arc.Dataset.addPart(arc.Model,owner) |> ignore
        arc.Dataset.delete(owner) |> ignore
        same (arc.Dataset.get(shared.Id.Value)) shared
        Expect.equal arc.Model.HasParts.Count 2 "Other memberships retained"
        arc.History.undo()
        same arc.Model.HasParts[2] owner
        let sample = (arc.Sample.list() |> Seq.head)
        Expect.throws (fun () -> arc.Sample.delete(sample) |> ignore) "Descriptor requires its target"
        same (arc.Sample.get(sample.Id.Value)) sample
        let data = (arc.Data.list() |> Seq.head)
        data.Path <- "out-of-band"
        Expect.throws (fun () -> arc.save()) "New entity types also detect direct mutation"
        arc.close())

    testCase "registration failure assigns no IDs and undo restores omitted IDs" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let value = Recipe(name="New",components=[Annotation("New component")])
        arc.Recipe.register(value) |> ignore
        let recipeId, annotationId = value.Id.Value, value.Components[0].Id.Value
        arc.History.undo()
        Expect.isNone value.Id "Assigned recipe ID undone"
        Expect.isNone value.Components[0].Id "Assigned component ID undone"
        arc.History.redo()
        same (arc.Recipe.get(recipeId)) value
        same (arc.Annotation.get(annotationId)) value.Components[0]
        let bad = Recipe(name="Bad",components=[Annotation("Pending")])
        bad.Components.Add(Annotation("Collision",id=arc.Model.Id.Value))
        Expect.throws (fun () -> arc.Recipe.register(bad) |> ignore) "Nested cross-type collision rejected"
        Expect.isNone bad.Id "No root ID assigned"
        Expect.isNone bad.Components[0].Id "No nested ID assigned"
        arc.Recipe.setComponents(value,[]) |> ignore
        arc.close()
        let resumed = ARC.openFolder(path,"sql")
        resumed.History.undo()
        let restored = resumed.Recipe.get(recipeId)
        Expect.equal restored.Components.Count 1 "Collection history survives restart"
        same restored.Components[0] (resumed.Annotation.get(annotationId))
        resumed.close())

    testCase "recursive Dataset and Data graphs preserve identity through SQL and YAML" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let child = arc.Model.HasParts[0]
        let data = arc.Model.DataFiles[0]
        arc.Dataset.addPart(child,arc.Model) |> ignore
        arc.Data.addPart(data,data) |> ignore
        arc.save()
        arc.close()
        for source in ["sql";"yml"] do
            let resumed = ARC.openFolder(path,source)
            same resumed.Model.HasParts[0].HasParts[0] resumed.Model
            same resumed.Model.DataFiles[0].HasParts[2] resumed.Model.DataFiles[0]
            Expect.isFalse resumed.HasSessionOnlyObjects "Reachability terminates on cycles"
            resumed.close())

#if !FABLE_COMPILER
    testCase "SQL failures roll back rich upserts and old session versions require explicit reload" (fun _ ->
        let path = folder()
        let arc = ARC.create(path,fixture())
        let recipe = arc.Model.Processes[0].ExecutesRecipe.Value
        let original = recipe.Components
        let options = Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
        options.DataSource <- arc.DatabasePath
        options.Pooling <- false
        use handle = new Microsoft.Data.Sqlite.SqliteConnection(options.ToString())
        handle.Open()
        use sql = PolyglotSQLite.Sqlite.WrapConnection(handle)
        sql.ExecuteScript("CREATE TRIGGER reject_recipe BEFORE INSERT ON recipe WHEN instr(NEW.payload,'Rejected')>0 BEGIN SELECT RAISE(ABORT,'injected'); END;")
        let component = Annotation("Pending")
        let replacement = Recipe(name="Rejected",components=[component],id=recipe.Id.Value)
        Expect.throws (fun () -> arc.Recipe.set(replacement)) "SQL rejects the planned graph"
        Expect.equal recipe.Name (Some "Recipe") "Canonical values restored"
        same recipe.Components original
        Expect.equal recipe.Components.Count 2 "Canonical references preserved"
        Expect.isNone component.Id "Pending ID restored"
        Expect.equal (arc.Annotation.list()).Count 3 "No partial registration"
        Expect.equal (sql.Scalar("SELECT count(*) FROM history").Value.AsInteger()) 0L "No history appended"
        Expect.equal (sql.Query("PRAGMA foreign_key_check")).Count 0 "No dangling references"
        Expect.equal (sql.Scalar("PRAGMA integrity_check").Value.AsText()) "ok" "Session integrity"
        sql.Execute("DROP TRIGGER reject_recipe")
        arc.save()
        arc.close()
        sql.Execute("UPDATE session SET schema_version=1")
        sql.Close()
        handle.Close()
        Expect.throws (fun () -> ARC.openFolder(path,"sql") |> ignore) "No silent session migration"
        Expect.throws (fun () -> ARC.openFolder(path,"auto") |> ignore) "Automatic open preserves incompatible SQL"
        let resumed = ARC.openFolder(path,"yml")
        Expect.equal resumed.Model.Processes[0].ExecutesRecipe.Value.Components.Count 2 "Explicit reload retains saved graph"
        resumed.close())
#endif

    testCase "re-registering an undone instance reuses its retained identity without aliases" (fun _ ->
        let arc = ARC.create(folder(),fixture())
        let data = Data("standalone.csv")
        arc.Data.register(data) |> ignore
        let id = data.Id.Value
        arc.History.undo()
        Expect.isNone data.Id "Assigned ID undone"
        arc.Data.register(data) |> ignore
        Expect.equal data.Id (Some id) "Same retained instance receives its reserved identity"
        Expect.isFalse arc.History.CanRedo "Registration starts a new branch"
        same (arc.Data.get(id)) data
        Expect.isTrue arc.HasSessionOnlyObjects "Registry remains consistent"
        arc.History.undo()
        data.Id <- Some "renamed"
        Expect.throws (fun () -> arc.Data.register(data) |> ignore) "Retained identity cannot alias another key"
        arc.close())

]
