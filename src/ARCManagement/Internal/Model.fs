namespace ARCManagement.Internal

open ARCManagement.Helper

open System
open System.Collections.Generic
open Fable.Core
open ARCBaseModel

module internal Model =
#if FABLE_COMPILER_PYTHON
    [<Emit("type($0) in (int, float) or isinstance($0, type(__import__('fable_library.core', fromlist=['float64']).float64(0.0)))")>]
    let private validNumber (_value: float) : bool = nativeOnly
    [<Emit("float($0)")>]
    let private nativeNumber (_value: float) : float = nativeOnly
    let number value =
        if not (validNumber value) then invalidArg "value" "Number requires a numeric value, excluding Boolean values."
        nativeNumber value
#else
    let number value = value
#endif
    let required name value = if isNull (box value) then nullArg name else value
    let entityId value = value |> Option.defaultWith (fun () -> invalidOp "Entity has no ID; add it through ARC operations first.")
    let kind (entity: obj) = (unbox<EntityObject> (required "entity" entity)).Type
    let id (entity: obj) = (unbox<EntityObject> (required "entity" entity)).Id
    let setId (entity: obj) id =
        let value = unbox<EntityObject> entity
        value.Common <- { value.Common with Id = id }
    let captureExtension (reference: obj -> string) value =
        let rec capture ancestors value =
            match value with
            | Entity.Object v -> ExtensionObject(reference (box v))
            | Entity.Collection v ->
                if ancestors |> List.exists (fun existing -> obj.ReferenceEquals(existing, v)) then
                    invalidOp "Collections cannot contain themselves; use typed objects for cyclic references."
                ExtensionCollection([for i in 0 .. v.Count - 1 -> capture (v :: ancestors) (v.Get(i))])
            | Entity.Number v -> ExtensionNumber(number v)
            | Entity.Text v -> ExtensionText(required "value" v)
            | Entity.Bool v -> ExtensionBool v
            | Entity.Null _ -> ExtensionNull
            | Entity.Blob v -> ExtensionBlob v.Base64
        capture [] value
    let rec extensionReferences = function
        | ExtensionObject id -> [id]
        | ExtensionCollection values -> List.collect extensionReferences values
        | _ -> []
    let rec restoreExtension resolve = function
        | ExtensionObject id -> Entity.Object(unbox<EntityObject> (resolve id))
        | ExtensionCollection values -> Entity.Collection(EntityCollection(List.map (restoreExtension resolve) values))
        | ExtensionNumber v -> Entity.Number v
        | ExtensionText v -> Entity.Text v
        | ExtensionBool v -> Entity.Bool v
        | ExtensionNull -> Entity.Null(EntityNull())
        | ExtensionBlob v -> Entity.Blob(EntityBlob(v))
    let endpoint reference = match reference with EntityReference.Sample v -> box v | EntityReference.Data v -> box v
    let capture sessionId (reference: obj -> string) (entity: obj) =
        let optional key wrap value = value |> Option.map (fun v -> key, wrap v) |> Option.toList
        let strings key values = [key, Texts(List.ofSeq values)]
        let links key values = [key, Links(values |> Seq.map (box >> reference) |> List.ofSeq)]
        let link key value = [key, Links [reference (box value)]]
        let annotation = function AnnotationValue.Text v -> Text(required "value" v) | AnnotationValue.Number v -> Number(number v)
        let intended = function RecipeIntendedUse.Text v -> Text(required "intendedUse" v) | RecipeIntendedUse.Term v -> Links [reference (box v)]
        let termset = function DefinedTermSetReference.Url v -> Text(required "inDefinedTermSet" v) | DefinedTermSetReference.TermSet v -> Links [reference (box v)]
        let endpointCell v = Links [reference (endpoint v)]
        let properties =
            match entity with
            | :? Organization as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ optional "url" (required "url" >> Text) v.Url
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Agent as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ optional "givenName" (required "givenName" >> Text) v.GivenName
                @ optional "familyName" (required "familyName" >> Text) v.FamilyName
                @ strings "emails" v.Emails
                @ links "affiliations" v.Affiliations
                @ strings "identifiers" v.Identifiers
                @ links "additionalProperties" v.AdditionalProperties
                @ links "jobTitles" v.JobTitles
                @ strings "additionalTypes" v.AdditionalTypes
            | :? ScholarlyArticle as v ->
                []
                @ ["headline", Text(required "headline" v.Headline)]
                @ strings "identifiers" v.Identifiers
                @ links "authors" v.Authors
                @ optional "creativeWorkStatus" (fun x -> Links [reference (box x)]) v.CreativeWorkStatus
                @ links "additionalProperties" v.AdditionalProperties
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Annotation as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ optional "value" annotation v.Value
                @ optional "unit" (required "unit" >> Text) v.Unit
                @ optional "nameTAN" (required "nameTAN" >> Text) v.NameTAN
                @ optional "valueTAN" (required "valueTAN" >> Text) v.ValueTAN
                @ optional "unitTAN" (required "unitTAN" >> Text) v.UnitTAN
                @ optional "instanceOf" (fun x -> Links [reference (box x)]) v.InstanceOf
                @ strings "additionalTypes" v.AdditionalTypes
            | :? FormalParameter as v ->
                []
                @ optional "name" (required "name" >> Text) v.Name
                @ optional "nameTAN" (required "nameTAN" >> Text) v.NameTAN
                @ optional "defaultValue" (fun x -> Links [reference (box x)]) v.DefaultValue
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Dataset as v ->
                []
                @ strings "conformsTo" v.ConformsTo
                @ strings "identifiers" v.Identifiers
                @ optional "title" (required "title" >> Text) v.Title
                @ optional "description" (required "description" >> Text) v.Description
                @ optional "license" (required "license" >> Text) v.License
                @ optional "datePublished" (required "datePublished" >> Text) v.DatePublished
                @ optional "dateCreated" (required "dateCreated" >> Text) v.DateCreated
                @ optional "dateModified" (required "dateModified" >> Text) v.DateModified
                @ links "hasParts" v.HasParts
                @ links "dataFiles" v.DataFiles
                @ links "agents" v.Agents
                @ links "citations" v.Citations
                @ links "processes" v.Processes
                @ links "descriptors" v.Descriptors
                @ links "additionalProperties" v.AdditionalProperties
                @ strings "additionalTypes" v.AdditionalTypes
            | :? DefinedTermSet as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ optional "identifier" (required "identifier" >> Text) v.Identifier
                @ strings "additionalTypes" v.AdditionalTypes
            | :? DefinedTerm as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ optional "identifier" (required "identifier" >> Text) v.Identifier
                @ optional "tan" (required "tan" >> Text) v.TAN
                @ optional "inDefinedTermSet" termset v.InDefinedTermSet
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Descriptor as v ->
                []
                @ ["describes", endpointCell v.Describes]
                @ links "annotations" v.Annotations
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Sample as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ links "additionalProperties" v.AdditionalProperties
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Data as v ->
                []
                @ ["path", Text(required "path" v.Path)]
                @ optional "selector" (required "selector" >> Text) v.Selector
                @ optional "selectorFormat" (required "selectorFormat" >> Text) v.SelectorFormat
                @ optional "encodingFormat" (required "encodingFormat" >> Text) v.EncodingFormat
                @ links "hasParts" v.HasParts
                @ links "additionalProperties" v.AdditionalProperties
                @ strings "additionalTypes" v.AdditionalTypes
            | :? ARCBaseModel.Process as v ->
                []
                @ ["name", Text(required "name" v.Name)]
                @ optional "input" endpointCell v.Input
                @ optional "output" endpointCell v.Output
                @ optional "executesRecipe" (fun x -> Links [reference (box x)]) v.ExecutesRecipe
                @ links "parameterValues" v.ParameterValues
                @ strings "additionalTypes" v.AdditionalTypes
            | :? Recipe as v ->
                []
                @ optional "name" (required "name" >> Text) v.Name
                @ links "parameters" v.Parameters
                @ optional "description" (required "description" >> Text) v.Description
                @ optional "intendedUse" intended v.IntendedUse
                @ links "additionalProperties" v.AdditionalProperties
                @ links "components" v.Components
                @ optional "version" (required "version" >> Text) v.Version
                @ optional "url" (required "url" >> Text) v.Url
                @ strings "additionalTypes" v.AdditionalTypes
            | :? EntityObject as v ->
                if Schema.isCore v.Type then invalidArg "entity" "A core discriminator requires its core class."
                strings "additionalTypes" v.AdditionalTypes
            | _ -> invalidArg "entity" "Expected a typed entity object."
        let value = unbox<EntityObject> entity
        let extensions = value.EntityProperties.Keys |> Seq.map (fun key -> key, captureExtension reference (value.EntityProperties.Get(key))) |> Map.ofSeq
        { Id = sessionId; Kind = kind entity; SuppliedId = id entity; Properties = Map.ofList properties; Extensions = extensions }
    let text key (row: EntityRow) = match Map.tryFind key row.Properties with Some(Text v) -> Some v | None -> None | _ -> invalidOp ("Expected text: " + key)
    let texts key (row: EntityRow) = match Map.tryFind key row.Properties with Some(Texts v) -> v | None -> [] | _ -> invalidOp ("Expected text collection: " + key)
    let links key (row: EntityRow) = match Map.tryFind key row.Properties with Some(Links v) -> v | None -> [] | _ -> invalidOp ("Expected entity reference: " + key)
    let create row : obj =
        let name key = text key row |> Option.defaultWith (fun () -> invalidOp ("Missing " + key))
        match row.Kind with
        | "Organization" -> box (Organization(name "name"))
        | "Agent" -> box (Agent(name "name"))
        | "ScholarlyArticle" -> box (ScholarlyArticle(name "headline"))
        | "Annotation" -> box (Annotation(name "name"))
        | "FormalParameter" -> box (FormalParameter())
        | "Dataset" -> box (Dataset(texts "conformsTo" row, texts "identifiers" row))
        | "DefinedTermSet" -> box (DefinedTermSet(name "name"))
        | "DefinedTerm" -> box (DefinedTerm(name "name"))
        | "Descriptor" -> box (Descriptor(EntityReference.Sample(Sample("pending"))))
        | "Sample" -> box (Sample(name "name"))
        | "Data" -> box (Data(name "path"))
        | "Process" -> box (ARCBaseModel.Process(name "name"))
        | "Recipe" -> box (Recipe())
        | _ -> box (EntityObject(row.Kind))
    let restore (resolve: string -> obj) row (entity: obj) =
        let coll values = Construction.collection "values" values
        let many key (convert: obj -> 'T) = links key row |> List.map (resolve >> convert) |> coll
        let one key = links key row |> List.tryHead |> Option.map resolve
        let endpoint key = one key |> Option.map (function :? Sample as v -> EntityReference.Sample v | :? Data as v -> EntityReference.Data v | _ -> invalidOp "Invalid endpoint")
        // The restore code extends the bag through the model's own With, so the model bundle builds the Map.
        // On JavaScript the model and management bundles carry separate Map implementations that cannot read each other's maps.
        let properties = row.Extensions |> Map.fold (fun (bag: EntityPropertyBag) key cell -> bag.With(key, restoreExtension resolve cell)) (EntityPropertyBag.Empty())
        match entity with
        | :? Organization as v ->
            v.State <- ({ Name = text "name" row |> Option.get; Url = text "url" row } : OrganizationState)
        | :? Agent as v ->
            v.State <-
                ({ Name = text "name" row |> Option.get
                   GivenName = text "givenName" row
                   FamilyName = text "familyName" row
                   Emails = coll (texts "emails" row)
                   Affiliations = many "affiliations" unbox<Organization>
                   Identifiers = coll (texts "identifiers" row)
                   AdditionalProperties = many "additionalProperties" unbox<Annotation>
                   JobTitles = many "jobTitles" unbox<DefinedTerm> } : AgentState)
        | :? ScholarlyArticle as v ->
            v.State <-
                ({ Headline = text "headline" row |> Option.get
                   Identifiers = coll (texts "identifiers" row)
                   Authors = many "authors" unbox<Agent>
                   CreativeWorkStatus = one "creativeWorkStatus" |> Option.map unbox<DefinedTerm>
                   AdditionalProperties = many "additionalProperties" unbox<Annotation> } : ScholarlyArticleState)
        | :? Annotation as v ->
            v.State <-
                ({ Name = text "name" row |> Option.get
                   Value = Map.tryFind "value" row.Properties |> Option.map (function Text x -> AnnotationValue.Text x | Number x -> AnnotationValue.Number x | _ -> invalidOp "Invalid Annotation value")
                   Unit = text "unit" row
                   NameTAN = text "nameTAN" row
                   ValueTAN = text "valueTAN" row
                   UnitTAN = text "unitTAN" row
                   InstanceOf = one "instanceOf" |> Option.map unbox<FormalParameter> } : AnnotationState)
        | :? FormalParameter as v ->
            v.State <-
                ({ Name = text "name" row
                   NameTAN = text "nameTAN" row
                   DefaultValue = one "defaultValue" |> Option.map unbox<Annotation> } : FormalParameterState)
        | :? Dataset as v ->
            v.State <-
                ({ ConformsTo = coll (texts "conformsTo" row)
                   Identifiers = coll (texts "identifiers" row)
                   Title = text "title" row
                   Description = text "description" row
                   License = text "license" row
                   DatePublished = text "datePublished" row
                   DateCreated = text "dateCreated" row
                   DateModified = text "dateModified" row
                   HasParts = many "hasParts" unbox<Dataset>
                   DataFiles = many "dataFiles" unbox<Data>
                   Agents = many "agents" unbox<Agent>
                   Citations = many "citations" unbox<ScholarlyArticle>
                   Processes = many "processes" unbox<ARCBaseModel.Process>
                   Descriptors = many "descriptors" unbox<Descriptor>
                   AdditionalProperties = many "additionalProperties" unbox<Annotation> } : DatasetState)
        | :? DefinedTermSet as v ->
            v.State <- ({ Name = text "name" row |> Option.get; Identifier = text "identifier" row } : DefinedTermSetState)
        | :? DefinedTerm as v ->
            v.State <-
                ({ Name = text "name" row |> Option.get
                   Identifier = text "identifier" row
                   TAN = text "tan" row
                   InDefinedTermSet = Map.tryFind "inDefinedTermSet" row.Properties |> Option.map (function Text x -> DefinedTermSetReference.Url x | Links [x] -> DefinedTermSetReference.TermSet(unbox (resolve x)) | _ -> invalidOp "Invalid alternative") } : DefinedTermState)
        | :? Descriptor as v ->
            v.State <-
                ({ Describes = endpoint "describes" |> Option.get
                   Annotations = many "annotations" unbox<Annotation> } : DescriptorState)
        | :? Sample as v ->
            v.State <-
                ({ Name = text "name" row |> Option.get
                   AdditionalProperties = many "additionalProperties" unbox<Annotation> } : SampleState)
        | :? Data as v ->
            v.State <-
                ({ Path = text "path" row |> Option.get
                   Selector = text "selector" row
                   SelectorFormat = text "selectorFormat" row
                   EncodingFormat = text "encodingFormat" row
                   HasParts = many "hasParts" unbox<Data>
                   AdditionalProperties = many "additionalProperties" unbox<Annotation> } : DataState)
        | :? ARCBaseModel.Process as v ->
            v.State <-
                ({ Name = text "name" row |> Option.get
                   Input = endpoint "input"
                   Output = endpoint "output"
                   ExecutesRecipe = one "executesRecipe" |> Option.map unbox<Recipe>
                   ParameterValues = many "parameterValues" unbox<Annotation> } : ProcessState)
        | :? Recipe as v ->
            v.State <-
                ({ Name = text "name" row
                   Parameters = many "parameters" unbox<FormalParameter>
                   Description = text "description" row
                   IntendedUse = Map.tryFind "intendedUse" row.Properties |> Option.map (function Text x -> RecipeIntendedUse.Text x | Links [x] -> RecipeIntendedUse.Term(unbox (resolve x)) | _ -> invalidOp "Invalid alternative")
                   AdditionalProperties = many "additionalProperties" unbox<Annotation>
                   Components = many "components" unbox<Annotation>
                   Version = text "version" row
                   Url = text "url" row } : RecipeState)
        | :? EntityObject -> ()
        | _ -> invalidArg "entity" "Expected a typed entity object."
        (unbox<EntityObject> entity).Common <-
            ({ Id = Some row.Id
               AdditionalTypes = coll (texts "additionalTypes" row)
               Properties = properties } : EntityCommon)
    let private referenceTargets shape =
        match shape with
        | Schema.Shape.Link targets | Schema.Shape.Links targets -> targets
        | Schema.Shape.TextOrLink target -> [target]
        | Schema.Shape.SampleOrData -> ["Sample"; "Data"]
        | _ -> []
    let validate state =
        let ids = state.Entities |> List.map (fun row -> row.Id)
        if (List.distinct ids).Length <> ids.Length then invalidOp "Different entities cannot share a session ID."
        let rows = state.Entities |> List.map (fun row -> row.Id, row) |> Map.ofList
        let root = Map.tryFind state.Root rows |> Option.defaultWith (fun () -> invalidOp "Root Dataset is missing.")
        if root.Kind <> "Dataset" then invalidOp "Root Dataset is missing."
        for row in state.Entities do
            required "id" row.Id |> ignore
            for key, cell in Map.toList row.Properties do
                let shape = (Schema.tryProperty row.Kind key |> Option.defaultWith (fun () -> invalidOp ("Unknown property: " + key))).Shape
                let valid =
                    match cell with
                    | Text v ->
                        required key v |> ignore
                        (match shape with Schema.Shape.Text | Schema.Shape.TextOrNumber | Schema.Shape.TextOrLink _ -> true | _ -> false)
                    | Number v -> not (Double.IsNaN v || Double.IsInfinity v) && shape = Schema.Shape.TextOrNumber
                    | Texts vs ->
                        for v in vs do required key v |> ignore
                        shape = Schema.Shape.Texts
                    | Links vs ->
                        for id in vs do
                            let target = Map.tryFind id rows |> Option.defaultWith (fun () -> invalidOp ("Unknown reference: " + id))
                            if not (referenceTargets shape |> List.contains target.Kind) then invalidOp ("Wrong reference type: " + key)
                        (match shape with
                         | Schema.Shape.Links _ -> true
                         | Schema.Shape.Link _ | Schema.Shape.TextOrLink _ | Schema.Shape.SampleOrData -> vs.Length = 1
                         | _ -> false)
                if not valid then invalidOp ("Invalid property: " + key)
            let probe = unbox<EntityObject> (create row)
            let rec validateExtension = function
                | ExtensionNumber v when Double.IsNaN v || Double.IsInfinity v -> invalidOp "Non-finite extension number."
                | ExtensionObject id when not (Map.containsKey id rows) -> invalidOp ("Unknown extension reference: " + id)
                | ExtensionCollection values -> List.iter validateExtension values
                | ExtensionBlob v -> EntityBlob(v) |> ignore
                | _ -> ()
            for KeyValue(key, cell) in row.Extensions do
                if key = "type" || key = "id" || (Schema.tryProperty row.Kind key).IsSome then
                    invalidArg "key" "Reserved core property name."
                EntityObject.withProperty(probe, key, Entity.Null(EntityNull())) |> ignore
                validateExtension cell
            for property in Schema.properties row.Kind do
                if property.Required && not (Map.containsKey property.Name row.Properties) then invalidOp ("Missing required property: " + property.Name)
            if row.Kind = "Dataset" then Dataset(texts "conformsTo" row, texts "identifiers" row) |> ignore
    let reachable state =
        let rows = state.Entities |> List.map (fun row -> row.Id, row) |> Map.ofList
        let rec visit seen id =
            if Set.contains id seen then seen
            else
                let seen = Set.add id seen
                let row = rows[id]
                let seen = row.Properties |> Map.toList |> List.fold (fun seen (_,cell) ->
                    match cell with Links ids -> List.fold visit seen ids | _ -> seen) seen
                row.Extensions |> Map.toList |> List.collect (snd >> extensionReferences) |> List.fold visit seen
        visit Set.empty state.Root
    let sessionOnly state = (reachable state).Count < state.Entities.Length

/// Adoption copies a submitted object graph into rows. The submitted objects stay unchanged and missing IDs
/// are assigned only to the rows.
module internal Adoption =
    /// Returns the next state and the ID of the submitted root. A strict adoption rejects a root ID that exists.
    /// Without recursion, nested objects whose supplied ID exists in the state are links and are not written.
    let adopt (state: State) (accepts: string -> bool) (submitted: obj) (strict: bool) (recursive: bool) : State * string =
        Model.required "entity" submitted |> ignore
        if not (accepts (Model.kind submitted)) then invalidArg "entity" "Wrong entity type."
        let existing = Dictionary<string, EntityRow>()
        for row in state.Entities do existing[row.Id] <- row
        let ids = Dictionary<obj, string>(HashIdentity.Reference)
        let written = Dictionary<string, EntityRow>()
        let inserted = ResizeArray<string>()
        let rec visit (isRoot: bool) (value: obj) : string =
            Model.required "entity" value |> ignore
            match ids.TryGetValue value with
            | true, id -> id
            | _ ->
                let supplied = Model.id value
                let stored =
                    match supplied with
                    | Some id -> (match existing.TryGetValue id with | true, row -> Some row | _ -> None)
                    | None -> None
                if isRoot && strict && stored.IsSome then invalidOp ("Entity ID already exists: " + supplied.Value)
                if not isRoot && not recursive && stored.IsSome then
                    ids[value] <- supplied.Value
                    supplied.Value
                else
                    let id = supplied |> Option.defaultWith Identifier.newId
                    ids[value] <- id
                    match stored with
                    | Some row when row.Kind <> Model.kind value -> invalidOp ("Entity ID belongs to another entity type: " + id)
                    | _ -> ()
                    let row = { Model.capture id (visit false) value with SuppliedId = Some id }
                    match written.TryGetValue id with
                    | true, previous -> if previous <> row then invalidOp ("Conflicting definitions for entity ID " + id)
                    | _ ->
                        written[id] <- row
                        if stored.IsNone then inserted.Add id
                    id
        let rootId = visit true submitted
        let replaced = state.Entities |> List.map (fun row -> match written.TryGetValue row.Id with | true, next -> next | _ -> row)
        { state with Entities = replaced @ [for id in inserted -> written[id]] }, rootId
