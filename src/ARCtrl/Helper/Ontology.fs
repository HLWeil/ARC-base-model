module internal ARCtrl.Helper.Ontology

let computeTanInfo (tan : string option) (tsr : string option) =
    match tan with
    | Some tan -> 
        match Regex.tryParseTermAnnotation tan with
        | Some ta -> Some ta
        | None ->
            match tsr with
            | Some "" | None -> None
            | Some tsr -> 
                Some {| IDSpace = tsr; LocalID = tan |}
    | None -> None

let tryGetTSR (tan : string) =
   computeTanInfo (Some tan) None
   |> Option.map (fun ta -> ta.IDSpace)
   
