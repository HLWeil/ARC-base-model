namespace ARCtrl.Internal

open System
open ARCBaseModel

module internal Model =
    let required name value = if isNull (box value) then nullArg name else value
    let entityId value = value |> Option.defaultWith (fun () -> invalidOp "Entity has no session ID; register it first.")
    let kind (entity: obj) =
        match entity with
        | :? Organization as value -> value.Type
        | :? Agent as value -> value.Type
        | :? ScholarlyArticle as value -> value.Type
        | :? Annotation as value -> value.Type
        | :? FormalParameter as value -> value.Type
        | :? Dataset as value -> value.Type
        | :? DefinedTermSet as value -> value.Type
        | :? DefinedTerm as value -> value.Type
        | :? Descriptor as value -> value.Type
        | :? Sample as value -> value.Type
        | :? Data as value -> value.Type
        | :? ARCBaseModel.Process as value -> value.Type
        | :? Recipe as value -> value.Type
        | _ -> invalidArg "entity" "Unsupported base-model entity."
    let id (entity: obj) =
        match entity with
        | :? Organization as value -> value.Id
        | :? Agent as value -> value.Id
        | :? ScholarlyArticle as value -> value.Id
        | :? Annotation as value -> value.Id
        | :? FormalParameter as value -> value.Id
        | :? Dataset as value -> value.Id
        | :? DefinedTermSet as value -> value.Id
        | :? DefinedTerm as value -> value.Id
        | :? Descriptor as value -> value.Id
        | :? Sample as value -> value.Id
        | :? Data as value -> value.Id
        | :? ARCBaseModel.Process as value -> value.Id
        | :? Recipe as value -> value.Id
        | _ -> invalidArg "entity" "Unsupported base-model entity."
    let setId (entity: obj) id =
        match entity with
        | :? Organization as value -> value.Id <- id
        | :? Agent as value -> value.Id <- id
        | :? ScholarlyArticle as value -> value.Id <- id
        | :? Annotation as value -> value.Id <- id
        | :? FormalParameter as value -> value.Id <- id
        | :? Dataset as value -> value.Id <- id
        | :? DefinedTermSet as value -> value.Id <- id
        | :? DefinedTerm as value -> value.Id <- id
        | :? Descriptor as value -> value.Id <- id
        | :? Sample as value -> value.Id <- id
        | :? Data as value -> value.Id <- id
        | :? ARCBaseModel.Process as value -> value.Id <- id
        | :? Recipe as value -> value.Id <- id
        | _ -> invalidArg "entity" "Unsupported base-model entity."
    let endpoint reference = match reference with EntityReference.Sample v -> box v | EntityReference.Data v -> box v
    let capture sessionId (reference: obj -> string) (entity: obj) =
        let optional key wrap value = value |> Option.map (fun v -> key, wrap v) |> Option.toList
        let strings key values = [key, Texts(List.ofSeq values)]
        let links key values = [key, Links(values |> Seq.map (box >> reference) |> List.ofSeq)]
        let link key value = [key, Links [reference (box value)]]
        let annotation = function AnnotationValue.Text v -> Text(required "value" v) | AnnotationValue.Number v -> Number v
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
            | _ -> invalidArg "entity" "Unsupported base-model entity."
        { Id = sessionId; Kind = kind entity; SuppliedId = id entity; Properties = Map.ofList properties }
    let text key row = match Map.tryFind key row.Properties with Some(Text v) -> Some v | None -> None | _ -> invalidOp ("Expected text: " + key)
    let texts key row = match Map.tryFind key row.Properties with Some(Texts v) -> v | None -> [] | _ -> invalidOp ("Expected text collection: " + key)
    let links key row = match Map.tryFind key row.Properties with Some(Links v) -> v | None -> [] | _ -> invalidOp ("Expected entity reference: " + key)
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
        | _ -> invalidOp "Unknown entity discriminator."
    let restore (resolve: string -> obj) row (entity: obj) =
        let replace (target: ResizeArray<'T>) values = target.Clear(); target.AddRange(values)
        let one key = links key row |> List.tryHead |> Option.map resolve
        let endpoint key = one key |> Option.map (function :? Sample as v -> EntityReference.Sample v | :? Data as v -> EntityReference.Data v | _ -> invalidOp "Invalid endpoint")
        setId entity (Some row.Id)
        match entity with
        | :? Organization as v ->
            v.Name <- text "name" row |> Option.get
            v.Url <- text "url" row
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Agent as v ->
            v.Name <- text "name" row |> Option.get
            v.GivenName <- text "givenName" row
            v.FamilyName <- text "familyName" row
            replace v.Emails (texts "emails" row)
            replace v.Affiliations (links "affiliations" row |> List.map (resolve >> unbox<Organization>))
            replace v.Identifiers (texts "identifiers" row)
            replace v.AdditionalProperties (links "additionalProperties" row |> List.map (resolve >> unbox<Annotation>))
            replace v.JobTitles (links "jobTitles" row |> List.map (resolve >> unbox<DefinedTerm>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? ScholarlyArticle as v ->
            v.Headline <- text "headline" row |> Option.get
            replace v.Identifiers (texts "identifiers" row)
            replace v.Authors (links "authors" row |> List.map (resolve >> unbox<Agent>))
            v.CreativeWorkStatus <- one "creativeWorkStatus" |> Option.map unbox<DefinedTerm>
            replace v.AdditionalProperties (links "additionalProperties" row |> List.map (resolve >> unbox<Annotation>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Annotation as v ->
            v.Name <- text "name" row |> Option.get
            v.Value <- Map.tryFind "value" row.Properties |> Option.map (function Text x -> AnnotationValue.Text x | Number x -> AnnotationValue.Number x | _ -> invalidOp "Invalid Annotation value")
            v.Unit <- text "unit" row
            v.NameTAN <- text "nameTAN" row
            v.ValueTAN <- text "valueTAN" row
            v.UnitTAN <- text "unitTAN" row
            v.InstanceOf <- one "instanceOf" |> Option.map unbox<FormalParameter>
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? FormalParameter as v ->
            v.Name <- text "name" row
            v.NameTAN <- text "nameTAN" row
            v.DefaultValue <- one "defaultValue" |> Option.map unbox<Annotation>
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Dataset as v ->
            replace v.ConformsTo (texts "conformsTo" row)
            replace v.Identifiers (texts "identifiers" row)
            v.Title <- text "title" row
            v.Description <- text "description" row
            v.License <- text "license" row
            v.DatePublished <- text "datePublished" row
            v.DateCreated <- text "dateCreated" row
            v.DateModified <- text "dateModified" row
            replace v.HasParts (links "hasParts" row |> List.map (resolve >> unbox<Dataset>))
            replace v.DataFiles (links "dataFiles" row |> List.map (resolve >> unbox<Data>))
            replace v.Agents (links "agents" row |> List.map (resolve >> unbox<Agent>))
            replace v.Citations (links "citations" row |> List.map (resolve >> unbox<ScholarlyArticle>))
            replace v.Processes (links "processes" row |> List.map (resolve >> unbox<ARCBaseModel.Process>))
            replace v.Descriptors (links "descriptors" row |> List.map (resolve >> unbox<Descriptor>))
            replace v.AdditionalProperties (links "additionalProperties" row |> List.map (resolve >> unbox<Annotation>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? DefinedTermSet as v ->
            v.Name <- text "name" row |> Option.get
            v.Identifier <- text "identifier" row
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? DefinedTerm as v ->
            v.Name <- text "name" row |> Option.get
            v.Identifier <- text "identifier" row
            v.TAN <- text "tan" row
            v.InDefinedTermSet <- Map.tryFind "inDefinedTermSet" row.Properties |> Option.map (function Text x -> DefinedTermSetReference.Url x | Links [x] -> DefinedTermSetReference.TermSet(unbox (resolve x)) | _ -> invalidOp "Invalid alternative")
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Descriptor as v ->
            v.Describes <- endpoint "describes" |> Option.get
            replace v.Annotations (links "annotations" row |> List.map (resolve >> unbox<Annotation>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Sample as v ->
            v.Name <- text "name" row |> Option.get
            replace v.AdditionalProperties (links "additionalProperties" row |> List.map (resolve >> unbox<Annotation>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Data as v ->
            v.Path <- text "path" row |> Option.get
            v.Selector <- text "selector" row
            v.SelectorFormat <- text "selectorFormat" row
            v.EncodingFormat <- text "encodingFormat" row
            replace v.HasParts (links "hasParts" row |> List.map (resolve >> unbox<Data>))
            replace v.AdditionalProperties (links "additionalProperties" row |> List.map (resolve >> unbox<Annotation>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? ARCBaseModel.Process as v ->
            v.Name <- text "name" row |> Option.get
            v.Input <- endpoint "input"
            v.Output <- endpoint "output"
            v.ExecutesRecipe <- one "executesRecipe" |> Option.map unbox<Recipe>
            replace v.ParameterValues (links "parameterValues" row |> List.map (resolve >> unbox<Annotation>))
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | :? Recipe as v ->
            v.Name <- text "name" row
            replace v.Parameters (links "parameters" row |> List.map (resolve >> unbox<FormalParameter>))
            v.Description <- text "description" row
            v.IntendedUse <- Map.tryFind "intendedUse" row.Properties |> Option.map (function Text x -> RecipeIntendedUse.Text x | Links [x] -> RecipeIntendedUse.Term(unbox (resolve x)) | _ -> invalidOp "Invalid alternative")
            replace v.AdditionalProperties (links "additionalProperties" row |> List.map (resolve >> unbox<Annotation>))
            replace v.Components (links "components" row |> List.map (resolve >> unbox<Annotation>))
            v.Version <- text "version" row
            v.Url <- text "url" row
            replace v.AdditionalTypes (texts "additionalTypes" row)
        | _ -> invalidArg "entity" "Unsupported base-model entity."
    let specifications kind =
        match kind with
        | "Organization" -> ["name", "text", "", true; "url", "text", "", false; "additionalTypes", "strings", "", false]
        | "Agent" -> ["name", "text", "", true; "givenName", "text", "", false; "familyName", "text", "", false; "emails", "strings", "", false; "affiliations", "links", "Organization", false; "identifiers", "strings", "", false; "additionalProperties", "links", "Annotation", false; "jobTitles", "links", "DefinedTerm", false; "additionalTypes", "strings", "", false]
        | "ScholarlyArticle" -> ["headline", "text", "", true; "identifiers", "strings", "", false; "authors", "links", "Agent", false; "creativeWorkStatus", "link", "DefinedTerm", false; "additionalProperties", "links", "Annotation", false; "additionalTypes", "strings", "", false]
        | "Annotation" -> ["name", "text", "", true; "value", "alternative", "", false; "unit", "text", "", false; "nameTAN", "text", "", false; "valueTAN", "text", "", false; "unitTAN", "text", "", false; "instanceOf", "link", "FormalParameter", false; "additionalTypes", "strings", "", false]
        | "FormalParameter" -> ["name", "text", "", false; "nameTAN", "text", "", false; "defaultValue", "link", "Annotation", false; "additionalTypes", "strings", "", false]
        | "Dataset" -> ["conformsTo", "strings", "", true; "identifiers", "strings", "", true; "title", "text", "", false; "description", "text", "", false; "license", "text", "", false; "datePublished", "text", "", false; "dateCreated", "text", "", false; "dateModified", "text", "", false; "hasParts", "links", "Dataset", false; "dataFiles", "links", "Data", false; "agents", "links", "Agent", false; "citations", "links", "ScholarlyArticle", false; "processes", "links", "Process", false; "descriptors", "links", "Descriptor", false; "additionalProperties", "links", "Annotation", false; "additionalTypes", "strings", "", false]
        | "DefinedTermSet" -> ["name", "text", "", true; "identifier", "text", "", false; "additionalTypes", "strings", "", false]
        | "DefinedTerm" -> ["name", "text", "", true; "identifier", "text", "", false; "tan", "text", "", false; "inDefinedTermSet", "alternative", "DefinedTermSet", false; "additionalTypes", "strings", "", false]
        | "Descriptor" -> ["describes", "alternative", "Sample|Data", true; "annotations", "links", "Annotation", false; "additionalTypes", "strings", "", false]
        | "Sample" -> ["name", "text", "", true; "additionalProperties", "links", "Annotation", false; "additionalTypes", "strings", "", false]
        | "Data" -> ["path", "text", "", true; "selector", "text", "", false; "selectorFormat", "text", "", false; "encodingFormat", "text", "", false; "hasParts", "links", "Data", false; "additionalProperties", "links", "Annotation", false; "additionalTypes", "strings", "", false]
        | "Process" -> ["name", "text", "", true; "input", "alternative", "Sample|Data", false; "output", "alternative", "Sample|Data", false; "executesRecipe", "link", "Recipe", false; "parameterValues", "links", "Annotation", false; "additionalTypes", "strings", "", false]
        | "Recipe" -> ["name", "text", "", false; "parameters", "links", "FormalParameter", false; "description", "text", "", false; "intendedUse", "alternative", "DefinedTerm", false; "additionalProperties", "links", "Annotation", false; "components", "links", "Annotation", false; "version", "text", "", false; "url", "text", "", false; "additionalTypes", "strings", "", false]
        | _ -> invalidOp "Unknown entity discriminator."
    let validate state =
        let ids = state.Entities |> List.map (fun row -> row.Id)
        if (List.distinct ids).Length <> ids.Length then invalidOp "Different entities cannot share a session ID."
        let rows = state.Entities |> List.map (fun row -> row.Id, row) |> Map.ofList
        match Map.tryFind state.Root rows with
        | Some row when row.Kind = "Dataset" -> ()
        | _ -> invalidOp "Root Dataset is missing."
        for row in state.Entities do
            required "id" row.Id |> ignore
            let spec = specifications row.Kind
            for key, cell in Map.toList row.Properties do
                let _, shape, targets, _ = spec |> List.tryFind (fun (name,_,_,_) -> name = key) |> Option.defaultWith (fun () -> invalidOp ("Unknown property: " + key))
                let valid =
                    match cell with
                    | Text v -> required key v |> ignore; shape = "text" || (shape = "alternative" && targets <> "Sample|Data")
                    | Number v -> not (Double.IsNaN v || Double.IsInfinity v) && row.Kind = "Annotation" && key = "value"
                    | Texts vs ->
                        for v in vs do required key v |> ignore
                        shape = "strings"
                    | Links vs ->
                        for id in vs do
                            let target = Map.tryFind id rows |> Option.defaultWith (fun () -> invalidOp ("Unknown reference: " + id))
                            if not (targets.Split('|') |> Array.contains target.Kind) then invalidOp ("Wrong reference type: " + key)
                        shape = "links" || ((shape = "link" || shape = "alternative") && vs.Length = 1)
                if not valid then invalidOp ("Invalid property: " + key)
            for key, _, _, mandatory in spec do
                if mandatory && not (Map.containsKey key row.Properties) then invalidOp ("Missing required property: " + key)
            if row.Kind = "Dataset" then Dataset(texts "conformsTo" row, texts "identifiers" row) |> ignore
    let reachable state =
        let rows = state.Entities |> List.map (fun row -> row.Id, row) |> Map.ofList
        let rec visit seen id =
            if Set.contains id seen then seen
            else
                let seen = Set.add id seen
                rows[id].Properties |> Map.toList |> List.fold (fun seen (_,cell) ->
                    match cell with Links ids -> List.fold visit seen ids | _ -> seen) seen
        visit Set.empty state.Root
    let sessionOnly state = (reachable state).Count < state.Entities.Length
