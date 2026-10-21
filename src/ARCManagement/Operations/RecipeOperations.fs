namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type RecipeOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<Recipe>(Recipe(name), "Recipe", true,None,false)
    member _.add(value: Recipe) = session.Adopt<Recipe>(value, "Recipe", true,None,false)
    member _.upsert(value: Recipe, ?baseline: Recipe, ?recursive: bool) = session.Adopt<Recipe>(value, "Recipe", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Recipe>("Recipe",id)
    member _.list() = session.List<Recipe>("Recipe")
    member _.read(id: string) = session.Read<Recipe>("Recipe",id)
    member _.readAll() = session.ReadAll<Recipe>("Recipe")
    member _.delete(value: Recipe, ?cascade: bool) = session.Delete(session.Id(value,"Recipe"),"Recipe",defaultArg cascade false)
    member _.setName(value: Recipe, replacement: string) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"Recipe.setName")
    member _.clearName(value: Recipe) = session.Change<Recipe>(session.Id(value,"Recipe"),"name", (fun () -> None),"Recipe.clearName")
    member _.setParameters(value: Recipe, replacement: seq<FormalParameter>) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"parameters", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"FormalParameter")) |> List.ofSeq))),"Recipe.setParameters")
    member _.addParameter(value: Recipe, target: FormalParameter) = session.Collection<Recipe>(session.Id(value,"Recipe"),"parameters",target,"FormalParameter",true,"Recipe.addParameter")
    member _.removeParameter(value: Recipe, target: FormalParameter) = session.Collection<Recipe>(session.Id(value,"Recipe"),"parameters",target,"FormalParameter",false,"Recipe.removeParameter")
    member _.setDescription(value: Recipe, replacement: string) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"description", (fun () -> Some(Text(Model.required "description" replacement))),"Recipe.setDescription")
    member _.clearDescription(value: Recipe) = session.Change<Recipe>(session.Id(value,"Recipe"),"description", (fun () -> None),"Recipe.clearDescription")
    member _.clearIntendedUse(value: Recipe) = session.Change<Recipe>(session.Id(value,"Recipe"),"intendedUse", (fun () -> None),"Recipe.clearIntendedUse")
    member _.setAdditionalProperties(value: Recipe, replacement: seq<Annotation>) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"additionalProperties", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"Recipe.setAdditionalProperties")
    member _.addAdditionalProperty(value: Recipe, target: Annotation) = session.Collection<Recipe>(session.Id(value,"Recipe"),"additionalProperties",target,"Annotation",true,"Recipe.addAdditionalProperty")
    member _.removeAdditionalProperty(value: Recipe, target: Annotation) = session.Collection<Recipe>(session.Id(value,"Recipe"),"additionalProperties",target,"Annotation",false,"Recipe.removeAdditionalProperty")
    member _.setComponents(value: Recipe, replacement: seq<Annotation>) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"components", (fun () -> Some(Links(replacement |> Seq.map (fun target -> session.Id(target,"Annotation")) |> List.ofSeq))),"Recipe.setComponents")
    member _.addComponent(value: Recipe, target: Annotation) = session.Collection<Recipe>(session.Id(value,"Recipe"),"components",target,"Annotation",true,"Recipe.addComponent")
    member _.removeComponent(value: Recipe, target: Annotation) = session.Collection<Recipe>(session.Id(value,"Recipe"),"components",target,"Annotation",false,"Recipe.removeComponent")
    member _.setVersion(value: Recipe, replacement: string) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"version", (fun () -> Some(Text(Model.required "version" replacement))),"Recipe.setVersion")
    member _.clearVersion(value: Recipe) = session.Change<Recipe>(session.Id(value,"Recipe"),"version", (fun () -> None),"Recipe.clearVersion")
    member _.setUrl(value: Recipe, replacement: string) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"url", (fun () -> Some(Text(Model.required "url" replacement))),"Recipe.setUrl")
    member _.clearUrl(value: Recipe) = session.Change<Recipe>(session.Id(value,"Recipe"),"url", (fun () -> None),"Recipe.clearUrl")
    member _.setAdditionalTypes(value: Recipe, replacement: seq<string>) =
        session.Change<Recipe>(session.Id(value,"Recipe"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Recipe.setAdditionalTypes")
    member _.setIntendedUseText(value: Recipe, replacement: string) = session.Change<Recipe>(session.Id(value,"Recipe"),"intendedUse", (fun () -> Some(Text(Model.required "intendedUse" replacement))),"Recipe.setIntendedUseText")
    member _.setIntendedUseTerm(value: Recipe, replacement: DefinedTerm) = session.Reference<Recipe>(session.Id(value,"Recipe"),"intendedUse",replacement,"DefinedTerm","Recipe.setIntendedUseTerm")
