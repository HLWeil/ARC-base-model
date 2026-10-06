namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

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
