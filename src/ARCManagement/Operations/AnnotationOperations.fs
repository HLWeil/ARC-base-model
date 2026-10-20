namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type AnnotationOperations internal (session: GraphContext) =
    member _.create(name: string) = session.Adopt<Annotation>(Annotation(name), "Annotation", true,None,false)
    member _.add(value: Annotation) = session.Adopt<Annotation>(value, "Annotation", true,None,false)
    member _.upsert(value: Annotation, ?baseline: Annotation, ?recursive: bool) = session.Adopt<Annotation>(value, "Annotation", false,Option.map box baseline,defaultArg recursive false)
    member _.get(id: string) = session.Get<Annotation>("Annotation",id)
    member _.list() = session.List<Annotation>("Annotation")
    member _.read(id: string) = session.Read<Annotation>("Annotation",id)
    member _.readAll() = session.ReadAll<Annotation>("Annotation")
    member _.delete(value: Annotation, ?cascade: bool) = session.Delete(session.Id(value,"Annotation"),"Annotation",defaultArg cascade false)
    member _.setName(value: Annotation, replacement: string) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"name", (fun () -> Some(Text(Model.required "name" replacement))),"Annotation.setName")
    member _.setValue(value: Annotation, replacement: AnnotationValue) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"value", (fun () -> Some(match replacement with AnnotationValue.Text v -> Text(Model.required "value" v) | AnnotationValue.Number v -> Number v)),"Annotation.setValue")
    member _.clearValue(value: Annotation) = session.Change<Annotation>(session.Id(value,"Annotation"),"value", (fun () -> None),"Annotation.clearValue")
    member _.setUnit(value: Annotation, replacement: string) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"unit", (fun () -> Some(Text(Model.required "unit" replacement))),"Annotation.setUnit")
    member _.clearUnit(value: Annotation) = session.Change<Annotation>(session.Id(value,"Annotation"),"unit", (fun () -> None),"Annotation.clearUnit")
    member _.setNameTAN(value: Annotation, replacement: string) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"nameTAN", (fun () -> Some(Text(Model.required "nameTAN" replacement))),"Annotation.setNameTAN")
    member _.clearNameTAN(value: Annotation) = session.Change<Annotation>(session.Id(value,"Annotation"),"nameTAN", (fun () -> None),"Annotation.clearNameTAN")
    member _.setValueTAN(value: Annotation, replacement: string) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"valueTAN", (fun () -> Some(Text(Model.required "valueTAN" replacement))),"Annotation.setValueTAN")
    member _.clearValueTAN(value: Annotation) = session.Change<Annotation>(session.Id(value,"Annotation"),"valueTAN", (fun () -> None),"Annotation.clearValueTAN")
    member _.setUnitTAN(value: Annotation, replacement: string) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"unitTAN", (fun () -> Some(Text(Model.required "unitTAN" replacement))),"Annotation.setUnitTAN")
    member _.clearUnitTAN(value: Annotation) = session.Change<Annotation>(session.Id(value,"Annotation"),"unitTAN", (fun () -> None),"Annotation.clearUnitTAN")
    member _.setInstanceOf(value: Annotation, replacement: string) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"instanceOf", (fun () -> Some(Links [replacement])),"Annotation.setInstanceOf")
    member _.clearInstanceOf(value: Annotation) = session.Change<Annotation>(session.Id(value,"Annotation"),"instanceOf", (fun () -> None),"Annotation.clearInstanceOf")
    member _.setAdditionalTypes(value: Annotation, replacement: seq<string>) =
        session.Change<Annotation>(session.Id(value,"Annotation"),"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Annotation.setAdditionalTypes")
    member _.setValueText(value: Annotation, replacement: string) = session.Change<Annotation>(session.Id(value,"Annotation"),"value", (fun () -> Some(Text(Model.required "value" replacement))),"Annotation.setValueText")
    member _.setValueNumber(value: Annotation, replacement: float) = session.Change<Annotation>(session.Id(value,"Annotation"),"value", (fun () -> Some(Number(Model.number replacement))),"Annotation.setValueNumber")
