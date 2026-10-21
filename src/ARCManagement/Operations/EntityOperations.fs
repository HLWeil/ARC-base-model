namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

/// Session-owned generic objects and extension operations for all entity types.
[<AttachMembers>]
type EntityOperations internal (session: GraphContext) =
    member _.create(entityType: string) = session.Adopt<EntityObject>(EntityObject(entityType),entityType,true,None,false)
    member _.add(value: EntityObject) = session.Adopt<EntityObject>(value,value.Type,true,None,false)
    member _.upsert(value: EntityObject, ?baseline: EntityObject, ?recursive: bool) = session.Adopt<EntityObject>(value,value.Type,false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.GetEntity(id)
    member _.list() = session.ListEntities()
    member _.read(id: string) = session.Read<EntityObject>("",id)
    member _.readAll() = session.ReadAll<EntityObject>("")
    member _.delete(value: EntityObject, ?cascade: bool) = session.Delete(session.Id(value,""),"",defaultArg cascade false)
    member _.setAdditionalTypes(value: EntityObject, replacement: seq<string>) =
        session.Change<EntityObject>(session.Id(value,""), "additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))), "Entity.setAdditionalTypes")
    member _.getProperty(value: EntityObject, key: string) = session.GetEntity(session.Id(value,"")).EntityProperties.Get(key)
    member _.hasProperty(value: EntityObject, key: string) = session.GetEntity(session.Id(value,"")).EntityProperties.Contains(key)
    member _.setProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some replacement), false)
    member _.addProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some replacement), true)
    member _.removeProperty(value: EntityObject, key: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> None), false)
    member _.setNumberProperty(value: EntityObject, key: string, replacement: float) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Number(Model.number replacement))), false)
    member _.setTextProperty(value: EntityObject, key: string, replacement: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Text replacement)), false)
    member _.setBoolProperty(value: EntityObject, key: string, replacement: bool) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Bool(Model.boolean replacement))), false)
    member _.setObjectProperty(value: EntityObject, key: string, replacement: EntityObject) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> session.Id(replacement,"") |> ignore; Some(Entity.Object replacement)), false)
    member _.setCollectionProperty(value: EntityObject, key: string, replacement: seq<Entity>) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Collection(EntityCollection(replacement)))), false)
    member _.setNullProperty(value: EntityObject, key: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Null(EntityNull()))), false)
    member _.setBlobProperty(value: EntityObject, key: string, base64: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Blob(EntityBlob(base64)))), false)

    member _.addNumberProperty(value: EntityObject, key: string, replacement: float) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Number(Model.number replacement))), true)
    member _.addTextProperty(value: EntityObject, key: string, replacement: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Text replacement)), true)
    member _.addBoolProperty(value: EntityObject, key: string, replacement: bool) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Bool(Model.boolean replacement))), true)
    member _.addObjectProperty(value: EntityObject, key: string, replacement: EntityObject) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> session.Id(replacement,"") |> ignore; Some(Entity.Object replacement)), true)
    member _.addCollectionProperty(value: EntityObject, key: string, replacement: seq<Entity>) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Collection(EntityCollection(replacement)))), true)
    member _.addNullProperty(value: EntityObject, key: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Null(EntityNull()))), true)
    member _.addBlobProperty(value: EntityObject, key: string, base64: string) = session.Extension<EntityObject>(session.Id(value,""), key, (fun () -> Some(Entity.Blob(EntityBlob(base64)))), true)
