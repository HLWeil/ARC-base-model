module ARCBaseModel.Tests.Behavior

open System
open ARCBaseModel
open Fable.Pyxpecto

let private same actual expected message = Expect.isTrue (Object.ReferenceEquals(actual,expected)) message
let private expectFails action = Expect.throws action "Invalid operation must fail"

let tests = testList "immutable portable domain model" [
    testCase "all entity classes preserve optional IDs and fixed discriminators" <| fun _ ->
        let sample=Sample("leaf")
        let entities:EntityObject list=[DefinedTermSet("ontology");DefinedTerm("term");Annotation("value");FormalParameter();sample;Data("file");Recipe();Process("step");Descriptor(EntityReference.Sample sample);Organization("lab");Agent("Ada");ScholarlyArticle("paper");Dataset(["administrative"],["study"])]
        let kinds=["DefinedTermSet";"DefinedTerm";"Annotation";"FormalParameter";"Sample";"Data";"Recipe";"Process";"Descriptor";"Organization";"Agent";"ScholarlyArticle";"Dataset"]
        for actual,expected in List.zip entities kinds do
            Expect.equal actual.Type expected "Fixed discriminator"
            Expect.isNone actual.Id "Absent ID remains absent"
    testCase "required fields and profile cardinalities are checked on constructors and copies" <| fun _ ->
        for make in [(fun()->Sample(null)|>ignore);(fun()->Data(null)|>ignore);(fun()->Dataset([], ["a"])|>ignore);(fun()->Dataset(["administrative"],[])|>ignore);(fun()->Dataset(["custom"],["a"])|>ignore)] do expectFails make
        let value=Dataset(["process-provenance"],["a"])
        expectFails(fun()->Dataset.withIdentifiers(value,[])|>ignore)
        expectFails(fun()->Dataset.withConformsTo(value,[])|>ignore)
        expectFails(fun()->Sample.withName(Sample("A"),null)|>ignore)
        Expect.equal (Sample("").Name) "" "Empty strings preserved"
        Expect.equal (Data(" ./file ").Path) " ./file " "Paths preserved"
    testCase "scalar copies preserve original and unchanged storage references" <| fun _ ->
        let a=Annotation("A")
        let sample=Sample("old",additionalProperties=[a;a])
        let validations=ImmutableCollections.ValidationCount
        let changed=Sample.withName(sample,"new")
        Expect.equal ImmutableCollections.ValidationCount validations "Scalar copies do not revisit unchanged containers"
        Expect.equal sample.Name "old" "Original stable"
        Expect.equal changed.Name "new" "Copy changed"
        same sample.AdditionalProperties changed.AdditionalProperties "Share immutable container"
        same changed.AdditionalProperties[0] a "Reference preserved"
        same changed.AdditionalProperties[0] changed.AdditionalProperties[1] "Duplicates preserved"
        let identified=Sample.withId(sample,"same")
        Expect.isNone sample.Id "ID assignment is a pure copy"
        Expect.equal identified.Id (Some "same") "Supplied ID copied"
        Expect.isNone (Sample.clearId(identified).Id) "ID clearing"
        let child=Dataset(["administrative"],["child"])
        let root=Dataset(["process-provenance"],["root"],hasParts=Seq.replicate 2000 child)
        let validations=ImmutableCollections.ValidationCount
        let titled=Dataset.withTitle(root,"updated")
        Expect.equal ImmutableCollections.ValidationCount validations "Dataset scalar copies skip every unchanged collection"
        same titled.HasParts root.HasParts "Large immutable collection remains shared"
    testCase "all optional scalar alternatives can be cleared" <| fun _ ->
        let number=Annotation.withValue(Annotation("temperature"),AnnotationValue.Number 0.)
        Expect.equal number.Value (Some(AnnotationValue.Number 0.)) "Zero remains present"
        Expect.isNone (Annotation.clearValue(number).Value) "Clear numeric alternative"
        let text=Annotation.withValue(number,AnnotationValue.Text "")
        Expect.equal text.Value (Some(AnnotationValue.Text "")) "Empty text present"
        Expect.isNone (Recipe.clearIntendedUse(Recipe.withIntendedUse(Recipe(),RecipeIntendedUse.Text "")).IntendedUse) "Clear text alternative"
        Expect.isNone (DefinedTerm.clearInDefinedTermSet(DefinedTerm.withInDefinedTermSet(DefinedTerm("term"),DefinedTermSetReference.Url "url")).InDefinedTermSet) "Clear ontology reference"
    testCase "collection construction owns containers but preserves order duplicates and references" <| fun _ ->
        let first=Annotation("one")
        let second=Annotation("two")
        let input=ResizeArray([first;second;first])
        let sample=Sample("sample",additionalProperties=input)
        input.Clear()
        Expect.equal sample.AdditionalProperties.Count 3 "Eager owned storage"
        same sample.AdditionalProperties[0] first "First reference"
        same sample.AdditionalProperties[1] second "Second reference"
        same sample.AdditionalProperties[2] first "Duplicate reference"
        let empty=Sample.withAdditionalProperties(sample,[])
        Expect.equal empty.AdditionalProperties.Count 0 "Empty replacement clears"
        Expect.equal sample.AdditionalProperties.Count 3 "Original container stable"
    testCase "read-only list count index and F# sequence operations work on each target" <| fun _ ->
        let child=Dataset(["administrative"],["child"])
        let root=Dataset(["process-provenance"],["root"],hasParts=[child;child])
        Expect.equal root.HasParts.Count 2 "Constant time interface count"
        same root.HasParts[1] child "Interface indexing"
        Expect.equal (Seq.length root.HasParts) 2 "F# sequence traversal"
        Expect.equal ((Seq.toArray root.HasParts).Length) 2 "F# array copy"
    testCase "every profile composes with native domain alternatives" <| fun _ ->
        let annotation=Annotation("temperature",value=AnnotationValue.Number 1.25)
        let parameter=FormalParameter(defaultValue=annotation)
        let termset=DefinedTermSet("ontology")
        let term=DefinedTerm("term",inDefinedTermSet=DefinedTermSetReference.TermSet termset)
        let sample=Sample("sample",additionalProperties=[annotation])
        let data=Data("file.csv",hasParts=[Data("fragment")])
        let recipe=Recipe(parameters=[parameter],intendedUse=RecipeIntendedUse.Term term,components=[annotation])
        let step=Process("measure",input=EntityReference.Sample sample,output=EntityReference.Data data,executesRecipe=recipe,parameterValues=[annotation])
        let organization=Organization("lab",url="url")
        let agent=Agent("Ada",affiliations=[organization],jobTitles=[term])
        let article=ScholarlyArticle("paper",authors=[agent],creativeWorkStatus=term)
        let descriptor=Descriptor(EntityReference.Data data,annotations=[annotation])
        let root=Dataset(["administrative";"process-provenance";"semantic-designation"],["study"],processes=[step],agents=[agent],citations=[article],descriptors=[descriptor],dataFiles=[data])
        same root.Processes[0] step "Process reference"
        same root.Citations[0].Authors[0] agent "Administrative reference"
        same root.Descriptors[0].Annotations[0] annotation "Mixed profile sharing"
        match step.Input with Some(EntityReference.Sample value)->same value sample "Sample alternative" | _ -> failwith "Wrong alternative"
        match term.InDefinedTermSet with Some(DefinedTermSetReference.TermSet value)->same value termset "Termset alternative" | _ -> failwith "Wrong alternative"
    testCase "extension values preserve scalar distinctions and pure edits" <| fun _ ->
        let sample=Sample("sample")
        let collection=EntityCollection([Entity.Number 0.;Entity.Bool false;Entity.Text "";Entity.Object sample;Entity.Null(EntityNull());Entity.Blob(EntityBlob("AA=="))])
        let original=EntityObject("QualityAssessment")
        let edited=EntityObject.withEntityProperty(original,"values",Entity.Collection collection)
        Expect.isFalse (original.EntityProperties.Contains("values")) "Original bag unchanged"
        Expect.isTrue (edited.EntityProperties.Contains("values")) "Copy contains extension"
        Expect.equal collection.Count 6 "All alternatives retained"
        expectFails(fun()->EntityObject.withEntityProperty(original,"id",Entity.Text "bad")|>ignore)
        Expect.isFalse (EntityObject.withoutEntityProperty(edited,"values").EntityProperties.Contains("values")) "Removal is pure"
    testCase "base64 remains canonical on every target" <| fun _ ->
        for value in ["";"AA==";"AQI=";"AQID"] do Expect.equal (EntityBlob(value).Base64) value "Preserved blob"
        for value in ["A";"A===";"AB==";"AAB=";"===="] do expectFails(fun()->EntityBlob(value)|>ignore)
    testCase "typed builder creates self cycles and duplicate links" <| fun _ ->
        let builder=ModelBuilder()
        let token=builder.draft(Dataset(["process-provenance"],["root"]))
        builder.Dataset.setHasParts(token,[token;token])
        builder.Dataset.setTitle(token,"root")
        let built=builder.finish()
        let root=built.get(token)
        same root.HasParts[0] root "Self cycle"
        same root.HasParts[1] root "Duplicate self cycle"
        Expect.equal root.Title (Some "root") "Scalar builder edit"
        expectFails(fun()->builder.finish()|>ignore)
    testCase "builder preserves mutual cycles and extension aliases" <| fun _ ->
        let b=ModelBuilder()
        let root=b.draft(Dataset(["administrative"],["root"]))
        let child=b.draft(Dataset(["semantic-designation"],["child"]))
        b.Dataset.setHasParts(root,[child;child])
        b.Dataset.setHasParts(child,[root])
        b.Dataset.setEntityProperty(root,"self",ModelValue.objectValue root)
        let built=b.finish()
        let value=built.get(root)
        same value.HasParts[0] value.HasParts[1] "Sharing"
        same value.HasParts[0].HasParts[0] value "Mutual cycle"
        match value.EntityProperties.Get("self") with Entity.Object actual -> same actual value "Extension cycle" | _->failwith "Wrong extension"
    testCase "cloneGraph rewires all references while draft retains prototype links" <| fun _ ->
        let b=ModelBuilder()
        let token=b.draft(Dataset(["process-provenance"],["root"]))
        b.Dataset.setHasParts(token,[token])
        let root=b.finish().get(token)
        let shallow=Dataset.withTitle(root,"shallow")
        same shallow.HasParts[0] root "Shallow copy keeps original link"
        let edit=ModelBuilder()
        let cloned=edit.cloneGraph(root)
        edit.Dataset.setTitle(edit.draftFor(root),"changed")
        let changed=edit.finish().get(cloned)
        same changed.HasParts[0] changed "Clone rewires cycle"
        Expect.isNone root.Title "Original stable"
        Expect.equal changed.Title (Some "changed") "Edited clone"
    testCase "failed finish is correctable; foreign and reference tokens are rejected" <| fun _ ->
        let b=ModelBuilder()
        let root=b.draft(Dataset(["administrative"],["root"]))
        b.Dataset.setIdentifiers(root,[])
        expectFails(fun()->b.finish()|>ignore)
        b.Dataset.setIdentifiers(root,["fixed"])
        Expect.equal (b.finish().get(root).Identifiers[0]) "fixed" "Correction allowed"
        let owner=ModelBuilder()
        let reference=owner.reference(Sample("sample"))
        expectFails(fun()->owner.Sample.setName(reference,"bad"))
        let other=ModelBuilder()
        expectFails(fun()->other.Sample.setName(reference,"bad"))
        expectFails(fun()->other.draftFor(Sample("missing"))|>ignore)
    testCase "detached values with equal supplied IDs remain distinct" <| fun _ ->
        let first=Sample("A",id="same")
        let second=Sample("A",id="same")
        Expect.isFalse (Object.ReferenceEquals(first,second)) "No identity normalization"
        let b=ModelBuilder()
        let root=b.draft(Dataset(["process-provenance"],["root"]))
        let p1=b.draft(Process("one"))
        let p2=b.draft(Process("two"))
        b.Process.setInputSample(p1,b.reference(first))
        b.Process.setInputSample(p2,b.reference(second))
        b.Dataset.setProcesses(root,[p1;p2])
        let built=b.finish().get(root)
        Expect.equal built.Processes.Count 2 "Detached duplicate IDs preserved"
]
