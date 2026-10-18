namespace ARCBaseModel
open System.Collections.Generic
open Fable.Core

[<AttachMembers>]
type internal ModelHydration private () =
    static let prototypes = Dictionary<string,EntityObject>()
    static member Allocate(kind:string):EntityObject =
        let prototype =
            match prototypes.TryGetValue(kind) with
            | true,value -> value
            | _ ->
                let value:EntityObject =
                    match kind with
                    | "DefinedTermSet" -> DefinedTermSet("") :> EntityObject
                    | "DefinedTerm" -> DefinedTerm("") :> EntityObject
                    | "Annotation" -> Annotation("") :> EntityObject
                    | "FormalParameter" -> FormalParameter() :> EntityObject
                    | "Sample" -> Sample("") :> EntityObject
                    | "Data" -> Data("") :> EntityObject
                    | "Recipe" -> Recipe() :> EntityObject
                    | "Process" -> Process("") :> EntityObject
                    | "Descriptor" -> Descriptor(EntityReference.Sample(Sample(""))) :> EntityObject
                    | "Organization" -> Organization("") :> EntityObject
                    | "Agent" -> Agent("") :> EntityObject
                    | "ScholarlyArticle" -> ScholarlyArticle("") :> EntityObject
                    | "Dataset" -> Dataset(["process-provenance"],["pending"]) :> EntityObject
                    | _ -> EntityObject(kind)
                prototypes.Add(kind,value)
                value
        let value = prototype.CloneObject() |> unbox<EntityObject>
        match kind with
        | "DefinedTermSet" -> value.AssignInternal("name",null)
        | "DefinedTerm" -> value.AssignInternal("name",null)
        | "Annotation" -> value.AssignInternal("name",null)
        | "Sample" -> value.AssignInternal("name",null)
        | "Data" -> value.AssignInternal("path",null)
        | "Process" -> value.AssignInternal("name",null)
        | "Descriptor" -> value.AssignInternal("describes",null)
        | "Organization" -> value.AssignInternal("name",null)
        | "Agent" -> value.AssignInternal("name",null)
        | "ScholarlyArticle" -> value.AssignInternal("headline",null)
        | "Dataset" -> value.AssignInternal("conformsTo",box Seq.empty<string>); value.AssignInternal("identifiers",box Seq.empty<string>)
        | _ -> ()
        value
    static member SetProperty(entity:EntityObject,key:string,value:obj) = entity.AssignInternal(key,value)
    static member SetExtension(entity:EntityObject,key:string,value:Entity) = entity.SetExtensionInternal(key,value)
    static member Seal(values:seq<EntityObject>) =
        let values=Seq.toArray values
        for value in values do value.EnsureUnsealed(); value.ValidateInternal()
        for value in values do value.SealInternal()
    static member Clone(entity:EntityObject):EntityObject = entity.CloneObject() |> unbox<EntityObject>
    static member References(entity:EntityObject):seq<EntityObject> =
        let found=ResizeArray<EntityObject>()
        let add (value:EntityObject) = found.Add(value)
        match entity with
        | :? DefinedTermSet as value ->
            ()
        | :? DefinedTerm as value ->
            value.InDefinedTermSet |> Option.iter (function DefinedTermSetReference.TermSet item -> add item | _ -> ())
        | :? Annotation as value ->
            value.InstanceOf |> Option.iter (fun item -> add item)
        | :? FormalParameter as value ->
            value.DefaultValue |> Option.iter (fun item -> add item)
        | :? Sample as value ->
            for item in value.AdditionalProperties do add item
        | :? Data as value ->
            for item in value.HasParts do add item
            for item in value.AdditionalProperties do add item
        | :? Recipe as value ->
            for item in value.Parameters do add item
            value.IntendedUse |> Option.iter (function RecipeIntendedUse.Term item -> add item | _ -> ())
            for item in value.AdditionalProperties do add item
            for item in value.Components do add item
        | :? Process as value ->
            value.Input |> Option.iter (function EntityReference.Sample item -> add item | EntityReference.Data item -> add item)
            value.Output |> Option.iter (function EntityReference.Sample item -> add item | EntityReference.Data item -> add item)
            value.ExecutesRecipe |> Option.iter (fun item -> add item)
            for item in value.ParameterValues do add item
        | :? Descriptor as value ->
            match value.Describes with EntityReference.Sample item -> add item | EntityReference.Data item -> add item
            for item in value.Annotations do add item
        | :? Organization as value ->
            ()
        | :? Agent as value ->
            for item in value.Affiliations do add item
            for item in value.AdditionalProperties do add item
            for item in value.JobTitles do add item
        | :? ScholarlyArticle as value ->
            for item in value.Authors do add item
            value.CreativeWorkStatus |> Option.iter (fun item -> add item)
            for item in value.AdditionalProperties do add item
        | :? Dataset as value ->
            for item in value.HasParts do add item
            for item in value.DataFiles do add item
            for item in value.Agents do add item
            for item in value.Citations do add item
            for item in value.Processes do add item
            for item in value.Descriptors do add item
            for item in value.AdditionalProperties do add item
        | _ -> ()
        let rec extension value =
            match value with
            | Entity.Object item -> add item
            | Entity.Collection items -> for i=0 to items.Count-1 do extension (items.Get(i))
            | _ -> ()
        for key in entity.EntityProperties.Keys do extension (entity.EntityProperties.Get(key))
        found :> seq<EntityObject>
    static member Remap(entity:EntityObject,resolve:EntityObject->EntityObject) =
        entity.EnsureUnsealed()
        match entity with
        | :? DefinedTermSet as value ->
            ()
        | :? DefinedTerm as value ->
            value.AssignInternal("inDefinedTermSet",box(value.InDefinedTermSet |> Option.map (function DefinedTermSetReference.Url item -> DefinedTermSetReference.Url item | DefinedTermSetReference.TermSet item -> DefinedTermSetReference.TermSet(resolve(item) |> unbox<DefinedTermSet>))))
        | :? Annotation as value ->
            value.AssignInternal("instanceOf",box(value.InstanceOf |> Option.map (fun item -> resolve(item :> EntityObject) |> unbox<FormalParameter>)))
        | :? FormalParameter as value ->
            value.AssignInternal("defaultValue",box(value.DefaultValue |> Option.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Sample as value ->
            value.AssignInternal("additionalProperties",box(value.AdditionalProperties |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Data as value ->
            value.AssignInternal("hasParts",box(value.HasParts |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Data>)))
            value.AssignInternal("additionalProperties",box(value.AdditionalProperties |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Recipe as value ->
            value.AssignInternal("parameters",box(value.Parameters |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<FormalParameter>)))
            value.AssignInternal("intendedUse",box(value.IntendedUse |> Option.map (function RecipeIntendedUse.Text item -> RecipeIntendedUse.Text item | RecipeIntendedUse.Term item -> RecipeIntendedUse.Term(resolve(item) |> unbox<DefinedTerm>))))
            value.AssignInternal("additionalProperties",box(value.AdditionalProperties |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
            value.AssignInternal("components",box(value.Components |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Process as value ->
            value.AssignInternal("input",box(value.Input |> Option.map (function EntityReference.Sample item -> EntityReference.Sample(resolve(item) |> unbox<Sample>) | EntityReference.Data item -> EntityReference.Data(resolve(item) |> unbox<Data>))))
            value.AssignInternal("output",box(value.Output |> Option.map (function EntityReference.Sample item -> EntityReference.Sample(resolve(item) |> unbox<Sample>) | EntityReference.Data item -> EntityReference.Data(resolve(item) |> unbox<Data>))))
            value.AssignInternal("executesRecipe",box(value.ExecutesRecipe |> Option.map (fun item -> resolve(item :> EntityObject) |> unbox<Recipe>)))
            value.AssignInternal("parameterValues",box(value.ParameterValues |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Descriptor as value ->
            value.AssignInternal("describes",box((match value.Describes with EntityReference.Sample item -> EntityReference.Sample(resolve(item) |> unbox<Sample>) | EntityReference.Data item -> EntityReference.Data(resolve(item) |> unbox<Data>))))
            value.AssignInternal("annotations",box(value.Annotations |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Organization as value ->
            ()
        | :? Agent as value ->
            value.AssignInternal("affiliations",box(value.Affiliations |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Organization>)))
            value.AssignInternal("additionalProperties",box(value.AdditionalProperties |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
            value.AssignInternal("jobTitles",box(value.JobTitles |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<DefinedTerm>)))
        | :? ScholarlyArticle as value ->
            value.AssignInternal("authors",box(value.Authors |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Agent>)))
            value.AssignInternal("creativeWorkStatus",box(value.CreativeWorkStatus |> Option.map (fun item -> resolve(item :> EntityObject) |> unbox<DefinedTerm>)))
            value.AssignInternal("additionalProperties",box(value.AdditionalProperties |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | :? Dataset as value ->
            value.AssignInternal("hasParts",box(value.HasParts |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Dataset>)))
            value.AssignInternal("dataFiles",box(value.DataFiles |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Data>)))
            value.AssignInternal("agents",box(value.Agents |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Agent>)))
            value.AssignInternal("citations",box(value.Citations |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<ScholarlyArticle>)))
            value.AssignInternal("processes",box(value.Processes |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Process>)))
            value.AssignInternal("descriptors",box(value.Descriptors |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Descriptor>)))
            value.AssignInternal("additionalProperties",box(value.AdditionalProperties |> Seq.map (fun item -> resolve(item :> EntityObject) |> unbox<Annotation>)))
        | _ -> ()
        let rec extension value =
            match value with
            | Entity.Object item -> Entity.Object(resolve item)
            | Entity.Collection items -> Entity.Collection(EntityCollection(seq {for i=0 to items.Count-1 do yield extension (items.Get(i))}))
            | _ -> value
        for key in entity.EntityProperties.Keys do entity.SetExtensionInternal(key,extension (entity.EntityProperties.Get(key)))
