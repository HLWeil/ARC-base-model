namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type AnnotationOperations internal (session: GraphContext) =
    member _.create(name: string): Annotation = session.Add<Annotation>(Annotation(name), Some "Annotation")
    member _.add(value: Annotation): Annotation = session.Add<Annotation>(value, Some "Annotation")
    member _.upsert(value: Annotation, ?recursive: bool): Annotation = session.Upsert<Annotation>(value, Some "Annotation", defaultArg recursive false)
    member _.get(id: string) = session.Get<Annotation>("Annotation",id)
    member _.list() = session.List<Annotation>("Annotation")
    member _.delete(value: Annotation) = session.Delete(value)
    member _.setName(value: Annotation, replacement: string) =
        session.Change<Annotation>(value,"name",Some(Text(Model.required "name" replacement)),"Annotation.setName")
    member _.setValue(value: Annotation, replacement: AnnotationValue option) =
        session.Change<Annotation>(value,"value",replacement |> Option.map (fun replacement -> match replacement with AnnotationValue.Text v -> Text(Model.required "value" v) | AnnotationValue.Number v -> Number(Model.number v)),"Annotation.setValue")
    member _.setUnit(value: Annotation, replacement: string option) =
        session.Change<Annotation>(value,"unit",replacement |> Option.map (fun replacement -> Text(Model.required "unit" replacement)),"Annotation.setUnit")
    member _.setNameTAN(value: Annotation, replacement: string option) =
        session.Change<Annotation>(value,"nameTAN",replacement |> Option.map (fun replacement -> Text(Model.required "nameTAN" replacement)),"Annotation.setNameTAN")
    member _.setValueTAN(value: Annotation, replacement: string option) =
        session.Change<Annotation>(value,"valueTAN",replacement |> Option.map (fun replacement -> Text(Model.required "valueTAN" replacement)),"Annotation.setValueTAN")
    member _.setUnitTAN(value: Annotation, replacement: string option) =
        session.Change<Annotation>(value,"unitTAN",replacement |> Option.map (fun replacement -> Text(Model.required "unitTAN" replacement)),"Annotation.setUnitTAN")
    member _.setInstanceOf(value: Annotation, replacement: FormalParameter option) =
        session.Change<Annotation>(value,"instanceOf",replacement |> Option.map (fun replacement -> Links [session.Id(replacement)]),"Annotation.setInstanceOf")
