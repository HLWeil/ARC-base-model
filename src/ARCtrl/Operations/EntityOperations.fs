namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

/// Session-owned generic objects and extension operations for all entity types.
[<AttachMembers>]
type EntityOperations internal (session: Session) =
    member _.create(entityType: string) =
        let value = EntityObject(entityType)
        session.Register(value, entityType)
        value
    member _.register(value: EntityObject) = session.Register(value, value.Type); value
    member _.upsert(value: EntityObject): unit = session.Set(value, value.Type)
    member _.get(id: string) = session.GetEntity(id)
    member _.list() = session.ListEntities()
    member _.delete(value: EntityObject) = session.Delete(value)
    member _.setAdditionalTypes(value: EntityObject, replacement: seq<string>) =
        session.Change(value, "additionalTypes", Some(Texts(List.ofSeq replacement)), value.Type + ".setAdditionalTypes")
    member _.getProperty(value: EntityObject, key: string) =
        session.Check()
        session.Id(value) |> ignore
        value.EntityProperties.Get(key)
    member _.hasProperty(value: EntityObject, key: string) =
        session.Check()
        session.Id(value) |> ignore
        value.EntityProperties.Contains(key)
    member _.setProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension(value, key, Some replacement, false)
    member _.addProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension(value, key, Some replacement, true)
    member _.removeProperty(value: EntityObject, key: string) = session.Extension(value, key, None, false)
    member _.setNumberProperty(value: EntityObject, key: string, replacement: float) = session.Extension(value, key, Some(Entity.Number(Model.number replacement)), false)
    member _.setTextProperty(value: EntityObject, key: string, replacement: string) = session.Extension(value, key, Some(Entity.Text replacement), false)
    member _.setBoolProperty(value: EntityObject, key: string, replacement: bool) = session.Extension(value, key, Some(Entity.Bool replacement), false)
    member _.setObjectProperty(value: EntityObject, key: string, replacement: EntityObject) = session.Extension(value, key, Some(Entity.Object replacement), false)
    member _.setCollectionProperty(value: EntityObject, key: string, replacement: seq<Entity>) = session.Extension(value, key, Some(Entity.Collection(EntityCollection(replacement))), false)
    member _.setNullProperty(value: EntityObject, key: string) = session.Extension(value, key, Some(Entity.Null(EntityNull())), false)
    member _.setBlobProperty(value: EntityObject, key: string, base64: string) = session.Extension(value, key, Some(Entity.Blob(EntityBlob(base64))), false)

    member _.addNumberProperty(value: EntityObject, key: string, replacement: float) = session.Extension(value, key, Some(Entity.Number(Model.number replacement)), true)
    member _.addTextProperty(value: EntityObject, key: string, replacement: string) = session.Extension(value, key, Some(Entity.Text replacement), true)
    member _.addBoolProperty(value: EntityObject, key: string, replacement: bool) = session.Extension(value, key, Some(Entity.Bool replacement), true)
    member _.addObjectProperty(value: EntityObject, key: string, replacement: EntityObject) = session.Extension(value, key, Some(Entity.Object replacement), true)
    member _.addCollectionProperty(value: EntityObject, key: string, replacement: seq<Entity>) = session.Extension(value, key, Some(Entity.Collection(EntityCollection(replacement))), true)
    member _.addNullProperty(value: EntityObject, key: string) = session.Extension(value, key, Some(Entity.Null(EntityNull())), true)
    member _.addBlobProperty(value: EntityObject, key: string, base64: string) = session.Extension(value, key, Some(Entity.Blob(EntityBlob(base64))), true)
