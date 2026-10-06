namespace ARCtrl

open Fable.Core
open ARCBaseModel
open ARCtrl.Internal

[<AttachMembers>]
type AnnotationOperations internal (session: Session) =
    member _.create(name: string) = let value = Annotation(name) in session.Register(value,"Annotation"); value
    member _.register(value: Annotation) = session.Register(value,"Annotation"); value
    member _.set(value: Annotation): unit = session.Set(value,"Annotation")
    member _.get(id: string) = session.Get<Annotation>("Annotation",id)
    member _.list() = session.List<Annotation>("Annotation")
    member _.delete(value: Annotation) = session.Delete(value)
    member _.setName(value: Annotation, replacement: string) =
        session.Change(value,"name",Some(Text(Model.required "name" replacement)),"Annotation.setName")
    member _.setValue(value: Annotation, replacement: AnnotationValue) =
        session.Change(value,"value",Some(match replacement with AnnotationValue.Text v -> Text(Model.required "value" v) | AnnotationValue.Number v -> Number v),"Annotation.setValue")
    member _.clearValue(value: Annotation) = session.Change(value,"value",None,"Annotation.clearValue")
    member _.setUnit(value: Annotation, replacement: string) =
        session.Change(value,"unit",Some(Text(Model.required "unit" replacement)),"Annotation.setUnit")
    member _.clearUnit(value: Annotation) = session.Change(value,"unit",None,"Annotation.clearUnit")
    member _.setNameTAN(value: Annotation, replacement: string) =
        session.Change(value,"nameTAN",Some(Text(Model.required "nameTAN" replacement)),"Annotation.setNameTAN")
    member _.clearNameTAN(value: Annotation) = session.Change(value,"nameTAN",None,"Annotation.clearNameTAN")
    member _.setValueTAN(value: Annotation, replacement: string) =
        session.Change(value,"valueTAN",Some(Text(Model.required "valueTAN" replacement)),"Annotation.setValueTAN")
    member _.clearValueTAN(value: Annotation) = session.Change(value,"valueTAN",None,"Annotation.clearValueTAN")
    member _.setUnitTAN(value: Annotation, replacement: string) =
        session.Change(value,"unitTAN",Some(Text(Model.required "unitTAN" replacement)),"Annotation.setUnitTAN")
    member _.clearUnitTAN(value: Annotation) = session.Change(value,"unitTAN",None,"Annotation.clearUnitTAN")
    member _.setInstanceOf(value: Annotation, replacement: FormalParameter) =
        session.Change(value,"instanceOf",Some(Links [session.Id(replacement)]),"Annotation.setInstanceOf")
    member _.clearInstanceOf(value: Annotation) = session.Change(value,"instanceOf",None,"Annotation.clearInstanceOf")
    member _.setAdditionalTypes(value: Annotation, replacement: seq<string>) =
        session.Change(value,"additionalTypes",Some(Texts(List.ofSeq replacement)),"Annotation.setAdditionalTypes")
    member _.setValueText(value: Annotation, replacement: string) = session.Change(value,"value",Some(Text(Model.required "value" replacement)),"Annotation.setValueText")
    member _.setValueNumber(value: Annotation, replacement: float) = session.Change(value,"value",Some(Number replacement),"Annotation.setValueNumber")
