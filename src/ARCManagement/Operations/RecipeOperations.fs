namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type RecipeOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<Recipe>(revision, Recipe(name), "Recipe", true, false)
    member _.add(revision: RevisionToken, value: Recipe) = session.Adopt<Recipe>(revision, value, "Recipe", true, false)
    member _.upsert(revision: RevisionToken, value: Recipe, ?recursive: bool) = session.Adopt<Recipe>(revision, value, "Recipe", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Recipe>("Recipe",id)
    member _.list() = session.List<Recipe>("Recipe")
    member _.read(id: string) = session.Read<Recipe>("Recipe",id)
    member _.readAll() = session.ReadAll<Recipe>("Recipe")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Recipe",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Recipe>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"Recipe.setName")
    member _.clearName(revision: RevisionToken, id: string) = session.Change<Recipe>(revision,id,"name", (fun () -> None),"Recipe.clearName")
    member _.setParameters(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Recipe>(revision,id,"parameters", (fun () -> Some(Links(replacement |> List.ofSeq))),"Recipe.setParameters")
    member _.addParameter(revision: RevisionToken, id: string, target: string) = session.Collection<Recipe>(revision,id,"parameters",target,true,"Recipe.addParameter")
    member _.removeParameter(revision: RevisionToken, id: string, target: string) = session.Collection<Recipe>(revision,id,"parameters",target,false,"Recipe.removeParameter")
    member _.setDescription(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Recipe>(revision,id,"description", (fun () -> Some(Text(Model.required "description" replacement))),"Recipe.setDescription")
    member _.clearDescription(revision: RevisionToken, id: string) = session.Change<Recipe>(revision,id,"description", (fun () -> None),"Recipe.clearDescription")
    member _.clearIntendedUse(revision: RevisionToken, id: string) = session.Change<Recipe>(revision,id,"intendedUse", (fun () -> None),"Recipe.clearIntendedUse")
    member _.setAdditionalProperties(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Recipe>(revision,id,"additionalProperties", (fun () -> Some(Links(replacement |> List.ofSeq))),"Recipe.setAdditionalProperties")
    member _.addAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Recipe>(revision,id,"additionalProperties",target,true,"Recipe.addAdditionalProperty")
    member _.removeAdditionalProperty(revision: RevisionToken, id: string, target: string) = session.Collection<Recipe>(revision,id,"additionalProperties",target,false,"Recipe.removeAdditionalProperty")
    member _.setComponents(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Recipe>(revision,id,"components", (fun () -> Some(Links(replacement |> List.ofSeq))),"Recipe.setComponents")
    member _.addComponent(revision: RevisionToken, id: string, target: string) = session.Collection<Recipe>(revision,id,"components",target,true,"Recipe.addComponent")
    member _.removeComponent(revision: RevisionToken, id: string, target: string) = session.Collection<Recipe>(revision,id,"components",target,false,"Recipe.removeComponent")
    member _.setVersion(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Recipe>(revision,id,"version", (fun () -> Some(Text(Model.required "version" replacement))),"Recipe.setVersion")
    member _.clearVersion(revision: RevisionToken, id: string) = session.Change<Recipe>(revision,id,"version", (fun () -> None),"Recipe.clearVersion")
    member _.setUrl(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Recipe>(revision,id,"url", (fun () -> Some(Text(Model.required "url" replacement))),"Recipe.setUrl")
    member _.clearUrl(revision: RevisionToken, id: string) = session.Change<Recipe>(revision,id,"url", (fun () -> None),"Recipe.clearUrl")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Recipe>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Recipe.setAdditionalTypes")
    member _.setIntendedUseText(revision: RevisionToken, id: string, replacement: string) = session.Change<Recipe>(revision,id,"intendedUse", (fun () -> Some(Text(Model.required "intendedUse" replacement))),"Recipe.setIntendedUseText")
    member _.setIntendedUseTerm(revision: RevisionToken, id: string, replacement: string) = session.Change<Recipe>(revision,id,"intendedUse", (fun () -> Some(Links [replacement])),"Recipe.setIntendedUseTerm")
