namespace ARCtrl.Internal

module internal Workspace =
    let path folder = Files.combine (Files.combine folder ".arc") "testing.sqlite"
    let create folder state baseline =
        Model.validate state
        Files.mkdir folder
        Files.mkdir (Files.combine folder ".arc")
        let database = Store.openDatabase (path folder)
        try Store.initialize database.Connection state baseline; Session(folder, database)
        with _ -> database.Close(); reraise()
    let load folder =
        let database = Store.openDatabase (path folder)
        try Session(folder, database) with _ -> database.Close(); reraise()
    let openFolder folder source =
        let folder = Files.fullPath folder
        let current = Files.readOptional (Files.combine folder "arc.yml")
        let hasSql = Files.exists (path folder)
        let fromYaml () =
            let state = current |> Option.defaultWith (fun () -> invalidOp "No arc.yml exists in this folder.") |> Codec.decodeGraph
            Model.validate state
            if hasSql then
                Files.replace (path folder) (Files.combine (Files.combine folder ".arc") ("testing-" + Files.newId() + ".sqlite"))
            create folder state current
        match source with
        | "yml" -> fromYaml()
        | "sql" ->
            if not hasSql then invalidOp "No SQL session exists in this folder."
            let session = load folder
            try
                session.AcceptPersistentBaseline(current)
                session
            with _ -> session.Close(); reraise()
        | "auto" when not hasSql -> fromYaml()
        | "auto" ->
            let database = Store.openDatabase (path folder)
            let oldBaseline, state, saved =
                try
                    let row = Store.metadata database.Connection
                    Store.optText row "baseline", row.GetByName("state").AsText() |> Codec.decodeState, Store.optText row "saved_graph"
                finally database.Close()
            Model.validate state
            if current = oldBaseline then load folder
            elif saved <> Some(Codec.encodeGraph state) || Model.sessionOnly state then
                invalidOp "Both persistent YAML and retained session state changed. Choose 'sql' or 'yml' explicitly."
            else fromYaml()
        | _ -> invalidArg "source" "Use 'auto', 'sql', or 'yml'."
