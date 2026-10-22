namespace ARCManagement.Internal

/// Typed property table for the core entities. Each property records its value shape and
/// required flag. It also names the place where schemas/sql/001_core.sql stores the value.
module internal Schema =
    [<RequireQualifiedAccess>]
    type Shape =
        | Text
        | Texts
        | Link of targets: string list
        | Links of targets: string list
        | TextOrNumber
        | TextOrLink of target: string
        | SampleOrData

    /// Owner columns of associations follow the `<table>_id` rule, so the table leaves them out.
    [<RequireQualifiedAccess>]
    type Storage =
        | Column of column: string
        | TextOrNumberColumns of text: string * number: string
        | TextOrLinkColumns of text: string * link: string
        | SampleOrDataColumns of sample: string * data: string
        | Association of table: string * valueColumn: string
        | Endpoint of direction: string

    /// Names under which the YAML codec reads and writes a property. A decoder accepts every listed key.
    [<RequireQualifiedAccess>]
    type Wire =
        /// One key and decode-only aliases. A collection is always a sequence on the wire.
        | Key of key: string * aliases: string list
        /// The singular key holds one scalar or object, the plural key a sequence. Only one of them may be present.
        | OneOrMany of singular: string * plural: string
        /// Dataset identifiers. `first` holds the first item, `all` the full sequence when there is more than one.
        | FirstAndAll of first: string * all: string

    type Property = { Name: string; Shape: Shape; Required: bool; Storage: Storage; Wire: Wire }

    type EntitySpec = { Kind: string; Table: string; Properties: Property list }

    let private prop name shape required storage = { Name = name; Shape = shape; Required = required; Storage = storage; Wire = Wire.Key(name, []) }
    let private text name required column = prop name Shape.Text required (Storage.Column column)
    let private strings table name required = prop name Shape.Texts required (Storage.Association(table, "value"))
    let private links table name target column = prop name (Shape.Links [target]) false (Storage.Association(table, column))
    let private link name target column = prop name (Shape.Link [target]) false (Storage.Column column)
    let private named key (property: Property) = { property with Wire = Wire.Key(key, []) }
    let private aliased key aliases (property: Property) = { property with Wire = Wire.Key(key, aliases) }
    let private many singular plural (property: Property) = { property with Wire = Wire.OneOrMany(singular, plural) }
    let private additionalTypes table = strings (table + "_additional_type") "additionalTypes" false |> many "additionalType" "additionalTypes"
    let private entity kind table properties = { Kind = kind; Table = table; Properties = properties @ [additionalTypes table] }

    let entities : EntitySpec list = [
        entity "Organization" "organization" [
            text "name" true "name"
            text "url" false "url" ]
        entity "Agent" "agent" [
            text "name" true "name"
            text "givenName" false "given_name"
            text "familyName" false "family_name"
            strings "agent_email" "emails" false |> many "email" "emails"
            links "agent_affiliation" "affiliations" "Organization" "organization_id" |> many "affiliation" "affiliations"
            strings "agent_identifier" "identifiers" false |> many "identifier" "identifiers"
            links "agent_additional_property" "additionalProperties" "Annotation" "annotation_id" |> named "additionalProperty"
            links "agent_job_title" "jobTitles" "DefinedTerm" "defined_term_id" |> aliased "jobTitles" ["jobTitle"] ]
        entity "ScholarlyArticle" "scholarly_article" [
            text "headline" true "headline"
            strings "scholarly_article_identifier" "identifiers" false |> many "identifier" "identifiers"
            links "scholarly_article_author" "authors" "Agent" "agent_id"
            link "creativeWorkStatus" "DefinedTerm" "creative_work_status_id"
            links "scholarly_article_additional_property" "additionalProperties" "Annotation" "annotation_id" |> named "additionalProperty" ]
        entity "Annotation" "annotation" [
            text "name" true "name"
            prop "value" Shape.TextOrNumber false (Storage.TextOrNumberColumns("value_text", "value_number"))
            text "unit" false "unit"
            text "nameTAN" false "name_tan"
            text "valueTAN" false "value_tan"
            text "unitTAN" false "unit_tan"
            link "instanceOf" "FormalParameter" "instance_of_id" ]
        entity "FormalParameter" "formal_parameter" [
            text "name" false "name"
            text "nameTAN" false "name_tan"
            link "defaultValue" "Annotation" "default_value_id" ]
        entity "Dataset" "dataset" [
            strings "dataset_conforms_to" "conformsTo" true
            strings "dataset_identifier" "identifiers" true |> fun property -> { property with Wire = Wire.FirstAndAll("identifier", "identifiers") }
            text "title" false "title"
            text "description" false "description"
            text "license" false "license"
            text "datePublished" false "date_published"
            text "dateCreated" false "date_created"
            text "dateModified" false "date_modified"
            links "dataset_has_part" "hasParts" "Dataset" "part_dataset_id" |> named "hasPart"
            links "dataset_data_file" "dataFiles" "Data" "data_id"
            links "dataset_agent" "agents" "Agent" "agent_id"
            links "dataset_citation" "citations" "ScholarlyArticle" "scholarly_article_id"
            links "dataset_process" "processes" "Process" "process_id"
            links "dataset_descriptor" "descriptors" "Descriptor" "descriptor_id"
            links "dataset_additional_property" "additionalProperties" "Annotation" "annotation_id" |> named "additionalProperty" ]
        entity "DefinedTermSet" "defined_term_set" [
            text "name" true "name"
            text "identifier" false "identifier" ]
        entity "DefinedTerm" "defined_term" [
            text "name" true "name"
            text "identifier" false "identifier"
            text "tan" false "tan" |> named "TAN"
            prop "inDefinedTermSet" (Shape.TextOrLink "DefinedTermSet") false (Storage.TextOrLinkColumns("in_defined_term_set_url", "in_defined_term_set_id")) ]
        entity "Descriptor" "descriptor" [
            prop "describes" Shape.SampleOrData true (Storage.SampleOrDataColumns("sample_id", "data_id"))
            links "descriptor_annotation" "annotations" "Annotation" "annotation_id" ]
        entity "Sample" "sample" [
            text "name" true "name"
            links "sample_additional_property" "additionalProperties" "Annotation" "annotation_id" |> named "additionalProperty" ]
        entity "Data" "data" [
            text "path" true "path"
            text "selector" false "selector"
            text "selectorFormat" false "selector_format"
            text "encodingFormat" false "encoding_format"
            links "data_has_part" "hasParts" "Data" "part_data_id" |> named "hasPart"
            links "data_additional_property" "additionalProperties" "Annotation" "annotation_id" |> named "additionalProperty" ]
        entity "Process" "process" [
            text "name" true "name"
            prop "input" Shape.SampleOrData false (Storage.Endpoint "input") |> named "inputs"
            prop "output" Shape.SampleOrData false (Storage.Endpoint "output") |> named "outputs"
            link "executesRecipe" "Recipe" "executes_recipe_id" |> aliased "executesRecipe" ["executesProtocol"]
            links "process_parameter_value" "parameterValues" "Annotation" "annotation_id" |> named "parameterValue" ]
        entity "Recipe" "recipe" [
            text "name" false "name"
            links "recipe_parameter" "parameters" "FormalParameter" "formal_parameter_id"
            text "description" false "description"
            prop "intendedUse" (Shape.TextOrLink "DefinedTerm") false (Storage.TextOrLinkColumns("intended_use_text", "intended_use_id"))
            links "recipe_additional_property" "additionalProperties" "Annotation" "annotation_id" |> named "additionalProperty"
            links "recipe_component" "components" "Annotation" "annotation_id"
            text "version" false "version"
            text "url" false "url" ]
    ]

    let tryEntity kind = entities |> List.tryFind (fun spec -> spec.Kind = kind)
    let isCore kind = (tryEntity kind).IsSome
    let properties kind =
        match tryEntity kind with
        | Some spec -> spec.Properties
        | None -> [strings "entity_additional_type" "additionalTypes" false |> many "additionalType" "additionalTypes"]
    let tryProperty kind name = properties kind |> List.tryFind (fun p -> p.Name = name)

    /// Every key that selects the property on the wire, aliases included.
    let wireNames (property: Property) =
        match property.Wire with
        | Wire.Key(key, aliases) -> key :: aliases
        | Wire.OneOrMany(singular, plural) -> [singular; plural]
        | Wire.FirstAndAll(first, all) -> [first; all]

    /// Keys of an entity mapping that extension properties cannot use.
    let reservedWireKeys kind = ["id"; "type"; "@type"; "@id"] @ (properties kind |> List.collect wireNames)

    /// Wire type names that decode to a core kind. The encoder always writes the kind name.
    let private typeAliases = [
        "schema:Dataset", "Dataset"; "bioschemas:LabProcess", "Process"; "bioschemas:Sample", "Sample"
        "schema:PropertyValue", "Annotation"; "bioschemas:LabProtocol", "Recipe"
        "bioschemas:FormalParameter", "FormalParameter"; "File", "Data"; "MediaObject", "Data" ]
    let canonicalKind (name: string) = typeAliases |> List.tryPick (fun (alias, kind) -> if alias = name then Some kind else None) |> Option.defaultValue name
