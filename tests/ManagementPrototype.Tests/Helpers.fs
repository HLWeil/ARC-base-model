module ManagementPrototype.Tests.Helpers

open System
open ARCtrl.Helper
open Fable.Pyxpecto

let folder category =
    let path = Path.fullPath(Path.combineNative ("build/out/arc-session/fixtures/" + category) (Identifier.newId()))
    Path.createDirectory path
    path

let tests = testList "Helpers" [
    testCase "text IO and listings distinguish files from directories" (fun _ ->
        let root = folder "helpers"
        let file = Path.combineNative root "unicode.txt"
        let child = Path.combineNative root "child"
        Path.createDirectory child
        Expect.isNone (Path.readFileTextOptional file) "Absent text"
        Path.writeFileText file "Grüße α\r\n"
        Path.appendFileText file "世界"
        Expect.equal (Path.readFileLines file) [|"Grüße α"; "世界"|] "UTF-8 and line endings"
        Expect.equal (Path.readFileTextOptional file) (Some(Path.readFileText file)) "Optional read"
        Expect.isTrue (Path.pathExists root) "Directory exists"
        Expect.isTrue (Path.directoryExists root) "Directory"
        Expect.isFalse (Path.fileExists root) "Directory is not a file"
        Expect.isFalse (Path.directoryExists file) "File is not a directory"
        Expect.equal (Path.getSubFiles root) [|file|] "Files only"
        Expect.equal (Path.getSubDirectories root) [|child|] "Directories only")

    testCase "exclusive creation and replacement preserve the destination" (fun _ ->
        let root = folder "helpers"
        let destination = Path.combineNative root "destination.txt"
        let staged = Path.combineNative root "staged.txt"
        Path.createFileExclusive destination
        Path.writeFileText destination "original"
        Expect.throws (fun () -> Path.createFileExclusive destination) "Reject existing file"
        Expect.equal (Path.readFileText destination) "original" "No truncation"
        Path.writeFileText staged "replacement"
        Path.replaceFile staged destination
        Expect.equal (Path.readFileText destination) "replacement" "Published contents"
        Expect.isFalse (Path.pathExists staged) "Staging file consumed")

    testCase "binary IO preserves every byte" (fun _ ->
        let file = Path.combineNative (folder "helpers") "bytes.bin"
        let bytes = [|0uy; 127uy; 128uy; 255uy|]
        Path.writeFileBinary file bytes
        Expect.equal (Path.readFileBinary file |> Seq.toArray) bytes "Native byte representation"
        Path.writeFileBinary file [||]
        Expect.equal (Path.readFileBinary file |> Seq.length) 0 "Empty bytes")

    testCaseAsync "async filesystem helpers share the synchronous adapters" (async {
        let root = folder "helpers"
        let file = Path.combineNative (Path.combineNative root "nested") "source.txt"
        do! Path.ensureDirectoryOfFileAsync file |> CrossAsync.asAsync
        do! Path.writeFileTextAsync file "contents" |> CrossAsync.asAsync
        let! text = Path.readFileTextAsync file |> CrossAsync.asAsync
        Expect.equal text (Path.readFileText file) "Same read behavior"
        let! paths = Path.getAllFilePathsAsync root |> CrossAsync.asAsync
        Expect.equal paths [|"/nested/source.txt"|] "Relative, slash-normalized paths"
        let destination = Path.combineNative root "moved.txt"
        do! Path.renameFileOrDirectoryAsync file destination |> CrossAsync.asAsync
        Expect.equal (Path.readFileText destination) "contents" "Move"
        do! Path.deleteFileOrDirectoryAsync destination |> CrossAsync.asAsync
        do! Path.deleteFileAsync destination |> CrossAsync.asAsync
        Expect.isFalse (Path.fileExists destination) "Missing-safe async deletion"
    })

    testCase "collection replacement preserves references duplicates and self-input" (fun _ ->
        let first, second = obj(), obj()
        let target = ResizeArray<obj>([first])
        ResizeArray.replace target [second; first; second]
        ResizeArray.replace target target
        Expect.equal target.Count 3 "Duplicates and self-input retained"
        Expect.isTrue (Object.ReferenceEquals(target[0],second)) "First reference"
        Expect.isTrue (Object.ReferenceEquals(target[1],first)) "Second reference"
        Expect.isTrue (Object.ReferenceEquals(target[2],second)) "Repeated reference"
        Expect.equal (ResizeArray.map (fun _ -> 1) target |> Seq.toArray) [|1;1;1|] "Mapping preserves order and duplicates")

    testCase "generated identities and legacy path parsing are separate helpers" (fun _ ->
        let first, second = Identifier.newId(), Identifier.newId()
        Expect.equal first.Length 32 "Existing generated ID shape"
        Expect.isTrue (first |> Seq.forall (fun c -> "0123456789abcdef".Contains(string c))) "Hex ID"
        Expect.isFalse (first = second) "Independent IDs"
        Expect.equal (Identifier.Assay.identifierFromFileName "assays/example/isa.assay.xlsx") "example" "Named regex group"
        Expect.equal (Path.combineMany ["studies/"; "/example/"; Path.StudyFileName]) "studies/example/isa.study.xlsx" "Portable ARC paths"
        Expect.equal (Path.split "./studies\\example/isa.study.xlsx") [|"studies";"example";"isa.study.xlsx"|] "Mixed separators"
        Expect.isNone (Option.fromValueWithDefault 0 0) "Default absence"
        Expect.equal (Option.fromValueWithDefault 0 1) (Some 1) "Non-default presence")

    testCase "ontology parsing retains accession and explicit source fallbacks" (fun _ ->
        Expect.equal (Ontology.tryGetTSR "MS:1003022") (Some "MS") "Compact accession"
        Expect.equal (Ontology.tryGetTSR "http://purl.obolibrary.org/obo/MS_1003022") (Some "MS") "URI accession"
        let fallback = Ontology.computeTanInfo (Some "local") (Some "CUSTOM") |> Option.get
        Expect.equal fallback.IDSpace "CUSTOM" "Explicit source"
        Expect.equal fallback.LocalID "local" "Unchanged local ID"
        Expect.isNone (Ontology.computeTanInfo None (Some "CUSTOM")) "No inferred accession")
]
