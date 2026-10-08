namespace ARCtrl.Internal

open ARCSession.Internal

/// Folder policy belongs to ARC, independently of database opening.
module internal Workspace =
    let path folder = Files.combine (Files.combine folder ".arc") "testing.sqlite"
    let bind (context: Session) folder =
        let folder = Files.fullPath(Model.required "folder" folder)
        context.Repository.Access(fun () ->
            context.Check()
            if context.Folder <> Some folder then
                if Files.exists(Files.combine folder "arc.yml") then invalidOp "Folder already contains arc.yml. Use ARC.importFolder."
                context.Repository.Connection.WithTransaction(fun () -> context.SetBinding(folder)))
    let save (context: Session) =
        context.Repository.Access(fun () ->
            context.Check()
            let folder = context.Folder |> Option.defaultWith (fun () -> invalidOp "ARC has no folder binding. Use bindFolder before save.")
            let rootPath = Files.combine folder "arc.yml"
            if Files.readOptional rootPath <> context.Baseline then invalidOp "Persistent YAML changed externally. Reopen with an explicit source preference."
            let graph = Codec.encodeGraph context.State
            let temporary = Files.combine folder (".arc-" + Files.newId() + ".tmp")
            Files.mkdir folder
            try
                context.Repository.Connection.WithTransaction(fun () ->
                    context.AssertRevision()
                    Files.write temporary graph
                    if Files.readOptional rootPath <> context.Baseline then invalidOp "Persistent YAML changed during save."
                    Files.replace temporary rootPath
                    context.SetCheckpoint(graph))
                context.AcceptCheckpoint(graph)
            finally
                if Files.exists temporary then Files.remove temporary)
    let recover (context: Session) source =
        context.Repository.Access(fun () ->
            context.Check()
            let folder = context.Folder |> Option.defaultWith (fun () -> invalidOp "ARC has no folder binding.")
            let current = Files.readOptional(Files.combine folder "arc.yml")
            let reload () =
                let yaml = current |> Option.defaultWith (fun () -> invalidOp "No arc.yml exists in this folder.")
                let state = Codec.decodeGraph yaml
                Model.validate state
                context.Reload(state,yaml)
            match source with
            | "sql" -> context.AcceptPersistentBaseline(current)
            | "yml" -> reload()
            | "auto" when current = context.Baseline -> context.AssertRevision()
            | "auto" when context.IsDirty || context.HasSessionOnlyObjects -> invalidOp "Both persistent YAML and retained session state changed. Choose 'sql' or 'yml' explicitly."
            | "auto" -> reload()
            | _ -> invalidArg "source" "Use 'auto', 'sql', or 'yml'.")
    let openFolder folder source =
        let folder = Files.fullPath(Model.required "folder" folder)
        if not (List.contains source ["auto";"sql";"yml"]) then invalidArg "source" "Use 'auto', 'sql', or 'yml'."
        let existing = Files.exists(path folder)
        if source = "sql" && not existing then invalidOp "No SQL session exists in this folder."
        if not existing then
            Files.read(Files.combine folder "arc.yml") |> Codec.decodeGraph |> Model.validate
            Files.mkdir(Files.combine folder ".arc")
        let repository = if existing then Repository.OpenFile(path folder) else Repository.CreateFile(path folder)
        try
            if not existing then ArcFactory.import repository folder
            else
                let rows = repository.Connection.Query("SELECT arc_id,folder FROM session")
                if rows.Count <> 1 then invalidOp "Folder convenience open requires exactly one ARC entry. Use ARCSession.Session.openFile."
                if Store.optText rows[0] "folder" <> Some folder then invalidOp "Repository ARC is bound to another folder. Use ARCSession.Session.openFile."
                let context = ArcFactory.load repository (rows[0].GetByName("arc_id").AsText())
                recover context source
                context
        with _ ->
            repository.Close()
            if not existing then Files.remove(path folder)
            reraise()
