namespace ARCManagement.Internal

open System.Collections.Generic
open ARCBaseModel

/// Document-local keys never become domain identities. Only IDs that the objects carry reach the document.
module internal DetachedYaml =
    /// Rows keyed by reference identity, plus the supplied ID of each object that has one.
    let private capture (root: Dataset) =
        Model.required "dataset" root |> ignore
        let keys = Dictionary<obj, string>(HashIdentity.Reference)
        let rows = ResizeArray<EntityRow>()
        let ids = Dictionary<string, string>()
        let rec visit (value: obj) : string =
            match keys.TryGetValue value with
            | true, key -> key
            | _ ->
                let key = "o" + string (keys.Count + 1)
                keys[value] <- key
                match Model.id value with
                | Some id -> ids[key] <- id
                | None -> ()
                rows.Add(Model.capture key visit value)
                key
        let key = visit (box root)
        let state = { Root = key; Entities = List.ofSeq rows }
        Model.validate state
        state, ids

    let encode indexed root =
        let state, ids = capture root
        ProcessCoreYaml.encode indexed state (fun key -> match ids.TryGetValue key with | true, id -> Some id | _ -> None)

    let decode source =
        Model.required "yaml" source |> ignore
        let state, ids = ProcessCoreYaml.decode source
        Model.validate state
        let objects = Dictionary<string, obj>()
        for row in state.Entities do objects.Add(row.Id, Model.create row)
        for row in state.Entities do Model.restore (fun key -> objects[key]) row objects[row.Id]
        for row in state.Entities do Model.setId objects[row.Id] (Map.tryFind row.Id ids)
        unbox<Dataset> objects[state.Root]

namespace ARCManagement

open ARCBaseModel
open ARCManagement.Internal
open Fable.Core

/// Detached Dataset YAML without SQL, workspace IO or identity assignment.
[<AttachMembers>]
type DatasetYaml private () =
    /// Encode the graph below a Dataset in the ProcessCore YAML shape, with every entity written inline at each
    /// occurrence. Entities without IDs cannot form a cycle, and entities that share an ID must have equal definitions.
    static member encode(dataset: Dataset): string = DetachedYaml.encode false dataset
    /// Encode like encode, but write Annotations, Recipes and Agents that have IDs once in the sections
    /// annotations, recipes and agentIndex of the root mapping and reference them by @id elsewhere.
    static member encodeIndexed(dataset: Dataset): string = DetachedYaml.encode true dataset
    /// Decode a Dataset from the ProcessCore YAML shape. Supplied IDs stay as they are and no IDs are assigned.
    static member decode(yaml: string): Dataset = DetachedYaml.decode yaml
