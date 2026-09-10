---
title: Schema.org mapping
category: Project
categoryindex: 2
index: 6
---

# Schema.org mapping

The ARC Base Model closely follows Schema.org to support RO-Crate interoperability. This page collects recommended mappings by profile and entity, including profile conventions and mappings that depend on the target profile. The mappings are recommendations rather than strict equivalences.

Rows named after an entity describe its external type mapping. The local `type` discriminator is a separate property. Shared entities can appear in more than one profile table.

## Process Provenance

Profile: [Process Provenance](../spec/process_provenance/overview.md).

### Dataset

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Dataset` | [`Dataset`](https://schema.org/Dataset) | - |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `identifiers` | [`identifier`](https://schema.org/identifier) | Renaming |
| `title` | [`name`](https://schema.org/name) | Renaming |
| `description` | [`description`](https://schema.org/description) | - |
| `processes` | [`about`](https://schema.org/about) | Renaming |
| `hasParts` | [`hasPart`](https://schema.org/hasPart) | Sub-datasets |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | As a profile convention; specific mappings may depend on the target profile. |

### Process

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Process` | [`LabProcess`](https://bioschemas.org/types/LabProcess/0.1-DRAFT) | - |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `name` | [`name`](https://schema.org/name) | - |
| `input` | [`object`](https://schema.org/object) | Renaming |
| `output` | [`result`](https://schema.org/result) | Renaming |
| `executesRecipe` | [`executesLabProtocol`](https://bioschemas.org/types/LabProcess/0.1-DRAFT#executesLabProtocol) | Renaming |
| `parameterValues` | [`parameterValue`](https://bioschemas.org/types/LabProcess/0.1-DRAFT) | Renaming |

### Recipe

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Recipe` | [`LabProtocol`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT) | - |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `name` | [`name`](https://schema.org/name) | - |
| `description` | [`description`](https://schema.org/description) | - |
| `parameters` | [`input`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT#input) | For consumed-input slots; specific mappings may depend on the parameter's role and target profile. |
| `intendedUse` | [`intendedUse`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT#intendedUse) | - |
| `components` | [`labEquipment`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT#labEquipment); [`computationalTool`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT#computationalTool); [`reagent`](https://bioschemas.org/types/LabProtocol/0.6-DRAFT#reagent) | Specific mappings depend on the component's kind and target profile. |
| `version` | [`version`](https://schema.org/version) | - |
| `url` | [`url`](https://schema.org/url) | - |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | As a profile convention; specific mappings may depend on the target profile. |

### Sample

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Sample` | [`Sample`](https://bioschemas.org/types/Sample/0.3-DRAFT) | For material samples; digital-sample mappings depend on the target profile. |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `name` | [`name`](https://schema.org/name) | - |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | Renaming |

### Data

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Data` | [`MediaObject`](https://schema.org/MediaObject) | `File` is the RO-Crate alias for MediaObject. |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `path` | `@id` | Renaming and String conversion |
| `selector` | `@id` | Renaming and String conversion |
| `selectorFormat` | [`usageInfo`](https://schema.org/usageInfo) | Renaming |
| `encodingFormat` | [`encodingFormat`](https://schema.org/encodingFormat) | - |
| `hasParts` | [`hasPart`](https://schema.org/hasPart) | Data fragments |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | As a profile convention; specific mappings may depend on the target profile. |

### Annotation

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Annotation` | [`PropertyValue`](https://schema.org/PropertyValue) | - |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `name` | [`name`](https://schema.org/name) | - |
| `value` | [`value`](https://schema.org/value) | - |
| `unit` | [`unitText`](https://schema.org/unitText) | Renaming |
| `nameTAN` | [`propertyID`](https://schema.org/propertyID) | Renaming |
| `valueTAN` | [`valueReference`](https://schema.org/valueReference) | Renaming |
| `unitTAN` | [`unitCode`](https://schema.org/unitCode) | Renaming |
| `instanceOf` | [`exampleOfWork`](https://schema.org/exampleOfWork) | As a profile convention; specific mappings may depend on the target profile. |

### FormalParameter

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `FormalParameter` | bioschemas:FormalParameter | - |
| `name` | [`name`](https://schema.org/name) | - |
| `nameTAN` | [`url`](https://schema.org/url) | Renaming |
| `defaultValue` | `bioschemas:defaultValue` | - |

### DefinedTerm

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `DefinedTerm` | bioschemas:DefinedTerm | - |
| `name` | [`name`](https://schema.org/name) | - |
| `TAN` | [`termCode`](https://schema.org/termCode) | Renaming |
| `inDefinedTermSet` | [`inDefinedTermSet`](https://schema.org/inDefinedTermSet) | - |

## Semantic Designation

Profile: [Semantic Designation](../spec/semantic_designation/overview.md). Shared Dataset and Annotation mappings are repeated here for the fields exposed by this profile.

### Dataset

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Dataset` | [`Dataset`](https://schema.org/Dataset) | - |
| `additionalType` | [`additionalType`](https://schema.org/additionalType) | - |
| `hasPart` | [`hasPart`](https://schema.org/hasPart) | Sub-datasets |

### Annotation

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Annotation` | [`PropertyValue`](https://schema.org/PropertyValue) | - |
| `additionalType` | [`additionalType`](https://schema.org/additionalType) | - |
| `name` | [`name`](https://schema.org/name) | - |
| `value` | [`value`](https://schema.org/value) | - |
| `unit` | [`unitText`](https://schema.org/unitText) | Renaming |

## Administrative

Profile: [Administrative](../spec/administrative/overview.md).

### Dataset

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Dataset` | [`Dataset`](https://schema.org/Dataset) | - |
| `id` | None | - |
| `type` | None | - |
| `additionalTypes` | [`additionalType`](https://schema.org/additionalType) | Renaming |
| `identifiers` | [`identifier`](https://schema.org/identifier) | Renaming |
| `title` | [`name`](https://schema.org/name) | Renaming |
| `description` | [`description`](https://schema.org/description) | - |
| `license` | [`license`](https://schema.org/license) | - |
| `datePublished` | [`datePublished`](https://schema.org/datePublished) | - |
| `dateCreated` | [`dateCreated`](https://schema.org/dateCreated) | - |
| `dateModified` | [`dateModified`](https://schema.org/dateModified) | - |
| `hasParts` | [`hasPart`](https://schema.org/hasPart) | Sub-datasets |
| `dataFiles` | [`hasPart`](https://schema.org/hasPart) | Data-file membership |
| `agents` | [`creator`](https://schema.org/creator) | For people; specific mappings may depend on the agent's kind, role, and target profile. |
| `citations` | [`citation`](https://schema.org/citation) | Renaming |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | As a profile convention; specific mappings may depend on the target profile. |

### Agent

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Agent` | [`Person`](https://schema.org/Person); [`SoftwareApplication`](https://schema.org/SoftwareApplication) | Person for people; SoftwareApplication for software agents. The local discriminator remains `Agent`. |
| `id` | None | - |
| `type` | None | - |
| `name` | [`name`](https://schema.org/name) | - |
| `givenName` | [`givenName`](https://schema.org/givenName) | For people. |
| `familyName` | [`familyName`](https://schema.org/familyName) | For people. |
| `emails` | [`email`](https://schema.org/email) | For people; software-agent mappings depend on the target profile. |
| `affiliations` | [`affiliation`](https://schema.org/affiliation) | For people; software-agent mappings depend on the relationship and target profile. |
| `identifiers` | [`identifier`](https://schema.org/identifier) | Renaming |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | As a profile convention; specific mappings may depend on the target profile. |
| `jobTitles` | [`jobTitle`](https://schema.org/jobTitle) | For professional titles of people; mappings for other roles or software agents depend on the target profile. |

### Organization

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Organization` | [`Organization`](https://schema.org/Organization) | - |
| `id` | None | - |
| `type` | None | - |
| `name` | [`name`](https://schema.org/name) | - |
| `url` | [`url`](https://schema.org/url) | - |

### ScholarlyArticle

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `ScholarlyArticle` | [`ScholarlyArticle`](https://schema.org/ScholarlyArticle) | - |
| `id` | None | - |
| `type` | None | - |
| `headline` | [`headline`](https://schema.org/headline) | - |
| `identifiers` | [`identifier`](https://schema.org/identifier) | Renaming |
| `authors` | [`author`](https://schema.org/author) | For people; software-agent mappings depend on the target profile. |
| `creativeWorkStatus` | [`creativeWorkStatus`](https://schema.org/creativeWorkStatus) | - |
| `additionalProperties` | [`additionalProperty`](https://schema.org/additionalProperty) | As a profile convention; specific mappings may depend on the target profile. |

## Datamap

Decoration profile: [Datamap](../spec/decorations/datamap/overview.md).

### Dataset

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `Dataset` | [`Dataset`](https://schema.org/Dataset) | - |
| `additionalType` | [`additionalType`](https://schema.org/additionalType) | - |
| `dataFiles` | [`hasPart`](https://schema.org/hasPart) | Added Property |
| `dataContexts` | [`variableMeasured`](https://schema.org/variableMeasured) | Renaming |

### DataContext

| ARC Base Model Property | Schema.org Property | Mapping |
|---|---|---|
| `DataContext` | [`PropertyValue`](https://schema.org/PropertyValue) | - |
| `data` | [`subjectOf`](https://schema.org/subjectOf) | Renaming |
| `explication` | [`value`](https://schema.org/value) | Renaming |
| `explicationTAN` | [`valueReference`](https://schema.org/valueReference) | Renaming |
| `objectType` | [`pattern`](https://schema.org/pattern) | Renaming |
| `objectTypeTAN` | [`valueReference`](https://schema.org/valueReference) | Renaming |
| `unit` | [`unitText`](https://schema.org/unitText) | Renaming |
| `unitTAN` | [`unitCode`](https://schema.org/unitCode) | Renaming |
| `label` | [`alternateName`](https://schema.org/alternateName) | Renaming |
| `description` | [`description`](https://schema.org/description) | - |
| `generatedBy` | [`measurementMethod`](https://schema.org/measurementMethod) | Renaming |
