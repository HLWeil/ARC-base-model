namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

/// Session-owned generic objects and extension operations for all entity types.
[<AttachMembers>]
type EntityOperations internal (session: GraphContext) =
    member _.create(entityType: string) =
        let value = EntityObject(entityType)
        session.Register(value, entityType)
        value
    member _.add(value: EntityObject) = session.Register(value, value.Type); value
    member _.upsert(value: EntityObject): unit = session.Set(value, value.Type)
    member _.get(id: string) = session.GetEntity(id)
    member _.list() = session.ListEntities()
    member _.delete(value: EntityObject) = session.Delete(value)
    member _.setAdditionalTypes(value: EntityObject, replacement: seq<string>) =
        session.Change(value, "additionalTypes", Some(Texts(List.ofSeq replacement)), value.Type + ".setAdditionalTypes")
    member _.setProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension(value, key, Some replacement, false)
    member _.addProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension(value, key, Some replacement, true)
    member _.removeProperty(value: EntityObject, key: string) = session.Extension(value, key, None, false)
