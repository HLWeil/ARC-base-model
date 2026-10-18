namespace ARCManagement

open Fable.Core
open ARCBaseModel
open ARCManagement.Internal

[<AttachMembers>]
type AnnotationOperations internal (session: GraphContext) =
    member _.create(revision: RevisionToken, name: string) = session.Adopt<Annotation>(revision, Annotation(name), "Annotation", true, false)
    member _.add(revision: RevisionToken, value: Annotation) = session.Adopt<Annotation>(revision, value, "Annotation", true, false)
    member _.upsert(revision: RevisionToken, value: Annotation, ?recursive: bool) = session.Adopt<Annotation>(revision, value, "Annotation", false, defaultArg recursive false)
    member _.get(id: string) = session.Get<Annotation>("Annotation",id)
    member _.list() = session.List<Annotation>("Annotation")
    member _.read(id: string) = session.Read<Annotation>("Annotation",id)
    member _.readAll() = session.ReadAll<Annotation>("Annotation")
    member _.delete(revision: RevisionToken, id: string, ?cascade: bool) = session.Delete(revision,id,"Annotation",defaultArg cascade false)
    member _.setName(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Annotation>(revision,id,"name", (fun () -> Some(Text(Model.required "name" replacement))),"Annotation.setName")
    member _.setValue(revision: RevisionToken, id: string, replacement: AnnotationValue) =
        session.Change<Annotation>(revision,id,"value", (fun () -> Some(match replacement with AnnotationValue.Text v -> Text(Model.required "value" v) | AnnotationValue.Number v -> Number v)),"Annotation.setValue")
    member _.clearValue(revision: RevisionToken, id: string) = session.Change<Annotation>(revision,id,"value", (fun () -> None),"Annotation.clearValue")
    member _.setUnit(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Annotation>(revision,id,"unit", (fun () -> Some(Text(Model.required "unit" replacement))),"Annotation.setUnit")
    member _.clearUnit(revision: RevisionToken, id: string) = session.Change<Annotation>(revision,id,"unit", (fun () -> None),"Annotation.clearUnit")
    member _.setNameTAN(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Annotation>(revision,id,"nameTAN", (fun () -> Some(Text(Model.required "nameTAN" replacement))),"Annotation.setNameTAN")
    member _.clearNameTAN(revision: RevisionToken, id: string) = session.Change<Annotation>(revision,id,"nameTAN", (fun () -> None),"Annotation.clearNameTAN")
    member _.setValueTAN(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Annotation>(revision,id,"valueTAN", (fun () -> Some(Text(Model.required "valueTAN" replacement))),"Annotation.setValueTAN")
    member _.clearValueTAN(revision: RevisionToken, id: string) = session.Change<Annotation>(revision,id,"valueTAN", (fun () -> None),"Annotation.clearValueTAN")
    member _.setUnitTAN(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Annotation>(revision,id,"unitTAN", (fun () -> Some(Text(Model.required "unitTAN" replacement))),"Annotation.setUnitTAN")
    member _.clearUnitTAN(revision: RevisionToken, id: string) = session.Change<Annotation>(revision,id,"unitTAN", (fun () -> None),"Annotation.clearUnitTAN")
    member _.setInstanceOf(revision: RevisionToken, id: string, replacement: string) =
        session.Change<Annotation>(revision,id,"instanceOf", (fun () -> Some(Links [replacement])),"Annotation.setInstanceOf")
    member _.clearInstanceOf(revision: RevisionToken, id: string) = session.Change<Annotation>(revision,id,"instanceOf", (fun () -> None),"Annotation.clearInstanceOf")
    member _.setAdditionalTypes(revision: RevisionToken, id: string, replacement: seq<string>) =
        session.Change<Annotation>(revision,id,"additionalTypes", (fun () -> Some(Texts(List.ofSeq replacement))),"Annotation.setAdditionalTypes")
    member _.setValueText(revision: RevisionToken, id: string, replacement: string) = session.Change<Annotation>(revision,id,"value", (fun () -> Some(Text(Model.required "value" replacement))),"Annotation.setValueText")
    member _.setValueNumber(revision: RevisionToken, id: string, replacement: float) = session.Change<Annotation>(revision,id,"value", (fun () -> Some(Number(Model.number replacement))),"Annotation.setValueNumber")
