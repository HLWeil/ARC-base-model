namespace ARCManagement.Internal

open System
open System.Collections.Generic
open ARCBaseModel
open YAMLicious.YAMLiciousTypes

/// Document-local reference keys never become domain identities.
module internal DetachedYaml =
    let private capture (root: Dataset) =
        Model.required "dataset" root |> ignore
        let objects = ResizeArray<string * obj>()
        let rows = ResizeArray<EntityRow>()
        let rec visit value =
            match objects |> Seq.tryFind (fun (_, previous) -> obj.ReferenceEquals(previous, value)) with
            | Some(key, _) -> key
            | None ->
                let key = "o" + string (objects.Count + 1)
                objects.Add(key, value)
                rows.Add(Model.capture key visit value)
                key
        let key = visit (box root)
        let state = { Root = key; Entities = List.ofSeq rows }
        Model.validate state
        state

    let encode root =
        let state = capture root
        let rows = state.Entities |> List.map (fun row -> row.Id, row) |> Map.ofList
        let rec anchored element =
            match element with
            | YAMLElement.Object values ->
                let fields = Codec.fields element
                match fields |> List.tryFind (fun (key, _) -> key = "type") with
                | Some _ ->
                    let key = Codec.value "id" element
                    let header = YAMLElement.Value(YAMLContent.create("", anchor = key, style = ScalarStyle.Plain))
                    let fields = values |> List.collect (function
                        | YAMLElement.Mapping(name, _) when name.Value = "id" ->
                            rows[key].SuppliedId |> Option.map (fun id -> YAMLElement.Mapping(name, Codec.text id)) |> Option.toList
                        | value -> [anchored value])
                    YAMLElement.Object(header :: fields)
                | None -> YAMLElement.Alias(Codec.value "$ref" element)
            | YAMLElement.Mapping(key, value) -> YAMLElement.Mapping(key, anchored value)
            | YAMLElement.Sequence values -> YAMLElement.Sequence(List.map anchored values)
            | value -> value
        Codec.rootElement state |> anchored |> Codec.write

    let rec private hasLocalReferences element =
        match element with
        | YAMLElement.Value value -> value.Anchor.IsSome
        | YAMLElement.Mapping(key, value) -> key.Anchor.IsSome || hasLocalReferences value
        | YAMLElement.Object values | YAMLElement.Sequence values -> List.exists hasLocalReferences values
        | YAMLElement.Alias _ -> true
        | _ -> false

    // Reuse the established graph decoder after replacing local references with
    // private keys. Scalar styles, extension keys and alternatives stay intact.
    let private localState element =
        let anchors = Dictionary<string, string>()
        let suppliedIds = Dictionary<string, string option>()
        let rejectScalarAnchor (content: YAMLContent) =
            if content.Anchor.IsSome then invalidOp "YAML anchors must identify entity objects."
        let rec normalize element =
            match element with
            | YAMLElement.Object [YAMLElement.Alias name] -> normalize (YAMLElement.Alias name)
            // Reader wraps sequence values in Object; writing that wrapper as a
            // sequence item otherwise turns an empty collection into a bare '-'.
            | YAMLElement.Object [YAMLElement.Sequence values] -> normalize (YAMLElement.Sequence values)
            | YAMLElement.Alias name ->
                match anchors.TryGetValue name with
                | true, key -> Codec.mapping ["$ref", Codec.text key]
                | _ -> invalidOp ("Unknown or forward YAML entity alias: " + name)
            | YAMLElement.Object values ->
                let anchor, values =
                    match values with
                    | YAMLElement.Value header :: rest when header.Value = "" && header.Anchor.IsSome && header.Tag.IsNone ->
                        header.Anchor, rest
                    | _ -> None, values
                let mapping = YAMLElement.Object values
                let isMapping = values |> List.forall (function YAMLElement.Mapping _ -> true | _ -> false)
                if isMapping then
                    let fields = Codec.fields mapping
                    let hasField name = fields |> List.exists (fun (key, _) -> key = name)
                    if hasField "type" then
                        let kind = Codec.value "type" mapping
                        let rejectLegacyReference value =
                            match value with
                            | YAMLElement.Value _ | YAMLElement.Object [YAMLElement.Value _] ->
                                invalidOp "YAML entity aliases cannot be mixed with scalar ID references."
                            | _ -> ()
                        let checkReferences name value =
                            match Schema.tryProperty kind name |> Option.map (fun p -> p.Shape) with
                            | Some(Schema.Shape.Links _) ->
                                match value with
                                | YAMLElement.Sequence items | YAMLElement.Object [YAMLElement.Sequence items] ->
                                    List.iter rejectLegacyReference items
                                | _ -> ()
                            | Some(Schema.Shape.Link _) | Some Schema.Shape.SampleOrData -> rejectLegacyReference value
                            | _ -> ()
                        let supplied = Codec.opt "id" mapping
                        let key = "o" + string (suppliedIds.Count + 1)
                        suppliedIds.Add(key, supplied)
                        match anchor with
                        | Some name ->
                            if anchors.ContainsKey name then invalidOp ("Duplicate YAML entity anchor: " + name)
                            anchors.Add(name, key)
                        | None -> ()
                        let fields = values |> List.map (function
                            | YAMLElement.Mapping(name, value) ->
                                rejectScalarAnchor name
                                if name.Value = "id" then
                                    match value with
                                    | YAMLElement.Value content | YAMLElement.Object [YAMLElement.Value content] -> rejectScalarAnchor content
                                    | _ -> ()
                                    YAMLElement.Mapping(name, Codec.text key)
                                else
                                    checkReferences name.Value value
                                    YAMLElement.Mapping(name, normalize value)
                            | _ -> invalidOp "Expected an entity mapping.")
                        let fields = if hasField "id" then fields else fields @ [YAMLElement.Mapping(YAMLContent.create("id"), Codec.text key)]
                        YAMLElement.Object fields
                    else
                        if anchor.IsSome then invalidOp "YAML anchors must identify entity objects."
                        if hasField "$ref" then invalidOp "YAML entity aliases cannot be mixed with legacy $ref references."
                        YAMLElement.Object(List.map normalize values)
                else
                    if anchor.IsSome then invalidOp "YAML anchors must identify entity objects."
                    YAMLElement.Object(List.map normalize values)
            | YAMLElement.Mapping(key, value) ->
                rejectScalarAnchor key
                YAMLElement.Mapping(key, normalize value)
            | YAMLElement.Sequence values -> YAMLElement.Sequence(List.map normalize values)
            | YAMLElement.Value value -> rejectScalarAnchor value; element
            | value -> value
        let state = normalize element |> Codec.write |> Codec.decodeGraph
        // Scalar ID-reference shorthand has no definition under a private key.
        // Validation below rejects it instead of resolving a domain ID.
        { state with Entities = state.Entities |> List.map (fun row -> { row with SuppliedId = suppliedIds[row.Id] }) }

    let decode source =
        Model.required "yaml" source |> ignore
        let element = YAMLicious.Reader.read source
        let state = if hasLocalReferences element then localState element else Codec.decodeGraph source
        Model.validate state
        let objects = Dictionary<string, obj>()
        for row in state.Entities do objects.Add(row.Id, Model.create row)
        for row in state.Entities do Model.restore (fun key -> objects[key]) row objects[row.Id]
        for row in state.Entities do Model.setId objects[row.Id] row.SuppliedId
        unbox<Dataset> objects[state.Root]

namespace ARCManagement

open ARCBaseModel
open ARCManagement.Internal
open Fable.Core

/// Detached Dataset YAML without SQL, workspace IO or identity assignment.
[<AttachMembers>]
type DatasetYaml private () =
    /// Encode shared and cyclic objects with document-local YAML anchors.
    static member encode(dataset: Dataset): string = DetachedYaml.encode dataset
    /// Decode local anchors or legacy id/$ref YAML, preserving optional IDs.
    static member decode(yaml: string): Dataset = DetachedYaml.decode yaml
