namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

/// Session-owned generic objects and extension operations for all entity types.
[<AttachMembers>]
type EntityOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, entityType: string) = session.Adopt<EntityObject>(revision,EntityObject(entityType),entityType,true,false)
    member _.add(revision: RevisionToken, value: EntityObject) = session.Adopt<EntityObject>(revision,value,value.Type,true,false)
    member _.upsert(revision: RevisionToken, value: EntityObject, ?recursive: bool) = session.Adopt<EntityObject>(revision,value,value.Type,false,defaultArg recursive false)
    member _.get(id: string) = session.GetEntity(id)
    member _.list() = session.ListEntities()
    member _.read(id: string) = session.Read<EntityObject>("",id)
    member _.readAll() = session.ReadAll<EntityObject>("")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"",defaultArg cascade false)
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<EntityObject>(revision,id, "additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))), "Entity.setAdditionalTypes")
    member _.getProperty(id: string, key: string) = session.GetEntity(id).EntityProperties.Get(key)
    member _.hasProperty(id: string, key: string) = session.GetEntity(id).EntityProperties.Contains(key)
    member _.setProperty(revision: RevisionToken, id: string, key: string, replacement: Entity) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some replacement), false)
    member _.addProperty(revision: RevisionToken, id: string, key: string, replacement: Entity) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some replacement), true)
    member _.removeProperty(revision: RevisionToken, id: string, key: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> None), false)
    member _.setNumberProperty(revision: RevisionToken, id: string, key: string, replacement: float) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Number(Model.number replacement))), false)
    member _.setTextProperty(revision: RevisionToken, id: string, key: string, replacement: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Text replacement)), false)
    member _.setBoolProperty(revision: RevisionToken, id: string, key: string, replacement: bool) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Bool(Model.boolean replacement))), false)
    member _.setObjectProperty(revision: RevisionToken, id: string, key: string, replacementId: string) = session.ExtensionCell<EntityObject>(revision,id, key, (fun () -> Some(ExtensionObject replacementId)), false)
    member _.setCollectionProperty(revision: RevisionToken, id: string, key: string, replacement: seq<Entity>) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Collection(EntityCollection(replacement)))), false)
    member _.setNullProperty(revision: RevisionToken, id: string, key: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Null(EntityNull()))), false)
    member _.setBlobProperty(revision: RevisionToken, id: string, key: string, base64: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Blob(EntityBlob(base64)))), false)

    member _.addNumberProperty(revision: RevisionToken, id: string, key: string, replacement: float) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Number(Model.number replacement))), true)
    member _.addTextProperty(revision: RevisionToken, id: string, key: string, replacement: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Text replacement)), true)
    member _.addBoolProperty(revision: RevisionToken, id: string, key: string, replacement: bool) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Bool(Model.boolean replacement))), true)
    member _.addObjectProperty(revision: RevisionToken, id: string, key: string, replacementId: string) = session.ExtensionCell<EntityObject>(revision,id, key, (fun () -> Some(ExtensionObject replacementId)), true)
    member _.addCollectionProperty(revision: RevisionToken, id: string, key: string, replacement: seq<Entity>) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Collection(EntityCollection(replacement)))), true)
    member _.addNullProperty(revision: RevisionToken, id: string, key: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Null(EntityNull()))), true)
    member _.addBlobProperty(revision: RevisionToken, id: string, key: string, base64: string) = session.Extension<EntityObject>(revision,id, key, (fun () -> Some(Entity.Blob(EntityBlob(base64)))), true)
