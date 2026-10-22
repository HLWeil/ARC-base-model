namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type RecipeOperations internal (session: GraphContext) =
    member _.create(name: string) = let value = Recipe(name) in session.Register(value,"Recipe"); value
    member _.add(value: Recipe) = session.Register(value,"Recipe"); value
    member _.upsert(value: Recipe): unit = session.Set(value,"Recipe")
    member _.get(id: string) = session.Get<Recipe>("Recipe",id)
    member _.list() = session.List<Recipe>("Recipe")
    member _.delete(value: Recipe) = session.Delete(value)
    member _.setName(value: Recipe, replacement: string option) =
        session.Change(value,"name",replacement |> Option.map (fun replacement -> Text(Model.required "name" replacement)),"Recipe.setName")
    member _.setParameters(value: Recipe, replacement: seq<FormalParameter>) =
        session.Change(value,"parameters",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Recipe.setParameters")
    member _.addParameter(value: Recipe, target: FormalParameter) = session.Collection(value,"parameters",target,true,"Recipe.addParameter")
    member _.removeParameter(value: Recipe, target: FormalParameter) = session.Collection(value,"parameters",target,false,"Recipe.removeParameter")
    member _.setDescription(value: Recipe, replacement: string option) =
        session.Change(value,"description",replacement |> Option.map (fun replacement -> Text(Model.required "description" replacement)),"Recipe.setDescription")
    member _.setIntendedUse(value: Recipe, replacement: RecipeIntendedUse option) =
        session.Change(value,"intendedUse",replacement |> Option.map (fun replacement -> match replacement with RecipeIntendedUse.Text v -> Text(Model.required "intendedUse" v) | RecipeIntendedUse.Term v -> Links [session.Id(v)]),"Recipe.setIntendedUse")
    member _.setAdditionalProperties(value: Recipe, replacement: seq<Annotation>) =
        session.Change(value,"additionalProperties",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Recipe.setAdditionalProperties")
    member _.addAdditionalProperty(value: Recipe, target: Annotation) = session.Collection(value,"additionalProperties",target,true,"Recipe.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Recipe, target: Annotation) = session.Collection(value,"additionalProperties",target,false,"Recipe.removeAdditionalProperty")
    member _.setComponents(value: Recipe, replacement: seq<Annotation>) =
        session.Change(value,"components",Some(Links(replacement |> Seq.map (box >> session.Id) |> List.ofSeq)),"Recipe.setComponents")
    member _.addComponent(value: Recipe, target: Annotation) = session.Collection(value,"components",target,true,"Recipe.addComponent")
    member _.removeComponent(value: Recipe, target: Annotation) = session.Collection(value,"components",target,false,"Recipe.removeComponent")
    member _.setVersion(value: Recipe, replacement: string option) =
        session.Change(value,"version",replacement |> Option.map (fun replacement -> Text(Model.required "version" replacement)),"Recipe.setVersion")
    member _.setUrl(value: Recipe, replacement: string option) =
        session.Change(value,"url",replacement |> Option.map (fun replacement -> Text(Model.required "url" replacement)),"Recipe.setUrl")
