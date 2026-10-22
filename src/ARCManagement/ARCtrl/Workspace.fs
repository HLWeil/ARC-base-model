namespace ARCtrl.Internal

open ARCManagement
open ARCManagement.Helper
open ARCManagement.Internal
open ARCtrl.Helper

/// Workspace locations, publication, baselines, and source-selection policy.
module internal Workspace =

    let databaseFileName = "arc.sqlite"

    let path folder = Path.combineNative (Path.combineNative folder Layout.ARCConfigFolderName) databaseFileName

    let import (repository: Repository) folder =
        let folder = Path.fullPath(Model.required "folder" folder)
        let current = Path.readFileText(Path.combineNative folder Layout.ARCFileName)
        let root = DatasetYaml.decode(current)
        ArcFactory.createWithMetadata repository root (Some folder) (Some current)

    let bind (context: GraphContext) folder =
        let folder = Path.fullPath(Model.required "folder" folder)
        context.SetBinding(folder, fun () ->
            if Path.pathExists(Path.combineNative folder Layout.ARCFileName) then invalidOp "Folder already contains arc.yml. Use ARC.importFolder.")

    let save (context: GraphContext) =
        context.Checkpoint(fun stored ->
            let folder = stored.Folder |> Option.defaultWith (fun () -> invalidOp "ARC has no folder binding. Use ARC.bind before save.")
            let rootPath = Path.combineNative folder Layout.ARCFileName
            if Path.readFileTextOptional rootPath <> stored.Baseline then invalidOp "Persistent YAML changed externally. Reopen with an explicit source preference."
            let graph = Codec.encodeGraph stored.State
            let temporary = Path.combineNative folder (".arc-" + ARCManagement.Helper.Identifier.newId() + ".tmp")
            Path.createDirectory folder
            try
                Path.writeFileText temporary graph
                if Path.readFileTextOptional rootPath <> stored.Baseline then invalidOp "Persistent YAML changed during save."
                Path.replaceFile temporary rootPath
                graph
            finally
                if Path.pathExists temporary then Path.deleteFile temporary)

    let recover (context: GraphContext) source =
        let folder = context.Folder |> Option.defaultWith (fun () -> invalidOp "ARC has no folder binding.")
        let rootPath = Path.combineNative folder Layout.ARCFileName
        // The folder is read inside the write transaction, so the decision uses the state that will be replaced.
        let replacement (current: string option) =
            let yaml = current |> Option.defaultWith (fun () -> invalidOp "No arc.yml exists in this folder.")
            DatasetYaml.decode(yaml) |> ArcFactory.snapshot, yaml
        match source with
        | "sql" -> context.AcceptPersistentBaseline(Path.readFileTextOptional rootPath)
        | "yml" -> context.Reload(fun _ -> Some(replacement (Path.readFileTextOptional rootPath)))
        | "auto" ->
            context.Reload(fun previous ->
                let current = Path.readFileTextOptional rootPath
                if current = previous.Baseline then None
                elif previous.SavedGraph <> Some(Codec.encodeGraph previous.State) || Model.sessionOnly previous.State then
                    invalidOp "Both persistent YAML and retained session state changed. Choose 'sql' or 'yml' explicitly."
                else Some(replacement current))
        | _ -> invalidArg "source" "Use 'auto', 'sql', or 'yml'."

    let openFolder folder source =
        let folder = Path.fullPath(Model.required "folder" folder)
        if not (List.contains source ["auto";"sql";"yml"]) then invalidArg "source" "Use 'auto', 'sql', or 'yml'."
        let existing = Path.pathExists(path folder)
        if source = "sql" && not existing then invalidOp "No SQL session exists in this folder."
        if not existing then
            Path.readFileText(Path.combineNative folder Layout.ARCFileName) |> DatasetYaml.decode |> ArcFactory.snapshot |> ignore
            Path.createDirectory(Path.combineNative folder Layout.ARCConfigFolderName)
        let session = if existing then Session.openFile(path folder) else Session.createFile(path folder)
        try
            let graph =
                if not existing then ManagedGraph.Wrap(import session.Repository folder)
                else
                    let rows = session.Repository.Access(fun () -> session.Repository.Connection.Query("SELECT arc_id,folder FROM arc"))
                    if rows.Count <> 1 then invalidOp "Folder convenience open requires exactly one ARC entry. Use ARCManagement.Session.openFile."
                    if Store.optText rows[0] "folder" <> Some folder then invalidOp "Repository ARC is bound to another folder. Use ARCManagement.Session.openFile."
                    let graph = session.openArc(rows[0].GetByName("arc_id").AsText())
                    recover graph.Context source
                    graph
            session,graph
        with _ ->
            session.close()
            if not existing then Path.deleteFile(path folder)
            reraise()
