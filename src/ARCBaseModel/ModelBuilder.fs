namespace ARCBaseModel

open System.Collections.Generic
open Fable.Core

// Draft identities are local to a builder; supplied domain IDs are never used.
type internal DraftState(owner:obj,prototype:EntityObject,writable:bool,key:string,cloned:bool) =
    inherit ImmutableObject()
    do base.SealObject()
    member _.Owner=owner
    member _.Prototype=prototype
    member _.Writable=writable
    member _.Key=key
    member _.Cloned=cloned

[<AttachMembers>]
type ModelToken<'T when 'T :> EntityObject> internal(state:DraftState) as this =
    inherit ImmutableObject()
    do this.SealObject()
    member internal _.State=state

[<AttachMembers>]
type ModelValue internal(build:(DraftState->EntityObject)->Entity,states:seq<DraftState>) as this =
    inherit ImmutableObject()
    let _states=Construction.collection states
    do this.SealObject()
    member internal _.States=_states
    member internal _.Build(resolve)=build resolve
    static member text(value:string)=ModelValue((fun _ -> Entity.Text(Construction.text "value" value)),Seq.empty)
    static member number(value:float)=
        if not (Construction.isNumber (box value)) then invalidArg "value" "Expected a number, excluding Boolean values."
        ModelValue((fun _ -> Entity.Number value),Seq.empty)
    static member boolean(value:bool)=
        if not (Construction.isBoolean (box value)) then invalidArg "value" "Expected a Boolean."
        ModelValue((fun _ -> Entity.Bool value),Seq.empty)
    static member nullValue()=ModelValue((fun _ -> Entity.Null(EntityNull())),Seq.empty)
    static member blob(base64:string)=
        let value=EntityBlob(base64)
        ModelValue((fun _ -> Entity.Blob value),Seq.empty)
    static member objectValue(value:ModelToken<'T>)=
        Construction.required "value" value |> ignore
        ModelValue((fun resolve -> Entity.Object(resolve value.State)),[value.State])
    static member collection(values:seq<ModelValue>)=
        let values=Construction.collection values
        ModelValue((fun resolve -> Entity.Collection(EntityCollection(values |> Seq.map (fun item -> item.Build(resolve))))),values |> Seq.collect (fun item -> item.States))

[<AttachMembers>]
type BuiltModel internal(owner:obj,values:Dictionary<DraftState,EntityObject>) as this =
    inherit ImmutableObject()
    let _values = values |> Seq.map (fun entry -> entry.Key.Key,entry.Value) |> Construction.map
    do this.SealObject()
    member _.get(token:ModelToken<'T>):'T =
        Construction.required "token" token |> ignore
        if not (obj.ReferenceEquals(owner,token.State.Owner)) then invalidArg "token" "Foreign builder token."
        if not (Construction.contains<EntityObject> _values token.State.Key) then invalidArg "token" "Unknown builder token."
        Construction.get<EntityObject> _values token.State.Key |> unbox<'T>

[<AttachMembers>]
type ModelBuilder() as this =
    let owner=obj()
    let drafts=ResizeArray<DraftState>()
    let edits=Dictionary<DraftState,Dictionary<string,(EntityObject->(DraftState->EntityObject)->unit)>>()
    let clones=Dictionary<EntityObject,DraftState>(HashIdentity.Reference)
    let mutable finished=false
    let check()=if finished then invalidOp "Builder has already completed."
    let checkState (state:DraftState) =
        check()
        if not (obj.ReferenceEquals(owner,state.Owner)) then invalidArg "token" "Foreign builder token."
    let stateOf (value:EntityObject) = match clones.TryGetValue(value) with true,state -> Some state | _ -> None
    member internal _.CheckState(state)=checkState state
    member internal _.CheckKind(state:DraftState,kind:string) =
        checkState state
        if kind <> "EntityObject" && state.Prototype.Type <> kind then invalidArg "token" "Unexpected entity kind."

    member internal _.Edit<'T when 'T :> EntityObject>(token:ModelToken<'T>,key:string,edit:EntityObject->(DraftState->EntityObject)->unit) =
        Construction.required "token" token |> ignore
        checkState token.State
        if not token.State.Writable then invalidArg "token" "Reference tokens cannot be edited."
        let changes=
            match edits.TryGetValue token.State with
            | true,value -> value
            | _ -> let value=Dictionary<string,(EntityObject->(DraftState->EntityObject)->unit)>() in edits.Add(token.State,value);value
        changes[key] <- edit
    member internal this.Scalar(token:ModelToken<'T>,key:string,value:obj) = this.Edit(token,key,fun entity _ -> entity.AssignInternal(key,value))
    member _.draft(value:'T):ModelToken<'T> =
        check()
        Construction.required "value" value |> ignore
        let state=DraftState(owner,value,true,string drafts.Count,false)
        drafts.Add(state)
        ModelToken<'T>(state)
    member _.reference(value:'T):ModelToken<'T> =
        check()
        Construction.required "value" value |> ignore
        let state=DraftState(owner,value,false,string drafts.Count,false)
        drafts.Add(state)
        ModelToken<'T>(state)
    member _.cloneGraph(value:'T):ModelToken<'T> =
        check()
        Construction.required "value" value |> ignore
        let pending=Stack<EntityObject>()
        pending.Push(value)
        while pending.Count>0 do
            let current=pending.Pop()
            match stateOf current with
            | Some _ -> ()
            | None ->
                let state=DraftState(owner,current,true,string drafts.Count,true)
                clones.Add(current,state)
                drafts.Add(state)
                for linked in ModelHydration.References(current) do pending.Push(linked)
        ModelToken<'T>(stateOf value |> Option.get)
    member _.draftFor(value:'T):ModelToken<'T> =
        check()
        match stateOf value with
        | Some state -> ModelToken<'T>(state)
        | None -> invalidArg "value" "Value was not cloned by this builder."
    member _.finish() =
        check()
        let values=Dictionary<DraftState,EntityObject>()
        for state in drafts do values.Add(state,if state.Writable then ModelHydration.Clone(state.Prototype) else state.Prototype)
        let resolve state =
            checkState state
            match values.TryGetValue(state) with true,value -> value | _ -> invalidArg "token" "Unknown token."
        for state in drafts do
            if state.Writable then
                let value=values[state]
                if state.Cloned then
                    ModelHydration.Remap(value,fun original -> match stateOf original with Some token -> resolve token | None -> original)
                match edits.TryGetValue state with
                | true,changes -> for edit in changes.Values do edit value resolve
                | _ -> ()
        ModelHydration.Seal(drafts |> Seq.filter (fun state -> state.Writable) |> Seq.map (fun state -> values[state]))
        let result=BuiltModel(owner,values)
        finished <- true
        result
    member _.Entity = EntityDraftOperations(this)
    member _.DefinedTermSet = DefinedTermSetDraftOperations(this)
    member _.DefinedTerm = DefinedTermDraftOperations(this)
    member _.Annotation = AnnotationDraftOperations(this)
    member _.FormalParameter = FormalParameterDraftOperations(this)
    member _.Sample = SampleDraftOperations(this)
    member _.Data = DataDraftOperations(this)
    member _.Recipe = RecipeDraftOperations(this)
    member _.Process = ProcessDraftOperations(this)
    member _.Descriptor = DescriptorDraftOperations(this)
    member _.Organization = OrganizationDraftOperations(this)
    member _.Agent = AgentDraftOperations(this)
    member _.ScholarlyArticle = ScholarlyArticleDraftOperations(this)
    member _.Dataset = DatasetDraftOperations(this)

and [<AttachMembers>] EntityDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<EntityObject>,value:string) =
        builder.CheckKind(token.State,"EntityObject")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<EntityObject>) =
        builder.CheckKind(token.State,"EntityObject")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<EntityObject>,value:seq<string>) =
        builder.CheckKind(token.State,"EntityObject")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setEntityProperty(token:ModelToken<EntityObject>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"EntityObject")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<EntityObject>,key:string) =
        builder.CheckKind(token.State,"EntityObject")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] DefinedTermSetDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<DefinedTermSet>,value:string) =
        builder.CheckKind(token.State,"DefinedTermSet")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<DefinedTermSet>) =
        builder.CheckKind(token.State,"DefinedTermSet")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<DefinedTermSet>,value:seq<string>) =
        builder.CheckKind(token.State,"DefinedTermSet")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<DefinedTermSet>,value:string) =
        builder.CheckKind(token.State,"DefinedTermSet")

        builder.Scalar(token,"name",box value)
    member _.setIdentifier(token:ModelToken<DefinedTermSet>,value:string) =
        builder.CheckKind(token.State,"DefinedTermSet")

        builder.Scalar(token,"identifier",box(Some value))
    member _.clearIdentifier(token:ModelToken<DefinedTermSet>) =
        builder.CheckKind(token.State,"DefinedTermSet")
        builder.Scalar(token,"identifier",box(None:string option))
    member _.setEntityProperty(token:ModelToken<DefinedTermSet>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"DefinedTermSet")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<DefinedTermSet>,key:string) =
        builder.CheckKind(token.State,"DefinedTermSet")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] DefinedTermDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<DefinedTerm>,value:string) =
        builder.CheckKind(token.State,"DefinedTerm")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<DefinedTerm>) =
        builder.CheckKind(token.State,"DefinedTerm")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<DefinedTerm>,value:seq<string>) =
        builder.CheckKind(token.State,"DefinedTerm")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<DefinedTerm>,value:string) =
        builder.CheckKind(token.State,"DefinedTerm")

        builder.Scalar(token,"name",box value)
    member _.setIdentifier(token:ModelToken<DefinedTerm>,value:string) =
        builder.CheckKind(token.State,"DefinedTerm")

        builder.Scalar(token,"identifier",box(Some value))
    member _.clearIdentifier(token:ModelToken<DefinedTerm>) =
        builder.CheckKind(token.State,"DefinedTerm")
        builder.Scalar(token,"identifier",box(None:string option))
    member _.setTAN(token:ModelToken<DefinedTerm>,value:string) =
        builder.CheckKind(token.State,"DefinedTerm")

        builder.Scalar(token,"tan",box(Some value))
    member _.clearTAN(token:ModelToken<DefinedTerm>) =
        builder.CheckKind(token.State,"DefinedTerm")
        builder.Scalar(token,"tan",box(None:string option))
    member _.setInDefinedTermSetUrl(token:ModelToken<DefinedTerm>,value:string) =
        builder.CheckKind(token.State,"DefinedTerm")

        builder.Edit(token,"inDefinedTermSet",fun entity resolve -> entity.AssignInternal("inDefinedTermSet",box(Some(DefinedTermSetReference.Url value))))
    member _.setInDefinedTermSetTermSet(token:ModelToken<DefinedTerm>,value:ModelToken<DefinedTermSet>) =
        builder.CheckKind(token.State,"DefinedTerm")

        builder.CheckState(value.State)
        builder.Edit(token,"inDefinedTermSet",fun entity resolve -> entity.AssignInternal("inDefinedTermSet",box(Some(DefinedTermSetReference.TermSet(resolve value.State |> unbox<DefinedTermSet>)))))
    member _.clearInDefinedTermSet(token:ModelToken<DefinedTerm>) =
        builder.CheckKind(token.State,"DefinedTerm")
        builder.Scalar(token,"inDefinedTermSet",box(None:DefinedTermSetReference option))
    member _.setEntityProperty(token:ModelToken<DefinedTerm>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"DefinedTerm")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<DefinedTerm>,key:string) =
        builder.CheckKind(token.State,"DefinedTerm")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] AnnotationDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Annotation>,value:string) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Annotation>,value:seq<string>) =
        builder.CheckKind(token.State,"Annotation")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<Annotation>,value:string) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"name",box value)
    member _.setValue(token:ModelToken<Annotation>,value:AnnotationValue) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"value",box(Some value))
    member _.clearValue(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"value",box(None:AnnotationValue option))
    member _.setUnit(token:ModelToken<Annotation>,value:string) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"unit",box(Some value))
    member _.clearUnit(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"unit",box(None:string option))
    member _.setNameTAN(token:ModelToken<Annotation>,value:string) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"nameTAN",box(Some value))
    member _.clearNameTAN(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"nameTAN",box(None:string option))
    member _.setValueTAN(token:ModelToken<Annotation>,value:string) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"valueTAN",box(Some value))
    member _.clearValueTAN(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"valueTAN",box(None:string option))
    member _.setUnitTAN(token:ModelToken<Annotation>,value:string) =
        builder.CheckKind(token.State,"Annotation")

        builder.Scalar(token,"unitTAN",box(Some value))
    member _.clearUnitTAN(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"unitTAN",box(None:string option))
    member _.setInstanceOf(token:ModelToken<Annotation>,value:ModelToken<FormalParameter>) =
        builder.CheckKind(token.State,"Annotation")

        builder.CheckState(value.State)
        builder.Edit(token,"instanceOf",fun entity resolve -> entity.AssignInternal("instanceOf",box(Some(resolve value.State |> unbox<FormalParameter>))))
    member _.clearInstanceOf(token:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"Annotation")
        builder.Scalar(token,"instanceOf",box(None:FormalParameter option))
    member _.setEntityProperty(token:ModelToken<Annotation>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Annotation")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Annotation>,key:string) =
        builder.CheckKind(token.State,"Annotation")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] FormalParameterDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<FormalParameter>,value:string) =
        builder.CheckKind(token.State,"FormalParameter")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<FormalParameter>) =
        builder.CheckKind(token.State,"FormalParameter")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<FormalParameter>,value:seq<string>) =
        builder.CheckKind(token.State,"FormalParameter")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<FormalParameter>,value:string) =
        builder.CheckKind(token.State,"FormalParameter")

        builder.Scalar(token,"name",box(Some value))
    member _.clearName(token:ModelToken<FormalParameter>) =
        builder.CheckKind(token.State,"FormalParameter")
        builder.Scalar(token,"name",box(None:string option))
    member _.setNameTAN(token:ModelToken<FormalParameter>,value:string) =
        builder.CheckKind(token.State,"FormalParameter")

        builder.Scalar(token,"nameTAN",box(Some value))
    member _.clearNameTAN(token:ModelToken<FormalParameter>) =
        builder.CheckKind(token.State,"FormalParameter")
        builder.Scalar(token,"nameTAN",box(None:string option))
    member _.setDefaultValue(token:ModelToken<FormalParameter>,value:ModelToken<Annotation>) =
        builder.CheckKind(token.State,"FormalParameter")

        builder.CheckState(value.State)
        builder.Edit(token,"defaultValue",fun entity resolve -> entity.AssignInternal("defaultValue",box(Some(resolve value.State |> unbox<Annotation>))))
    member _.clearDefaultValue(token:ModelToken<FormalParameter>) =
        builder.CheckKind(token.State,"FormalParameter")
        builder.Scalar(token,"defaultValue",box(None:Annotation option))
    member _.setEntityProperty(token:ModelToken<FormalParameter>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"FormalParameter")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<FormalParameter>,key:string) =
        builder.CheckKind(token.State,"FormalParameter")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] SampleDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Sample>,value:string) =
        builder.CheckKind(token.State,"Sample")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Sample>) =
        builder.CheckKind(token.State,"Sample")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Sample>,value:seq<string>) =
        builder.CheckKind(token.State,"Sample")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<Sample>,value:string) =
        builder.CheckKind(token.State,"Sample")

        builder.Scalar(token,"name",box value)
    member _.setAdditionalProperties(token:ModelToken<Sample>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Sample")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"additionalProperties",fun entity resolve -> entity.AssignInternal("additionalProperties",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setEntityProperty(token:ModelToken<Sample>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Sample")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Sample>,key:string) =
        builder.CheckKind(token.State,"Sample")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] DataDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Data>,value:string) =
        builder.CheckKind(token.State,"Data")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Data>) =
        builder.CheckKind(token.State,"Data")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Data>,value:seq<string>) =
        builder.CheckKind(token.State,"Data")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setPath(token:ModelToken<Data>,value:string) =
        builder.CheckKind(token.State,"Data")

        builder.Scalar(token,"path",box value)
    member _.setSelector(token:ModelToken<Data>,value:string) =
        builder.CheckKind(token.State,"Data")

        builder.Scalar(token,"selector",box(Some value))
    member _.clearSelector(token:ModelToken<Data>) =
        builder.CheckKind(token.State,"Data")
        builder.Scalar(token,"selector",box(None:string option))
    member _.setSelectorFormat(token:ModelToken<Data>,value:string) =
        builder.CheckKind(token.State,"Data")

        builder.Scalar(token,"selectorFormat",box(Some value))
    member _.clearSelectorFormat(token:ModelToken<Data>) =
        builder.CheckKind(token.State,"Data")
        builder.Scalar(token,"selectorFormat",box(None:string option))
    member _.setEncodingFormat(token:ModelToken<Data>,value:string) =
        builder.CheckKind(token.State,"Data")

        builder.Scalar(token,"encodingFormat",box(Some value))
    member _.clearEncodingFormat(token:ModelToken<Data>) =
        builder.CheckKind(token.State,"Data")
        builder.Scalar(token,"encodingFormat",box(None:string option))
    member _.setHasParts(token:ModelToken<Data>,values:seq<ModelToken<Data>>) =
        builder.CheckKind(token.State,"Data")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"hasParts",fun entity resolve -> entity.AssignInternal("hasParts",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Data>))))
    member _.setAdditionalProperties(token:ModelToken<Data>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Data")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"additionalProperties",fun entity resolve -> entity.AssignInternal("additionalProperties",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setEntityProperty(token:ModelToken<Data>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Data")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Data>,key:string) =
        builder.CheckKind(token.State,"Data")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] RecipeDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Recipe>,value:string) =
        builder.CheckKind(token.State,"Recipe")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Recipe")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Recipe>,value:seq<string>) =
        builder.CheckKind(token.State,"Recipe")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<Recipe>,value:string) =
        builder.CheckKind(token.State,"Recipe")

        builder.Scalar(token,"name",box(Some value))
    member _.clearName(token:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Recipe")
        builder.Scalar(token,"name",box(None:string option))
    member _.setParameters(token:ModelToken<Recipe>,values:seq<ModelToken<FormalParameter>>) =
        builder.CheckKind(token.State,"Recipe")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"parameters",fun entity resolve -> entity.AssignInternal("parameters",box(values |> Seq.map (fun value -> resolve value.State |> unbox<FormalParameter>))))
    member _.setDescription(token:ModelToken<Recipe>,value:string) =
        builder.CheckKind(token.State,"Recipe")

        builder.Scalar(token,"description",box(Some value))
    member _.clearDescription(token:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Recipe")
        builder.Scalar(token,"description",box(None:string option))
    member _.setIntendedUseText(token:ModelToken<Recipe>,value:string) =
        builder.CheckKind(token.State,"Recipe")

        builder.Edit(token,"intendedUse",fun entity resolve -> entity.AssignInternal("intendedUse",box(Some(RecipeIntendedUse.Text value))))
    member _.setIntendedUseTerm(token:ModelToken<Recipe>,value:ModelToken<DefinedTerm>) =
        builder.CheckKind(token.State,"Recipe")

        builder.CheckState(value.State)
        builder.Edit(token,"intendedUse",fun entity resolve -> entity.AssignInternal("intendedUse",box(Some(RecipeIntendedUse.Term(resolve value.State |> unbox<DefinedTerm>)))))
    member _.clearIntendedUse(token:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Recipe")
        builder.Scalar(token,"intendedUse",box(None:RecipeIntendedUse option))
    member _.setAdditionalProperties(token:ModelToken<Recipe>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Recipe")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"additionalProperties",fun entity resolve -> entity.AssignInternal("additionalProperties",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setComponents(token:ModelToken<Recipe>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Recipe")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"components",fun entity resolve -> entity.AssignInternal("components",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setVersion(token:ModelToken<Recipe>,value:string) =
        builder.CheckKind(token.State,"Recipe")

        builder.Scalar(token,"version",box(Some value))
    member _.clearVersion(token:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Recipe")
        builder.Scalar(token,"version",box(None:string option))
    member _.setUrl(token:ModelToken<Recipe>,value:string) =
        builder.CheckKind(token.State,"Recipe")

        builder.Scalar(token,"url",box(Some value))
    member _.clearUrl(token:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Recipe")
        builder.Scalar(token,"url",box(None:string option))
    member _.setEntityProperty(token:ModelToken<Recipe>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Recipe")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Recipe>,key:string) =
        builder.CheckKind(token.State,"Recipe")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] ProcessDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Process>,value:string) =
        builder.CheckKind(token.State,"Process")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Process>) =
        builder.CheckKind(token.State,"Process")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Process>,value:seq<string>) =
        builder.CheckKind(token.State,"Process")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<Process>,value:string) =
        builder.CheckKind(token.State,"Process")

        builder.Scalar(token,"name",box value)
    member _.setInputSample(token:ModelToken<Process>,value:ModelToken<Sample>) =
        builder.CheckKind(token.State,"Process")

        builder.CheckKind(value.State,"Sample")
        builder.Edit(token,"input",fun entity resolve -> entity.AssignInternal("input",box(Some(EntityReference.Sample(resolve value.State |> unbox<Sample>)))))
    member _.setInputData(token:ModelToken<Process>,value:ModelToken<Data>) =
        builder.CheckKind(token.State,"Process")

        builder.CheckKind(value.State,"Data")
        builder.Edit(token,"input",fun entity resolve -> entity.AssignInternal("input",box(Some(EntityReference.Data(resolve value.State |> unbox<Data>)))))
    member _.clearInput(token:ModelToken<Process>) =
        builder.CheckKind(token.State,"Process")
        builder.Scalar(token,"input",box(None:EntityReference option))
    member _.setOutputSample(token:ModelToken<Process>,value:ModelToken<Sample>) =
        builder.CheckKind(token.State,"Process")

        builder.CheckKind(value.State,"Sample")
        builder.Edit(token,"output",fun entity resolve -> entity.AssignInternal("output",box(Some(EntityReference.Sample(resolve value.State |> unbox<Sample>)))))
    member _.setOutputData(token:ModelToken<Process>,value:ModelToken<Data>) =
        builder.CheckKind(token.State,"Process")

        builder.CheckKind(value.State,"Data")
        builder.Edit(token,"output",fun entity resolve -> entity.AssignInternal("output",box(Some(EntityReference.Data(resolve value.State |> unbox<Data>)))))
    member _.clearOutput(token:ModelToken<Process>) =
        builder.CheckKind(token.State,"Process")
        builder.Scalar(token,"output",box(None:EntityReference option))
    member _.setExecutesRecipe(token:ModelToken<Process>,value:ModelToken<Recipe>) =
        builder.CheckKind(token.State,"Process")

        builder.CheckState(value.State)
        builder.Edit(token,"executesRecipe",fun entity resolve -> entity.AssignInternal("executesRecipe",box(Some(resolve value.State |> unbox<Recipe>))))
    member _.clearExecutesRecipe(token:ModelToken<Process>) =
        builder.CheckKind(token.State,"Process")
        builder.Scalar(token,"executesRecipe",box(None:Recipe option))
    member _.setParameterValues(token:ModelToken<Process>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Process")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"parameterValues",fun entity resolve -> entity.AssignInternal("parameterValues",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setEntityProperty(token:ModelToken<Process>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Process")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Process>,key:string) =
        builder.CheckKind(token.State,"Process")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] DescriptorDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Descriptor>,value:string) =
        builder.CheckKind(token.State,"Descriptor")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Descriptor>) =
        builder.CheckKind(token.State,"Descriptor")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Descriptor>,value:seq<string>) =
        builder.CheckKind(token.State,"Descriptor")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setDescribesSample(token:ModelToken<Descriptor>,value:ModelToken<Sample>) =
        builder.CheckKind(token.State,"Descriptor")

        builder.CheckKind(value.State,"Sample")
        builder.Edit(token,"describes",fun entity resolve -> entity.AssignInternal("describes",box(EntityReference.Sample(resolve value.State |> unbox<Sample>))))
    member _.setDescribesData(token:ModelToken<Descriptor>,value:ModelToken<Data>) =
        builder.CheckKind(token.State,"Descriptor")

        builder.CheckKind(value.State,"Data")
        builder.Edit(token,"describes",fun entity resolve -> entity.AssignInternal("describes",box(EntityReference.Data(resolve value.State |> unbox<Data>))))
    member _.setAnnotations(token:ModelToken<Descriptor>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Descriptor")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"annotations",fun entity resolve -> entity.AssignInternal("annotations",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setEntityProperty(token:ModelToken<Descriptor>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Descriptor")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Descriptor>,key:string) =
        builder.CheckKind(token.State,"Descriptor")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] OrganizationDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Organization>,value:string) =
        builder.CheckKind(token.State,"Organization")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Organization>) =
        builder.CheckKind(token.State,"Organization")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Organization>,value:seq<string>) =
        builder.CheckKind(token.State,"Organization")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<Organization>,value:string) =
        builder.CheckKind(token.State,"Organization")

        builder.Scalar(token,"name",box value)
    member _.setUrl(token:ModelToken<Organization>,value:string) =
        builder.CheckKind(token.State,"Organization")

        builder.Scalar(token,"url",box(Some value))
    member _.clearUrl(token:ModelToken<Organization>) =
        builder.CheckKind(token.State,"Organization")
        builder.Scalar(token,"url",box(None:string option))
    member _.setEntityProperty(token:ModelToken<Organization>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Organization")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Organization>,key:string) =
        builder.CheckKind(token.State,"Organization")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] AgentDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Agent>,value:string) =
        builder.CheckKind(token.State,"Agent")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Agent>) =
        builder.CheckKind(token.State,"Agent")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Agent>,value:seq<string>) =
        builder.CheckKind(token.State,"Agent")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setName(token:ModelToken<Agent>,value:string) =
        builder.CheckKind(token.State,"Agent")

        builder.Scalar(token,"name",box value)
    member _.setGivenName(token:ModelToken<Agent>,value:string) =
        builder.CheckKind(token.State,"Agent")

        builder.Scalar(token,"givenName",box(Some value))
    member _.clearGivenName(token:ModelToken<Agent>) =
        builder.CheckKind(token.State,"Agent")
        builder.Scalar(token,"givenName",box(None:string option))
    member _.setFamilyName(token:ModelToken<Agent>,value:string) =
        builder.CheckKind(token.State,"Agent")

        builder.Scalar(token,"familyName",box(Some value))
    member _.clearFamilyName(token:ModelToken<Agent>) =
        builder.CheckKind(token.State,"Agent")
        builder.Scalar(token,"familyName",box(None:string option))
    member _.setEmails(token:ModelToken<Agent>,value:seq<string>) =
        builder.CheckKind(token.State,"Agent")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"emails",box value)
    member _.setAffiliations(token:ModelToken<Agent>,values:seq<ModelToken<Organization>>) =
        builder.CheckKind(token.State,"Agent")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"affiliations",fun entity resolve -> entity.AssignInternal("affiliations",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Organization>))))
    member _.setIdentifiers(token:ModelToken<Agent>,value:seq<string>) =
        builder.CheckKind(token.State,"Agent")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"identifiers",box value)
    member _.setAdditionalProperties(token:ModelToken<Agent>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Agent")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"additionalProperties",fun entity resolve -> entity.AssignInternal("additionalProperties",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setJobTitles(token:ModelToken<Agent>,values:seq<ModelToken<DefinedTerm>>) =
        builder.CheckKind(token.State,"Agent")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"jobTitles",fun entity resolve -> entity.AssignInternal("jobTitles",box(values |> Seq.map (fun value -> resolve value.State |> unbox<DefinedTerm>))))
    member _.setEntityProperty(token:ModelToken<Agent>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Agent")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Agent>,key:string) =
        builder.CheckKind(token.State,"Agent")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] ScholarlyArticleDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<ScholarlyArticle>,value:string) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<ScholarlyArticle>) =
        builder.CheckKind(token.State,"ScholarlyArticle")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<ScholarlyArticle>,value:seq<string>) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setHeadline(token:ModelToken<ScholarlyArticle>,value:string) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        builder.Scalar(token,"headline",box value)
    member _.setIdentifiers(token:ModelToken<ScholarlyArticle>,value:seq<string>) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"identifiers",box value)
    member _.setAuthors(token:ModelToken<ScholarlyArticle>,values:seq<ModelToken<Agent>>) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"authors",fun entity resolve -> entity.AssignInternal("authors",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Agent>))))
    member _.setCreativeWorkStatus(token:ModelToken<ScholarlyArticle>,value:ModelToken<DefinedTerm>) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        builder.CheckState(value.State)
        builder.Edit(token,"creativeWorkStatus",fun entity resolve -> entity.AssignInternal("creativeWorkStatus",box(Some(resolve value.State |> unbox<DefinedTerm>))))
    member _.clearCreativeWorkStatus(token:ModelToken<ScholarlyArticle>) =
        builder.CheckKind(token.State,"ScholarlyArticle")
        builder.Scalar(token,"creativeWorkStatus",box(None:DefinedTerm option))
    member _.setAdditionalProperties(token:ModelToken<ScholarlyArticle>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"additionalProperties",fun entity resolve -> entity.AssignInternal("additionalProperties",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setEntityProperty(token:ModelToken<ScholarlyArticle>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"ScholarlyArticle")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<ScholarlyArticle>,key:string) =
        builder.CheckKind(token.State,"ScholarlyArticle")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))

and [<AttachMembers>] DatasetDraftOperations internal(builder:ModelBuilder) =
    member _.setId(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"id",box(Some value))
    member _.clearId(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"id",box(None:string option))
    member _.setAdditionalTypes(token:ModelToken<Dataset>,value:seq<string>) =
        builder.CheckKind(token.State,"Dataset")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"additionalTypes",box value)
    member _.setConformsTo(token:ModelToken<Dataset>,value:seq<string>) =
        builder.CheckKind(token.State,"Dataset")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"conformsTo",box value)
    member _.setIdentifiers(token:ModelToken<Dataset>,value:seq<string>) =
        builder.CheckKind(token.State,"Dataset")

        let value=Construction.collection value :> seq<string>
        builder.Scalar(token,"identifiers",box value)
    member _.setTitle(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"title",box(Some value))
    member _.clearTitle(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"title",box(None:string option))
    member _.setDescription(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"description",box(Some value))
    member _.clearDescription(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"description",box(None:string option))
    member _.setLicense(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"license",box(Some value))
    member _.clearLicense(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"license",box(None:string option))
    member _.setDatePublished(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"datePublished",box(Some value))
    member _.clearDatePublished(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"datePublished",box(None:string option))
    member _.setDateCreated(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"dateCreated",box(Some value))
    member _.clearDateCreated(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"dateCreated",box(None:string option))
    member _.setDateModified(token:ModelToken<Dataset>,value:string) =
        builder.CheckKind(token.State,"Dataset")

        builder.Scalar(token,"dateModified",box(Some value))
    member _.clearDateModified(token:ModelToken<Dataset>) =
        builder.CheckKind(token.State,"Dataset")
        builder.Scalar(token,"dateModified",box(None:string option))
    member _.setHasParts(token:ModelToken<Dataset>,values:seq<ModelToken<Dataset>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"hasParts",fun entity resolve -> entity.AssignInternal("hasParts",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Dataset>))))
    member _.setDataFiles(token:ModelToken<Dataset>,values:seq<ModelToken<Data>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"dataFiles",fun entity resolve -> entity.AssignInternal("dataFiles",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Data>))))
    member _.setAgents(token:ModelToken<Dataset>,values:seq<ModelToken<Agent>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"agents",fun entity resolve -> entity.AssignInternal("agents",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Agent>))))
    member _.setCitations(token:ModelToken<Dataset>,values:seq<ModelToken<ScholarlyArticle>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"citations",fun entity resolve -> entity.AssignInternal("citations",box(values |> Seq.map (fun value -> resolve value.State |> unbox<ScholarlyArticle>))))
    member _.setProcesses(token:ModelToken<Dataset>,values:seq<ModelToken<Process>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"processes",fun entity resolve -> entity.AssignInternal("processes",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Process>))))
    member _.setDescriptors(token:ModelToken<Dataset>,values:seq<ModelToken<Descriptor>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"descriptors",fun entity resolve -> entity.AssignInternal("descriptors",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Descriptor>))))
    member _.setAdditionalProperties(token:ModelToken<Dataset>,values:seq<ModelToken<Annotation>>) =
        builder.CheckKind(token.State,"Dataset")

        let values=Construction.collection values
        for value in values do builder.CheckState(value.State)
        builder.Edit(token,"additionalProperties",fun entity resolve -> entity.AssignInternal("additionalProperties",box(values |> Seq.map (fun value -> resolve value.State |> unbox<Annotation>))))
    member _.setEntityProperty(token:ModelToken<Dataset>,key:string,value:ModelValue) =
        builder.CheckKind(token.State,"Dataset")

        for state in value.States do builder.CheckState(state)
        builder.Edit(token,"extension:"+key,fun entity resolve -> entity.SetExtensionInternal(key,value.Build(resolve)))
    member _.removeEntityProperty(token:ModelToken<Dataset>,key:string) =
        builder.CheckKind(token.State,"Dataset")
        builder.Edit(token,"extension:"+key,fun entity _ -> entity.RemoveExtensionInternal(key))
