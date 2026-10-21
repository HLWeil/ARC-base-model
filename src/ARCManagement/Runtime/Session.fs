namespace ARCManagement.Internal

open System
open System.Collections.Generic
open Fable.Core
open ARCBaseModel
open PolyglotSQLite
open ARCManagement
open ARCManagement.Helper

module private RuntimeResults =
    let value<'T> (changed,operation,value: obj option) = WriteResult<'T>(unbox<'T> value.Value,changed,operation)
    let packed<'T> outcome = box(value<'T> outcome)

type private SnapshotOrigin = { Scope:string; ArcId:string; Revision:int64; Id:string }

/// Attest only published immutable values, without keeping snapshots alive.
module private SnapshotOrigins =
#if FABLE_COMPILER_PYTHON
    [<Fable.Core.Emit("__import__('weakref').WeakKeyDictionary()")>]
    let private create ():obj = nativeOnly
    [<Fable.Core.Emit("$0.get($1)")>]
    let private get (_table:obj) (_value:obj):SnapshotOrigin option = nativeOnly
    [<Fable.Core.Emit("$0.__setitem__($1, $2)")>]
    let private put (_table:obj) (_value:obj) (_origin:SnapshotOrigin):unit = nativeOnly
#else
#if FABLE_COMPILER
    [<Fable.Core.Emit("new WeakMap()")>]
    let private create ():obj = jsNative
    [<Fable.Core.Emit("$0.get($1)")>]
    let private get (_table:obj) (_value:obj):SnapshotOrigin option = jsNative
    [<Fable.Core.Emit("$0.set($1, $2)")>]
    let private put (_table:obj) (_value:obj) (_origin:SnapshotOrigin):unit = jsNative
#else
    let private create () = System.Runtime.CompilerServices.ConditionalWeakTable<obj,SnapshotOrigin>()
    let private get (table:System.Runtime.CompilerServices.ConditionalWeakTable<obj,SnapshotOrigin>) value =
        match table.TryGetValue(value) with true,origin -> Some origin | _ -> None
    let private put (table:System.Runtime.CompilerServices.ConditionalWeakTable<obj,SnapshotOrigin>) value origin =
        table.Remove(value) |> ignore
        table.Add(value,origin)
#endif
#endif
    let private table = create()
    let tryGet value = get table value
    let set value origin = put table value origin

/// An immutable SQL view. All authority remains in the repository.
type internal GraphContext(repository: Repository, arcId: string, _supplied: (string * obj) list) =
    let db = repository.Connection
    let scope = Identifier.newId()
    let mutable closed = false
    let mutable revision = -1L
    let mutable cursor = 0
    let mutable rootId = ""
    let mutable folder: string option = None
    let mutable baseline: string option = None
    let mutable savedGraph: string option = None
    let rows = Dictionary<string, EntityRow>()
    let objects = Dictionary<string, obj>()
    let idLists = Dictionary<string,string list>()
    let mutable complete: State option = None
    let mutable rootOnly: State option = None
    let mutable exported: string option = None
    let mutable standalone: bool option = None
    let mutable materializedCount = 0
    let mutable fullReadCount = 0
    let ensureOpen () =
        repository.EnsureOpen()
        if closed then invalidOp "The ARC graph is closed."
    let refresh () =
        ensureOpen()
        let observed = Store.revision db arcId
        if observed <> revision then
            let metadata = Store.metadata db arcId
            rows.Clear(); objects.Clear(); idLists.Clear(); complete <- None; rootOnly <- None; exported <- None; standalone <- None
            revision <- metadata.GetByName("revision").AsInteger()
            cursor <- int (metadata.GetByName("cursor").AsInteger())
            rootId <- metadata.GetByName("root_id").AsText()
            folder <- Store.optText metadata "folder"
            baseline <- Store.optText metadata "baseline"
            savedGraph <- Store.optText metadata "saved_graph"
    let query sql values = db.Query(sql, Store.parameters (("arc", Store.text arcId)::values))
    let execute sql values = Store.execute db sql (("arc", Store.text arcId)::values)
    let load ids =
        let ids = ids |> Seq.distinct |> Seq.toList
        let missing = ids |> List.filter (fun id -> not (rows.ContainsKey id))
        for row in Store.readRows db arcId missing do rows[row.Id] <- row
        ids |> List.choose (fun id -> match rows.TryGetValue id with true,row -> Some row | _ -> None)
    let row id = load [id] |> List.tryHead
    // Keep the pinned Python compiler's dictionary operation outside the
    // second operand of a short-circuit expression (which otherwise emits .has).
    let rowCached id = rows.ContainsKey id
    let requiredRow kind id =
        Model.required "id" id |> ignore
        match row id with
        | Some value when kind = "" || value.Kind = kind -> value
        | _ -> invalidArg "id" ("Unknown " + (if kind = "" then "entity" else kind) + ": " + id)
    let allState () =
        match complete with
        | Some value -> value
        | None ->
            fullReadCount <- fullReadCount+1
            let value = Store.readState db arcId
            for item in value.Entities do rows[item.Id] <- item
            complete <- Some value
            value
    let rootState () =
        match rootOnly with
        | Some value -> value
        | None ->
            let value=Store.readClosure db arcId [rootId]
            for item in value.Entities do rows[item.Id] <- item
            rootOnly<-Some value
            value
    let exportGraph () =
        match exported with
        | Some value -> value
        | None ->
            let value=Codec.encodeGraph(rootState())
            exported<-Some value
            value
    let sessionOnly () =
        match standalone with
        | Some value -> value
        | None ->
            let sql="WITH RECURSIVE reachable(id) AS (SELECT root_id FROM session WHERE arc_id=$arc UNION SELECT r.target_id FROM reachable q CROSS JOIN entity_reference r INDEXED BY reference_owner ON r.arc_id=$arc AND r.owner_id=q.id UNION SELECT x.target_id FROM reachable q CROSS JOIN entity_extension x INDEXED BY extension_owner ON x.arc_id=$arc AND x.owner_id=q.id WHERE x.target_id IS NOT NULL) SELECT (SELECT count(*) FROM reachable) AS reachable,(SELECT count(*) FROM entity WHERE arc_id=$arc) AS total"
            let count=(query sql [])[0]
            let value=count.GetByName("reachable").AsInteger()<count.GetByName("total").AsInteger()
            standalone<-Some value
            value
    let references item =
        (item.Properties |> Map.toList |> List.collect (fun (_,cell) -> match cell with Links ids -> ids | _ -> [])) @
        (item.Extensions |> Map.toList |> List.collect (snd >> Model.extensionReferences))
    let materialize (ids: string list) (overlay: EntityRow list) reuse =
        let proposed = Dictionary<string, EntityRow>()
        for item in overlay do proposed[item.Id] <- item
        if not overlay.IsEmpty then
            let external = (ids @ (overlay |> List.collect references)) |> List.filter (fun id -> not (proposed.ContainsKey id)) |> List.distinct
            if not external.IsEmpty then
                let closure=Store.readClosure db arcId external
                for item in closure.Entities do rows[item.Id] <- item
        let pending = Dictionary<string, EntityRow>()
        let values = Dictionary<string,obj>()
        let work=ResizeArray<string>(ids)
        while work.Count>0 do
            let index=work.Count-1
            let id=work[index]
            work.RemoveAt(index)
            if reuse && objects.ContainsKey id then values[id] <- objects[id]
            elif not (pending.ContainsKey id) then
                let item=
                    match proposed.TryGetValue id with
                    | true,item -> item
                    | _ -> match rows.TryGetValue id with true,item -> item | _ -> invalidOp ("Missing entity in copied SQL snapshot: "+id)
                pending.Add(id,item)
                for target in references item do work.Add(target)
        for item in pending.Values do
            values.Add(item.Id, Model.create item)
            materializedCount <- materializedCount+1
        for item in pending.Values do Model.restore (fun id -> values[id]) item values[item.Id]
        ModelHydration.Seal(pending.Keys |> Seq.map (fun id -> unbox<EntityObject> values[id]))
        values
    let copyReadRows ids =
        if ids |> List.exists (fun id -> not (objects.ContainsKey id)) then
            let missing = ids |> List.filter (fun id -> not (objects.ContainsKey id))
            let closure = Store.readClosure db arcId missing
            for item in closure.Entities do rows[item.Id] <- item
    let hydrateValues ids =
        if ids |> List.exists (fun id -> not (objects.ContainsKey id)) then
            let missing = ids |> List.filter (fun id -> not (objects.ContainsKey id))
            let values = materialize missing [] true
            for KeyValue(id,value) in values do
                if not (objects.ContainsKey id) then
                    objects.Add(id,value)
                    SnapshotOrigins.set value {Scope=scope;ArcId=arcId;Revision=revision;Id=id}
        ids |> List.map (fun id -> objects[id])
    let readValues ids = copyReadRows ids;hydrateValues ids
    // Copy a consistent SQL closure first. Allocation/linking/sealing happens
    // after ending that read transaction, while retaining the repository gate.
    let valueRead ready select = repository.Access(fun () ->
        ensureOpen()
        let ids=
            if Store.revision db arcId=revision && ready() then select()
            else db.WithTransaction(fun () ->
                refresh()
                let ids=select()
                copyReadRows ids
                ids)
        hydrateValues ids)
    let read action = repository.Access(fun () -> db.WithTransaction(fun () -> refresh(); action()))
    let cachedRead ready action = repository.Access(fun () ->
        ensureOpen()
        if Store.revision db arcId=revision && ready() then action()
        else db.WithTransaction(fun () -> refresh(); action()))
    let listIds kind =
        match idLists.TryGetValue kind with
        | true,ids -> ids
        | _ ->
            let ids=Store.listIds db arcId (if kind="" then None else Some kind)
            idLists[kind] <- ids
            ids
    let listReady kind () =
        match idLists.TryGetValue kind with
        | true,ids -> ids |> List.forall objects.ContainsKey
        | _ -> false
    let normalized values = values |> List.map (fun item -> {item with SuppliedId = None})
    let invalidate () = rows.Clear(); objects.Clear(); idLists.Clear(); complete <- None; rootOnly <- None; exported <- None; standalone <- None
    let validationResolver (before:EntityRow list) (after:EntityRow list) =
        let proposed=after |> List.map (fun item -> item.Id,item) |> Map.ofList
        let deleted=before |> List.map (fun item -> item.Id) |> Set.ofList |> fun previous -> Set.difference previous (proposed |> Map.toSeq |> Seq.map fst |> Set.ofSeq)
        let targets=Dictionary<string,EntityRow>()
        let missing=after |> Seq.collect references |> Seq.filter (fun id -> not (Map.containsKey id proposed) && not (Set.contains id deleted) && not (rows.ContainsKey id))
        for id,kind in Store.readKinds db arcId missing do
            targets[id] <- {Id=id;Kind=kind;SuppliedId=None;Properties=Map.empty;Extensions=Map.empty}
        fun id ->
            if Set.contains id deleted then None
            else match Map.tryFind id proposed with
                 | Some value -> Some value
                 | None -> match rows.TryGetValue id with
                           | true,value -> Some value
                           | _ -> match targets.TryGetValue id with true,value -> Some value | _ -> None
    // A monomorphic preparation boundary avoids duplicated generic parameters
    // emitted for captured local functions by the pinned Python compiler.
    let write (kind:string) (project:bool * AppliedOperation option * obj option -> obj) (prepare:unit -> obj) (plan:obj -> State * State * string option) =
        repository.Access(fun () ->
            ensureOpen()
            let prepared=prepare()
            let before,next,result,resultObjects = db.WithTransaction(fun () ->
                refresh()
                let before,next,resultId = plan prepared
                Model.validateRows (validationResolver before.Entities next.Entities) next.Entities
                let resultObjects =
                    if normalized before.Entities = normalized next.Entities then None
                    else resultId |> Option.map (fun id -> materialize [id] next.Entities false)
                let result = resultId |> Option.map (fun id ->
                    match resultObjects with Some values -> values[id] | None -> readValues [id] |> List.head)
                before,next,result,resultObjects)
            let changed = normalized before.Entities <> normalized next.Entities
            if not changed then project(false,None,result)
            else
                if revision = Int64.MaxValue then invalidOp "The ARC revision counter is exhausted."
                let operationId = Identifier.newId()
                let nextRevision = revision + 1L
                let output=project(true,Some(AppliedOperation(operationId,kind)),result)
                db.WithTransaction(fun () ->
                    if not (Store.claimRevision db arcId revision) then
                        raise (ConcurrentWriteException())
                    execute "DELETE FROM history WHERE arc_id=$arc AND sequence > $cursor" ["cursor",SqlValue.Integer(int64 cursor)]
                    Store.commit db arcId before next (cursor+1) operationId kind
                    execute "UPDATE session SET cursor=$cursor WHERE arc_id=$arc" ["cursor",SqlValue.Integer(int64 (cursor+1))]
                    execute "INSERT INTO journal(arc_id,sequence,operation_id,kind,action,revision) VALUES($arc,$revision,$id,$kind,'apply',$revision)"
                        ["revision",SqlValue.Integer nextRevision;"id",Store.text operationId;"kind",Store.text kind])
                revision <- nextRevision; cursor <- cursor+1; invalidate()
                resultObjects |> Option.iter (fun values ->
                    for KeyValue(id,value) in values do
                        objects.Add(id,value)
                        SnapshotOrigins.set value {Scope=scope;ArcId=arcId;Revision=revision;Id=id})
                output)
    let partial items = {Root=rootId;Entities=items}
    let commandResult (changed,operation,_) = box(CommandResult(changed,operation))
    do db.WithTransaction(refresh)

    member internal _.MaterializedCount = materializedCount
    member internal _.FullReadCount = fullReadCount
    member _.ArcId = arcId
    member _.Repository = repository
    member _.DatabasePath = repository.DatabasePath
    member _.IsClosed = closed
    member _.Folder = read (fun () -> folder)
    member _.Baseline = read (fun () -> baseline)
    member _.SavedGraph = read (fun () -> savedGraph)
    member _.State = read allState
    member _.Export() = cachedRead (fun () -> exported.IsSome) exportGraph
    member _.Model = valueRead (fun () -> objects.ContainsKey rootId) (fun () -> [rootId]) |> List.head |> unbox<Dataset>
    member _.ReadModel() =
        let values=valueRead (fun () -> objects.ContainsKey rootId) (fun () -> [rootId])
        values |> List.head |> unbox<Dataset>
    member _.Read<'T>(kind: string,id: string) =
        let values=valueRead (fun () -> objects.ContainsKey id && rowCached id) (fun () ->
            match row id with Some item when kind="" || item.Kind=kind -> [id] | _ -> [])
        values |> List.tryHead |> Option.map unbox<'T>
    member _.Get<'T>(kind: string,id: string) =
        valueRead (fun () -> objects.ContainsKey id && rowCached id) (fun () -> requiredRow kind id |> ignore;[id]) |> List.head |> unbox<'T>
    member _.List<'T>(kind: string) =
        valueRead (listReady kind) (fun () -> listIds kind) |> Seq.map unbox<'T> |> ImmutableCollections.OfSeq
    member _.ReadAll<'T>(kind: string) =
        let values=valueRead (listReady kind) (fun () -> listIds kind)
        values |> Seq.map unbox<'T> |> ImmutableCollections.OfSeq
    member _.GetEntity(id: string) =
        valueRead (fun () -> objects.ContainsKey id && rowCached id) (fun () -> requiredRow "" id |> ignore;[id]) |> List.head |> unbox<EntityObject>
    member _.ListEntities() =
        valueRead (listReady "") (fun () -> listIds "") |> Seq.map unbox<EntityObject> |> ImmutableCollections.OfSeq
    member _.Refresh() = refresh()
    member _.FolderValue = folder
    member _.BaselineValue = baseline
    member _.StateValue = rootState()
    member _.ExportValue = exportGraph()
    member _.IsDirtyValue = savedGraph<>Some(exportGraph())
    member _.HasSessionOnlyObjectsValue = sessionOnly()
    member _.Check() = read ignore
    member _.Id(value: EntityObject, kind:string) =
        Model.required "value" value |> ignore
        match box value with
        | :? EntityObject as entity ->
            if kind<>"" && Model.kind entity<>kind then invalidArg "value" ("Expected a "+kind+" subject.")
            match SnapshotOrigins.tryGet entity with
            | Some origin when origin.ArcId<>arcId -> invalidArg "value" "The subject was read from a different ARC."
            | _ -> Model.entityId entity.Id
        | _ -> invalidArg "value" "Expected an entity value with a persisted ID."
    member this.EndpointId(value: EntityReference) =
        match value with
        | EntityReference.Sample target -> this.Id(target,"Sample")
        | EntityReference.Data target -> this.Id(target,"Data")
    member _.Change<'T>(id:string,key:string,value:unit -> Cell option,kind:string) =
        write kind RuntimeResults.packed<'T> (fun () -> box(value())) (fun prepared ->
            let value=unbox<Cell option> prepared
            let before = requiredRow (if kind.StartsWith("Entity.") then "" else kind.Split('.')[0]) id
            let next = {before with Properties=match value with Some cell -> Map.add key cell before.Properties | None -> Map.remove key before.Properties}
            partial [before],partial [next],Some id) |> unbox<WriteResult<'T>>
    member this.Reference<'T>(id,key,target:EntityObject,targetKind,kind:string) =
        write kind RuntimeResults.packed<'T> (fun () -> box(this.Id(target,targetKind))) (fun prepared ->
            let targetId=unbox<string> prepared
            let before=requiredRow (kind.Split('.')[0]) id
            requiredRow targetKind targetId |> ignore
            let next={before with Properties=Map.add key (Links [targetId]) before.Properties}
            partial [before],partial [next],Some id) |> unbox<WriteResult<'T>>
    member this.Endpoint<'T>(id,key,target:EntityReference,kind) =
        match target with
        | EntityReference.Sample value -> this.Reference<'T>(id,key,value,"Sample",kind)
        | EntityReference.Data value -> this.Reference<'T>(id,key,value,"Data",kind)
    member this.Extension<'T>(id:string,key:string,value:unit -> Entity option,addOnly:bool) =
        write "Entity.setProperty" RuntimeResults.packed<'T> (fun () -> box(value())) (fun prepared ->
            let value=unbox<Entity option> prepared
            let before = requiredRow "" id
            if addOnly && Map.containsKey key before.Extensions then invalidArg "key" "Duplicate extension property."
            let capture value = Model.captureExtension (fun target -> let entity=unbox<EntityObject> target in let targetId=this.Id(entity,"") in requiredRow entity.Type targetId |> ignore; targetId) value
            let next = {before with Extensions=match value with Some value -> Map.add key (capture value) before.Extensions | None -> Map.remove key before.Extensions}
            partial [before],partial [next],Some id) |> unbox<WriteResult<'T>>
    member this.Collection<'T>(id,key,target:EntityObject,targetKind,append,kind) =
        write kind RuntimeResults.packed<'T> (fun () -> box(this.Id(target,targetKind))) (fun prepared ->
            let targetId=unbox<string> prepared
            let before=requiredRow (kind.Split('.')[0]) id
            requiredRow targetKind targetId |> ignore
            let values=Model.links key before
            if not append && not (List.contains targetId values) then invalidOp "Target is not attached."
            let rec remove = function [] -> [] | head::tail when head=targetId -> tail | head::tail -> head::remove tail
            let next={before with Properties=Map.add key (Links(if append then values@[targetId] else remove values)) before.Properties}
            partial [before],partial [next],Some id) |> unbox<WriteResult<'T>>
    member _.Move<'T>(id,destinationId,key,kind) =
        write kind RuntimeResults.packed<'T> (fun () -> box()) (fun _ ->
            requiredRow (if key="processes" then "Process" else "Dataset") id |> ignore
            requiredRow "Dataset" destinationId |> ignore
            let ownerIds=query "SELECT DISTINCT r.owner_id FROM entity_reference r JOIN entity e ON e.arc_id=r.arc_id AND e.id=r.owner_id WHERE r.arc_id=$arc AND r.property=$property AND r.target_id=$target AND e.type='Dataset'"
                                ["property",Store.text key;"target",Store.text id]
                         |> Seq.map (fun item -> item.GetByName("owner_id").AsText()) |> Seq.toList
            let owners=load (destinationId::ownerIds)
            let count=owners |> List.collect (Model.links key) |> List.filter ((=) id) |> List.length
            if count>1 then invalidOp "Move requires a single membership."
            let next=owners |> List.map (fun item ->
                let values=Model.links key item |> List.filter ((<>) id)
                {item with Properties=Map.add key (Links(if item.Id=destinationId then values@[id] else values)) item.Properties})
            partial owners,partial next,Some id) |> unbox<WriteResult<'T>>
    member this.CreateDescriptor(target:EntityReference) =
        write "Descriptor.add" RuntimeResults.packed<Descriptor> (fun () -> box(this.EndpointId(target),Model.kind (Model.endpoint target))) (fun prepared ->
            let targetId,targetKind=unbox<string*string> prepared
            requiredRow targetKind targetId |> ignore
            let id=Identifier.newId()
            let value={Id=id;Kind="Descriptor";SuppliedId=None
                       Properties=Map.ofList ["describes",Links[targetId];"annotations",Links [];"additionalTypes",Texts []]
                       Extensions=Map.empty}
            partial [],partial [value],Some id) |> unbox<WriteResult<Descriptor>>
    member _.Adopt<'T>(entity: obj,kind,insertOnly,baselineValue:obj option,recursively) =
        write (kind+(if insertOnly then ".add" else ".upsert")) RuntimeResults.packed<'T> (fun () -> box()) (fun _ ->
            Model.required "entity" entity |> ignore
            if Model.kind entity<>kind then invalidArg "entity" "Wrong entity type."
            let visited=Dictionary<obj,string>(HashIdentity.Reference)
            let definitions=Dictionary<string,obj>()
            let staged=Dictionary<string,EntityRow>()
            let existing=Dictionary<string,EntityRow>()
            let pending=ResizeArray<obj*string>()
            let captured=ResizeArray<obj*EntityRow>()
            let register (value: obj) =
                Model.required "entity" value |> ignore
                match visited.TryGetValue value with
                | true,id -> id
                | _ ->
                    match SnapshotOrigins.tryGet value with
                    | Some origin when origin.Scope=scope && origin.Revision=revision && not recursively && not (obj.ReferenceEquals(value,entity)) -> origin.Id
                    | _ ->
                        let id=Model.id value |> Option.defaultWith Identifier.newId
                        visited.Add(value,id)
                        pending.Add(value,id)
                        id
            let resultId=register entity
            let baselineRows=Dictionary<string,EntityRow>()
            baselineValue |> Option.iter (fun original ->
                Model.required "baseline" original |> ignore
                match SnapshotOrigins.tryGet original with
                | Some origin when origin.ArcId<>arcId -> invalidArg "baseline" "The baseline was read from a different ARC."
                | _ -> ()
                if Model.kind original<>kind || Model.id original<>Some resultId then
                    invalidArg "baseline" "The baseline must have the same entity kind and ID as the upsert value."
                let seen=HashSet<obj>(HashIdentity.Reference)
                let originals=ResizeArray<obj>([original])
                let identify value =
                    let id=Model.id value |> Model.entityId
                    if recursively && seen.Add(value) then originals.Add(value)
                    id
                seen.Add(original) |> ignore
                let mutable index=0
                while index<originals.Count do
                    let value=originals[index]
                    index<-index+1
                    let id=Model.id value |> Model.entityId
                    let captured=Model.capture id identify value
                    if baselineRows.ContainsKey id then invalidArg "baseline" ("Distinct baseline objects share an ID: "+id)
                    baselineRows.Add(id,captured))
            let mutable index=0
            while index<pending.Count do
                let value,id=pending[index]
                index<-index+1
                captured.Add(value,Model.capture id register value)
            load (pending |> Seq.map snd) |> ignore
            for value,candidate in captured do
                let id=candidate.Id
                let stored=row id
                let isRoot=obj.ReferenceEquals(value,entity)
                stored |> Option.iter (fun item ->
                    if item.Kind<>candidate.Kind then invalidOp ("Entity ID has another type: "+id)
                    existing[id] <- item)
                if isRoot && insertOnly && stored.IsSome then invalidOp ("Entity already exists: "+id)
                let writable=isRoot || stored.IsNone || recursively
                let candidate={candidate with SuppliedId=stored |> Option.bind (fun item -> item.SuppliedId)}
                if writable then
                    match definitions.TryGetValue id with
                    | true,other when not (obj.ReferenceEquals(other,value)) -> invalidOp ("Conflicting submitted definitions: "+id)
                    | _ -> definitions[id] <- value
                    staged[id] <- candidate
                else
                    let old=stored.Value
                    if {candidate with SuppliedId=None}<>{old with SuppliedId=None} then
                        let coreDifference =
                            Seq.append (old.Properties |> Map.toSeq |> Seq.map fst) (candidate.Properties |> Map.toSeq |> Seq.map fst)
                            |> Seq.distinct
                            |> Seq.tryFind (fun key -> Map.tryFind key old.Properties <> Map.tryFind key candidate.Properties)
                        let difference = coreDifference |> Option.orElseWith (fun () ->
                            Seq.append (old.Extensions |> Map.toSeq |> Seq.map fst) (candidate.Extensions |> Map.toSeq |> Seq.map fst)
                            |> Seq.distinct
                            |> Seq.tryFind (fun key -> Map.tryFind key old.Extensions <> Map.tryFind key candidate.Extensions)
                            |> Option.map (fun key -> "extension:"+key))
                        invalidOp ("Existing nested entity differs from storage: "+id+"."+defaultArg difference "definition"+". Use recursive=true or update that entity directly.")
            if not insertOnly then
                for id in staged.Keys do
                    let stored=match existing.TryGetValue id with true,item -> Some item | _ -> None
                    let original=match baselineRows.TryGetValue id with true,item -> Some item | _ -> None
                    match stored,original with
                    | Some stored,Some original when normalized [stored] <> normalized [original] -> raise (SnapshotConflictException(id))
                    | None,Some _ -> raise (SnapshotConflictException(id))
                    | Some _,None -> invalidArg "baseline" ("Replacing an existing entity requires its original baseline: "+id+". Pass baseline=original or use a focused command.")
                    | _ -> ()
            let before=staged.Keys |> Seq.choose (fun id -> match existing.TryGetValue id with true,item -> Some item | _ -> None) |> Seq.toList
            let next=staged.Values |> Seq.toList
            partial before,partial next,Some resultId) |> unbox<WriteResult<'T>>
    member _.Delete(id,kind,cascade) =
        write (kind+".delete") commandResult (fun () -> box()) (fun _ ->
            requiredRow kind id |> ignore
            if id=rootId then invalidOp "The root Dataset cannot be deleted."
            let candidates=Dictionary<string,EntityRow>()
            let scheduled=HashSet<string>()
            let pending=ResizeArray<string>([id])
            scheduled.Add(id) |> ignore
            while pending.Count>0 do
                let batch=pending.ToArray()
                pending.Clear()
                for item in load batch do
                    candidates[item.Id] <- item
                    if cascade && item.Kind="Dataset" then
                        for child in Model.links "hasParts" item @ Model.links "processes" item do
                            if child<>rootId && scheduled.Add(child) then pending.Add(child)
            let incoming=ResizeArray<string*string>()
            for batch in candidates.Keys |> Seq.chunkBySize 300 do
                let names=batch |> Array.mapi (fun index _ -> "$target"+string index) |> String.concat ","
                let bindings=batch |> Array.mapi (fun index target -> "target"+string index,Store.text target) |> Array.toList
                let sql="SELECT owner_id,target_id FROM entity_reference WHERE arc_id=$arc AND target_id IN ("+names+") UNION SELECT owner_id,target_id FROM entity_extension WHERE arc_id=$arc AND target_id IN ("+names+")"
                for item in query sql bindings do incoming.Add(item.GetByName("owner_id").AsText(),item.GetByName("target_id").AsText())
            let preserved=HashSet<string>()
            let preserve=ResizeArray<string>()
            if cascade then
                for owner,target in incoming do
                    if target<>id && not (candidates.ContainsKey owner) && preserved.Add(target) then preserve.Add(target)
                while preserve.Count>0 do
                    let last=preserve.Count-1
                    let owner=preserve[last]
                    preserve.RemoveAt(last)
                    for target in references candidates[owner] do
                        if target<>id && candidates.ContainsKey target && preserved.Add(target) then preserve.Add(target)
            let removed=candidates.Keys |> Seq.filter (fun candidate -> not (preserved.Contains candidate)) |> Set.ofSeq
            let owners=incoming |> Seq.map fst |> Seq.filter (fun owner -> not (Set.contains owner removed)) |> Seq.distinct |> load
            let updated=ResizeArray<EntityRow>()
            for item in owners do
                if item.Extensions |> Map.exists (fun _ cell -> Model.extensionReferences cell |> List.exists (fun target -> Set.contains target removed)) then
                    invalidOp "Remove extension references before deleting their targets."
                let props=item.Properties |> Map.toList |> List.choose (fun (key,cell) ->
                    match cell with
                    | Links ids when ids |> List.exists (fun target -> Set.contains target removed) ->
                        if not cascade then invalidOp "Entity is referenced. Use cascade=true to detach optional references."
                        let _,shape,_,mandatory=Model.specifications item.Kind |> List.find (fun (name,_,_,_) -> name=key)
                        let remaining=ids |> List.filter (fun target -> not (Set.contains target removed))
                        if shape<>"links" && remaining=[] then
                            if mandatory then invalidOp "Cannot delete a required reference target."
                            None
                        else Some(key,Links remaining)
                    | _ -> Some(key,cell)) |> Map.ofList
                if props<>item.Properties then updated.Add({item with Properties=props})
            let before=(removed |> Seq.map (fun target -> candidates[target]) |> Seq.toList) @ (updated |> Seq.map (fun item -> rows[item.Id]) |> Seq.toList)
            partial before,partial(List.ofSeq updated),None) |> unbox<CommandResult>
    member _.CanUndo = read (fun () -> cursor>0)
    member _.CanRedo = read (fun () -> (query "SELECT sequence FROM history WHERE arc_id=$arc AND sequence=$sequence" ["sequence",SqlValue.Integer(int64 (cursor+1))]).Count>0)
    member _.History(redo: bool) = repository.Access(fun () ->
        let before,next,operationId,kind,sequence = db.WithTransaction(fun () ->
            refresh()
            let sequence=if redo then cursor+1 else cursor
            if sequence<=0 then invalidOp "Nothing to undo."
            let before,next,operationId,kind=Store.historyRows db arcId sequence redo
            Model.validateRows (validationResolver before.Entities next.Entities) next.Entities
            before,next,operationId,kind,sequence)
        if revision=Int64.MaxValue then invalidOp "The ARC revision counter is exhausted."
        let nextCursor=if redo then cursor+1 else cursor-1
        let output=CommandResult(true,Some(AppliedOperation(operationId,kind,action=(if redo then "redo" else "undo"))))
        db.WithTransaction(fun () ->
            if not (Store.claimRevision db arcId revision) then raise (ConcurrentWriteException())
            Store.writeDelta db arcId before next
            Store.restoreHistoryOrder db arcId sequence redo
            execute "UPDATE session SET cursor=$cursor WHERE arc_id=$arc" ["cursor",SqlValue.Integer(int64 nextCursor)]
            execute "INSERT INTO journal VALUES($arc,$revision,$id,$kind,$action,$revision)"
                ["revision",SqlValue.Integer(revision+1L);"id",Store.text operationId;"kind",Store.text kind;"action",Store.text(if redo then "redo" else "undo")])
        revision<-revision+1L;cursor<-nextCursor;invalidate()
        output)
    member _.IsDirty = cachedRead (fun () -> exported.IsSome) (fun () -> savedGraph<>Some(exportGraph()))
    member _.HasSessionOnlyObjects = cachedRead (fun () -> standalone.IsSome) sessionOnly
    member _.AssertRevision() = ensureOpen(); if Store.revision db arcId<>revision then invalidOp "ARC changed during the workspace operation."
    member _.SetBinding(path: string) =
        if folder<>Some path then
            if not (Store.claimRevision db arcId revision) then invalidOp "ARC changed during binding."
            execute "UPDATE session SET folder=$folder,baseline=NULL,saved_graph=NULL WHERE arc_id=$arc" ["folder",Store.text path]
            folder<-Some path;baseline<-None;savedGraph<-None;revision<-revision+1L;invalidate()
    member _.ClaimCheckpoint() =
        if revision=Int64.MaxValue then invalidOp "The ARC revision counter is exhausted."
        if not (Store.claimRevision db arcId revision) then invalidOp "ARC changed during save."
    member _.SetCheckpoint(graph: string) =
        execute "UPDATE session SET baseline=$graph,saved_graph=$graph WHERE arc_id=$arc" ["graph",Store.text graph]
    member _.AcceptCheckpoint(graph: string) = baseline<-Some graph;savedGraph<-Some graph;revision<-revision+1L;invalidate()
    member _.AcceptPersistentBaseline(current: string option) =
        refresh()
        if current<>baseline then
            db.WithTransaction(fun () ->
                if not (Store.claimRevision db arcId revision) then invalidOp "ARC changed during recovery."
                execute "UPDATE session SET baseline=$baseline,saved_graph=NULL WHERE arc_id=$arc" ["baseline",Store.optional current])
            baseline<-current;savedGraph<-None;revision<-revision+1L;invalidate()
    member _.Reload(next: State,current: string) =
        refresh(); Model.validate next
        db.WithTransaction(fun () ->
            if not (Store.claimRevision db arcId revision) then invalidOp "ARC changed during recovery."
            let archiveId=Store.archive db arcId
            execute "UPDATE arc_archive SET revision=$revision WHERE archive_id=$archive" ["revision",SqlValue.Integer revision;"archive",Store.text archiveId]
            Store.mirror db arcId next
            execute "DELETE FROM history WHERE arc_id=$arc" []
            execute "DELETE FROM journal WHERE arc_id=$arc" []
            execute "UPDATE session SET root_id=$root,baseline=$baseline,saved_graph=$saved,cursor=0 WHERE arc_id=$arc"
                ["root",Store.text next.Root;"baseline",Store.text current;"saved",Store.text(Codec.encodeGraph next)])
        rootId<-next.Root;baseline<-Some current;savedGraph<-Some(Codec.encodeGraph next);cursor<-0;revision<-revision+1L;invalidate()
    member _.Close() =
        if not closed then closed<-true;repository.Forget(arcId);invalidate()
