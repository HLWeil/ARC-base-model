namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

/// Session-owned generic objects and extension operations for all entity types.
[<AttachMembers>]
type EntityOperations internal (session: GraphContext) =
    member _.create(entityType: string): EntityObject = session.Add<EntityObject>(EntityObject(entityType), None)
    member _.add(value: EntityObject): EntityObject = session.Add<EntityObject>(value, None)
    member _.upsert(value: EntityObject, ?recursive: bool): EntityObject = session.Upsert<EntityObject>(value, None, defaultArg recursive false)
    member _.get(id: string) = session.GetEntity(id)
    member _.list(): seq<EntityObject> = session.ListEntities()
    member _.delete(value: EntityObject) = session.Delete(value)
    member _.setAdditionalTypes(value: EntityObject, replacement: seq<string>) =
        session.Change<EntityObject>(value, "additionalTypes", Some(Texts(List.ofSeq replacement)), value.Type + ".setAdditionalTypes")
    member _.setProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension<EntityObject>(value, key, Some replacement, false)
    member _.addProperty(value: EntityObject, key: string, replacement: Entity) = session.Extension<EntityObject>(value, key, Some replacement, true)
    member _.removeProperty(value: EntityObject, key: string) = session.Extension<EntityObject>(value, key, None, false)
