namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal


[<AttachMembers>]
type OrganizationOperations internal (session: Session) =
    member _.create(name: string) = let value = Organization(name) in session.Register(value,"Organization"); value
    member _.register(value: Organization) = session.Register(value,"Organization"); value
    member _.set(value: Organization): unit = session.Set(value,"Organization")
    member _.get(id: string) = session.Get<Organization>("Organization",id)
    member _.list() = session.List<Organization>("Organization")
    member _.delete(value: Organization) = session.Delete(value)
    member _.setName(value: Organization, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Organization.setName")
    member _.setUrl(value: Organization, replacement: string) =
        session.Change(value,"url",Some(Text(Model.required "url" replacement)),"Organization.setUrl")
    member _.clearUrl(value: Organization) = session.Change(value,"url",None,"Organization.clearUrl")
    member _.setAdditionalTypes(value: Organization, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Organization.setAdditionalTypes")


[<AttachMembers>]
type AgentOperations internal (session: Session) =
    member _.create(name: string) = let value = Agent(name) in session.Register(value,"Agent"); value
    member _.register(value: Agent) = session.Register(value,"Agent"); value
    member _.set(value: Agent): unit = session.Set(value,"Agent")
    member _.get(id: string) = session.Get<Agent>("Agent",id)
    member _.list() = session.List<Agent>("Agent")
    member _.delete(value: Agent) = session.Delete(value)
    member _.setName(value: Agent, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Agent.setName")
    member _.setGivenName(value: Agent, replacement: string) =
        session.Change(value,"givenName",Some(Text(Model.required "givenName" replacement)),"Agent.setGivenName")
    member _.clearGivenName(value: Agent) = session.Change(value,"givenName",None,"Agent.clearGivenName")
    member _.setFamilyName(value: Agent, replacement: string) =
        session.Change(value,"familyName",Some(Text(Model.required "familyName" replacement)),"Agent.setFamilyName")
    member _.clearFamilyName(value: Agent) = session.Change(value,"familyName",None,"Agent.clearFamilyName")
    member _.setEmails(value: Agent, replacement: seq<string>) =
        session.Change(value,"emails",Some(Texts(List.ofSeq replacement)),"Agent.setEmails")
    member _.setAffiliations(value: Agent, replacement: seq<Organization>) =
        session.Change(value,"affiliations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setAffiliations")
    member _.addAffiliation(value: Agent, target: Organization) = session.Collection(value,"affiliations",target,true,"Agent.addAffiliation")
    member _.removeAffiliation(value: Agent, target: Organization) = session.Collection(value,"affiliations",target,false,"Agent.removeAffiliation")
    member _.setIdentifiers(value: Agent, replacement: seq<string>) =
        session.Change(value,"identifiers",Some(Texts(List.ofSeq replacement)),"Agent.setIdentifiers")
    member _.setAdditionalProperties(value: Agent, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setAdditionalProperties")
    member _.addAdditionalProperty(value: Agent, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Agent.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Agent, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Agent.removeAdditionalProperty")
    member _.setJobTitles(value: Agent, replacement: seq<DefinedTerm>) =
        session.Change(value,"jobTitles",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Agent.setJobTitles")
    member _.addJobTitle(value: Agent, target: DefinedTerm) = session.Collection(value,"jobTitles",target,true,"Agent.addJobTitle")
    member _.removeJobTitle(value: Agent, target: DefinedTerm) = session.Collection(value,"jobTitles",target,false,"Agent.removeJobTitle")
    member _.setAdditionalTypes(value: Agent, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Agent.setAdditionalTypes")


[<AttachMembers>]
type ScholarlyArticleOperations internal (session: Session) =
    member _.create(headline: string) = let value = ScholarlyArticle(headline) in session.Register(value,"ScholarlyArticle"); value
    member _.register(value: ScholarlyArticle) = session.Register(value,"ScholarlyArticle"); value
    member _.set(value: ScholarlyArticle): unit = session.Set(value,"ScholarlyArticle")
    member _.get(id: string) = session.Get<ScholarlyArticle>("ScholarlyArticle",id)
    member _.list() = session.List<ScholarlyArticle>("ScholarlyArticle")
    member _.delete(value: ScholarlyArticle) = session.Delete(value)
    member _.setHeadline(value: ScholarlyArticle, replacement: string) =
        session.Change(value,"headline",Some(Text(Model.required "headline" replacement)),"ScholarlyArticle.setHeadline")
    member _.setIdentifiers(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change(value,"identifiers",Some(Texts(List.ofSeq replacement)),"ScholarlyArticle.setIdentifiers")
    member _.setAuthors(value: ScholarlyArticle, replacement: seq<Agent>) =
        session.Change(value,"authors",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"ScholarlyArticle.setAuthors")
    member _.addAuthor(value: ScholarlyArticle, target: Agent) = session.Collection(value,"authors",target,true,"ScholarlyArticle.addAuthor")
    member _.removeAuthor(value: ScholarlyArticle, target: Agent) = session.Collection(value,"authors",target,false,"ScholarlyArticle.removeAuthor")
    member _.setCreativeWorkStatus(value: ScholarlyArticle, replacement: DefinedTerm) =
        session.Change(value,"creativeWorkStatus",Some(Links [session.Id(replacement)]),"ScholarlyArticle.setCreativeWorkStatus")
    member _.clearCreativeWorkStatus(value: ScholarlyArticle) = session.Change(value,"creativeWorkStatus",None,"ScholarlyArticle.clearCreativeWorkStatus")
    member _.setAdditionalProperties(value: ScholarlyArticle, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"ScholarlyArticle.setAdditionalProperties")
    member _.addAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"ScholarlyArticle.addAdditionalProperty")
    member _.removeAdditionalProperty(value: ScholarlyArticle, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"ScholarlyArticle.removeAdditionalProperty")
    member _.setAdditionalTypes(value: ScholarlyArticle, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"ScholarlyArticle.setAdditionalTypes")


[<AttachMembers>]
type AnnotationOperations internal (session: Session) =
    member _.create(name: string) = let value = Annotation(name) in session.Register(value,"Annotation"); value
    member _.register(value: Annotation) = session.Register(value,"Annotation"); value
    member _.set(value: Annotation): unit = session.Set(value,"Annotation")
    member _.get(id: string) = session.Get<Annotation>("Annotation",id)
    member _.list() = session.List<Annotation>("Annotation")
    member _.delete(value: Annotation) = session.Delete(value)
    member _.setName(value: Annotation, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Annotation.setName")
    member _.setValue(value: Annotation, replacement: AnnotationValue) =
        session.Change(value,"value",Some(match replacement with AnnotationValue.Text v -> Text(Model.required "value" v) | AnnotationValue.Number v -> Number v),"Annotation.setValue")
    member _.clearValue(value: Annotation) = session.Change(value,"value",None,"Annotation.clearValue")
    member _.setUnit(value: Annotation, replacement: string) =
        session.Change(value,"unit",Some(Text(Model.required "unit" replacement)),"Annotation.setUnit")
    member _.clearUnit(value: Annotation) = session.Change(value,"unit",None,"Annotation.clearUnit")
    member _.setNameTAN(value: Annotation, replacement: string) =
        session.Change(value,"nameTAN",Some(Text(Model.required "nameTAN" replacement)),"Annotation.setNameTAN")
    member _.clearNameTAN(value: Annotation) = session.Change(value,"nameTAN",None,"Annotation.clearNameTAN")
    member _.setValueTAN(value: Annotation, replacement: string) =
        session.Change(value,"valueTAN",Some(Text(Model.required "valueTAN" replacement)),"Annotation.setValueTAN")
    member _.clearValueTAN(value: Annotation) = session.Change(value,"valueTAN",None,"Annotation.clearValueTAN")
    member _.setUnitTAN(value: Annotation, replacement: string) =
        session.Change(value,"unitTAN",Some(Text(Model.required "unitTAN" replacement)),"Annotation.setUnitTAN")
    member _.clearUnitTAN(value: Annotation) = session.Change(value,"unitTAN",None,"Annotation.clearUnitTAN")
    member _.setInstanceOf(value: Annotation, replacement: FormalParameter) =
        session.Change(value,"instanceOf",Some(Links [session.Id(replacement)]),"Annotation.setInstanceOf")
    member _.clearInstanceOf(value: Annotation) = session.Change(value,"instanceOf",None,"Annotation.clearInstanceOf")
    member _.setAdditionalTypes(value: Annotation, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Annotation.setAdditionalTypes")
    member _.setValueText(value: Annotation, replacement: string) = session.Change(value,"value",Some(Text(Model.required "value" replacement)),"Annotation.setValueText")
    member _.setValueNumber(value: Annotation, replacement: float) = session.Change(value,"value",Some(Number replacement),"Annotation.setValueNumber")


[<AttachMembers>]
type FormalParameterOperations internal (session: Session) =
    member _.create(name: string) = let value = FormalParameter(name) in session.Register(value,"FormalParameter"); value
    member _.register(value: FormalParameter) = session.Register(value,"FormalParameter"); value
    member _.set(value: FormalParameter): unit = session.Set(value,"FormalParameter")
    member _.get(id: string) = session.Get<FormalParameter>("FormalParameter",id)
    member _.list() = session.List<FormalParameter>("FormalParameter")
    member _.delete(value: FormalParameter) = session.Delete(value)
    member _.setName(value: FormalParameter, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"FormalParameter.setName")
    member _.clearName(value: FormalParameter) = session.Change(value,"name",None,"FormalParameter.clearName")
    member _.setNameTAN(value: FormalParameter, replacement: string) =
        session.Change(value,"nameTAN",Some(Text(Model.required "nameTAN" replacement)),"FormalParameter.setNameTAN")
    member _.clearNameTAN(value: FormalParameter) = session.Change(value,"nameTAN",None,"FormalParameter.clearNameTAN")
    member _.setDefaultValue(value: FormalParameter, replacement: Annotation) =
        session.Change(value,"defaultValue",Some(Links [session.Id(replacement)]),"FormalParameter.setDefaultValue")
    member _.clearDefaultValue(value: FormalParameter) = session.Change(value,"defaultValue",None,"FormalParameter.clearDefaultValue")
    member _.setAdditionalTypes(value: FormalParameter, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"FormalParameter.setAdditionalTypes")


[<AttachMembers>]
type DefinedTermSetOperations internal (session: Session) =
    member _.create(name: string) = let value = DefinedTermSet(name) in session.Register(value,"DefinedTermSet"); value
    member _.register(value: DefinedTermSet) = session.Register(value,"DefinedTermSet"); value
    member _.set(value: DefinedTermSet): unit = session.Set(value,"DefinedTermSet")
    member _.get(id: string) = session.Get<DefinedTermSet>("DefinedTermSet",id)
    member _.list() = session.List<DefinedTermSet>("DefinedTermSet")
    member _.delete(value: DefinedTermSet) = session.Delete(value)
    member _.setName(value: DefinedTermSet, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTermSet.setName")
    member _.setIdentifier(value: DefinedTermSet, replacement: string) =
        session.Change(value,"identifier",Some(Text(Model.required "identifier" replacement)),"DefinedTermSet.setIdentifier")
    member _.clearIdentifier(value: DefinedTermSet) = session.Change(value,"identifier",None,"DefinedTermSet.clearIdentifier")
    member _.setAdditionalTypes(value: DefinedTermSet, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"DefinedTermSet.setAdditionalTypes")


[<AttachMembers>]
type DefinedTermOperations internal (session: Session) =
    member _.create(name: string) = let value = DefinedTerm(name) in session.Register(value,"DefinedTerm"); value
    member _.register(value: DefinedTerm) = session.Register(value,"DefinedTerm"); value
    member _.set(value: DefinedTerm): unit = session.Set(value,"DefinedTerm")
    member _.get(id: string) = session.Get<DefinedTerm>("DefinedTerm",id)
    member _.list() = session.List<DefinedTerm>("DefinedTerm")
    member _.delete(value: DefinedTerm) = session.Delete(value)
    member _.setName(value: DefinedTerm, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"DefinedTerm.setName")
    member _.setIdentifier(value: DefinedTerm, replacement: string) =
        session.Change(value,"identifier",Some(Text(Model.required "identifier" replacement)),"DefinedTerm.setIdentifier")
    member _.clearIdentifier(value: DefinedTerm) = session.Change(value,"identifier",None,"DefinedTerm.clearIdentifier")
    member _.setTAN(value: DefinedTerm, replacement: string) =
        session.Change(value,"tan",Some(Text(Model.required "tan" replacement)),"DefinedTerm.setTAN")
    member _.clearTAN(value: DefinedTerm) = session.Change(value,"tan",None,"DefinedTerm.clearTAN")
    member _.setInDefinedTermSet(value: DefinedTerm, replacement: DefinedTermSetReference) =
        session.Change(value,"inDefinedTermSet",Some(match replacement with DefinedTermSetReference.Url v -> Text(Model.required "inDefinedTermSet" v) | DefinedTermSetReference.TermSet v -> Links [session.Id(v)]),"DefinedTerm.setInDefinedTermSet")
    member _.clearInDefinedTermSet(value: DefinedTerm) = session.Change(value,"inDefinedTermSet",None,"DefinedTerm.clearInDefinedTermSet")
    member _.setAdditionalTypes(value: DefinedTerm, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"DefinedTerm.setAdditionalTypes")
    member _.setInDefinedTermSetUrl(value: DefinedTerm, replacement: string) = session.Change(value,"inDefinedTermSet",Some(Text(Model.required "url" replacement)),"DefinedTerm.setInDefinedTermSetUrl")
    member _.setInDefinedTermSetEntity(value: DefinedTerm, replacement: DefinedTermSet) = session.Change(value,"inDefinedTermSet",Some(Links [session.Id(replacement)]),"DefinedTerm.setInDefinedTermSetEntity")


[<AttachMembers>]
type DescriptorOperations internal (session: Session) =
    member _.create(describes: EntityReference) = let value = Descriptor(describes) in session.Register(value,"Descriptor"); value
    member _.register(value: Descriptor) = session.Register(value,"Descriptor"); value
    member _.set(value: Descriptor): unit = session.Set(value,"Descriptor")
    member _.get(id: string) = session.Get<Descriptor>("Descriptor",id)
    member _.list() = session.List<Descriptor>("Descriptor")
    member _.delete(value: Descriptor) = session.Delete(value)
    member _.setDescribes(value: Descriptor, replacement: EntityReference) =
        session.Change(value,"describes",Some(Links [session.Id(Model.endpoint replacement)]),"Descriptor.setDescribes")
    member _.setAnnotations(value: Descriptor, replacement: seq<Annotation>) =
        session.Change(value,"annotations",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Descriptor.setAnnotations")
    member _.addAnnotation(value: Descriptor, target: Annotation) = session.Collection(value,"annotations",target,true,"Descriptor.addAnnotation")
    member _.removeAnnotation(value: Descriptor, target: Annotation) = session.Collection(value,"annotations",target,false,"Descriptor.removeAnnotation")
    member _.setAdditionalTypes(value: Descriptor, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Descriptor.setAdditionalTypes")
    member _.setDescribesSample(value: Descriptor, target: Sample) = session.Change(value,"describes",Some(Links [session.Id(target)]),"Descriptor.setDescribesSample")
    member _.setDescribesData(value: Descriptor, target: Data) = session.Change(value,"describes",Some(Links [session.Id(target)]),"Descriptor.setDescribesData")


[<AttachMembers>]
type DataOperations internal (session: Session) =
    member _.create(path: string) = let value = Data(path) in session.Register(value,"Data"); value
    member _.register(value: Data) = session.Register(value,"Data"); value
    member _.set(value: Data): unit = session.Set(value,"Data")
    member _.get(id: string) = session.Get<Data>("Data",id)
    member _.list() = session.List<Data>("Data")
    member _.delete(value: Data) = session.Delete(value)
    member _.setPath(value: Data, replacement: string) =
        session.Change(value,"path",Some(Text(Model.required "path" replacement)),"Data.setPath")
    member _.setSelector(value: Data, replacement: string) =
        session.Change(value,"selector",Some(Text(Model.required "selector" replacement)),"Data.setSelector")
    member _.clearSelector(value: Data) = session.Change(value,"selector",None,"Data.clearSelector")
    member _.setSelectorFormat(value: Data, replacement: string) =
        session.Change(value,"selectorFormat",Some(Text(Model.required "selectorFormat" replacement)),"Data.setSelectorFormat")
    member _.clearSelectorFormat(value: Data) = session.Change(value,"selectorFormat",None,"Data.clearSelectorFormat")
    member _.setEncodingFormat(value: Data, replacement: string) =
        session.Change(value,"encodingFormat",Some(Text(Model.required "encodingFormat" replacement)),"Data.setEncodingFormat")
    member _.clearEncodingFormat(value: Data) = session.Change(value,"encodingFormat",None,"Data.clearEncodingFormat")
    member _.setHasParts(value: Data, replacement: seq<Data>) =
        session.Change(value,"hasParts",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setHasParts")
    member _.addPart(value: Data, target: Data) = session.Collection(value,"hasParts",target,true,"Data.addPart")
    member _.removePart(value: Data, target: Data) = session.Collection(value,"hasParts",target,false,"Data.removePart")
    member _.setAdditionalProperties(value: Data, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Data.setAdditionalProperties")
    member _.addAdditionalProperty(value: Data, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Data.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Data, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Data.removeAdditionalProperty")
    member _.setAdditionalTypes(value: Data, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Data.setAdditionalTypes")


[<AttachMembers>]
type RecipeOperations internal (session: Session) =
    member _.create(name: string) = let value = Recipe(name) in session.Register(value,"Recipe"); value
    member _.register(value: Recipe) = session.Register(value,"Recipe"); value
    member _.set(value: Recipe): unit = session.Set(value,"Recipe")
    member _.get(id: string) = session.Get<Recipe>("Recipe",id)
    member _.list() = session.List<Recipe>("Recipe")
    member _.delete(value: Recipe) = session.Delete(value)
    member _.setName(value: Recipe, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Recipe.setName")
    member _.clearName(value: Recipe) = session.Change(value,"name",None,"Recipe.clearName")
    member _.setParameters(value: Recipe, replacement: seq<FormalParameter>) =
        session.Change(value,"parameters",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Recipe.setParameters")
    member _.addParameter(value: Recipe, target: FormalParameter) = session.Collection(value,"parameters",target,true,"Recipe.addParameter")
    member _.removeParameter(value: Recipe, target: FormalParameter) = session.Collection(value,"parameters",target,false,"Recipe.removeParameter")
    member _.setDescription(value: Recipe, replacement: string) =
        session.Change(value,"description",Some(Text(Model.required "description" replacement)),"Recipe.setDescription")
    member _.clearDescription(value: Recipe) = session.Change(value,"description",None,"Recipe.clearDescription")
    member _.setIntendedUse(value: Recipe, replacement: RecipeIntendedUse) =
        session.Change(value,"intendedUse",Some(match replacement with RecipeIntendedUse.Text v -> Text(Model.required "intendedUse" v) | RecipeIntendedUse.Term v -> Links [session.Id(v)]),"Recipe.setIntendedUse")
    member _.clearIntendedUse(value: Recipe) = session.Change(value,"intendedUse",None,"Recipe.clearIntendedUse")
    member _.setAdditionalProperties(value: Recipe, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Recipe.setAdditionalProperties")
    member _.addAdditionalProperty(value: Recipe, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Recipe.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Recipe, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Recipe.removeAdditionalProperty")
    member _.setComponents(value: Recipe, replacement: seq<Annotation>) =
        session.Change(value,"components",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Recipe.setComponents")
    member _.addComponent(value: Recipe, target: Annotation) = session.Collection(value,"components",target,true,"Recipe.addComponent")
    member _.removeComponent(value: Recipe, target: Annotation) = session.Collection(value,"components",target,false,"Recipe.removeComponent")
    member _.setVersion(value: Recipe, replacement: string) =
        session.Change(value,"version",Some(Text(Model.required "version" replacement)),"Recipe.setVersion")
    member _.clearVersion(value: Recipe) = session.Change(value,"version",None,"Recipe.clearVersion")
    member _.setUrl(value: Recipe, replacement: string) =
        session.Change(value,"url",Some(Text(Model.required "url" replacement)),"Recipe.setUrl")
    member _.clearUrl(value: Recipe) = session.Change(value,"url",None,"Recipe.clearUrl")
    member _.setAdditionalTypes(value: Recipe, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Recipe.setAdditionalTypes")
    member _.setIntendedUseText(value: Recipe, replacement: string) = session.Change(value,"intendedUse",Some(Text(Model.required "intendedUse" replacement)),"Recipe.setIntendedUseText")
    member _.setIntendedUseTerm(value: Recipe, replacement: DefinedTerm) = session.Change(value,"intendedUse",Some(Links [session.Id(replacement)]),"Recipe.setIntendedUseTerm")
