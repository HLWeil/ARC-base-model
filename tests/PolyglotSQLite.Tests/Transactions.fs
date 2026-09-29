module PolyglotSQLite.Tests.Transactions

open PolyglotSQLite
open Fable.Pyxpecto
open PolyglotSQLite.Tests.Drivers

let private setup action =
    withDatabase <| fun db ->
        db.Execute("CREATE TABLE items (id INTEGER PRIMARY KEY, label TEXT)")
        action db

let tests = testList "transactions" [
    testCase "R01 mixed parameterized and parameterless writes roll back together" <| fun _ ->
        setup <| fun db ->
            let scope = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (1, 'first')")
            db.Execute("INSERT INTO items VALUES ($id, $label)", [SqlParameter("id", SqlValue.Integer(2L)); SqlParameter("label", SqlValue.Text("second"))])
            Expect.equal (integer db "SELECT count(*) FROM items") 2L "Both writes visible inside transaction"
            scope.Rollback()
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "No implicit commit"

    testCase "explicit commit and unfinished scope disposal" <| fun _ ->
        setup <| fun db ->
            let committed = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (1, 'committed')")
            committed.Commit()
            committed.Close()
            let abandoned = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (2, 'abandoned')")
            abandoned.Close()
            abandoned.Close()
            Expect.equal (integer db "SELECT count(*) FROM items") 1L "Only completed scope persists"

    testCase "callback result returned and callback failure rolls back" <| fun _ ->
        setup <| fun db ->
            let result = db.WithTransaction(fun () -> db.Execute("INSERT INTO items VALUES (1, 'ok')"); "result")
            Expect.equal result "result" "Callback value returned unchanged"
            let mutable message = ""
            try
                db.WithTransaction(fun () -> db.Execute("INSERT INTO items VALUES (2, 'bad')"); failwith "original callback failure") |> ignore
            with error -> message <- error.Message
            Expect.equal message "original callback failure" "Original exception preserved"
            Expect.equal (integer db "SELECT count(*) FROM items") 1L "Callback write rolled back"

    testCase "inner commit remains subject to outer rollback" <| fun _ ->
        setup <| fun db ->
            let outer = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (1, 'outer')")
            let inner = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (2, 'inner')")
            inner.Commit()
            outer.Rollback()
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "Released savepoint is not durable commit"

    testCase "inner rollback leaves outer transaction usable" <| fun _ ->
        setup <| fun db ->
            db.WithTransaction(fun () ->
                db.Execute("INSERT INTO items VALUES (1, 'outer')")
                Expect.throws
                    (fun () -> db.WithTransaction(fun () -> db.Execute("INSERT INTO items VALUES (2, 'inner')"); failwith "inner failure"))
                    "Inner callback failure"
                db.Execute("INSERT INTO items VALUES (3, 'after inner')"))
            Expect.equal (integer db "SELECT count(*) FROM items") 2L "Only inner work rolled back"
            Expect.equal (integer db "SELECT count(*) FROM items WHERE id=2") 0L "Failed inner write absent"

    testCase "transaction scopes enforce stack order" <| fun _ ->
        setup <| fun db ->
            let outer = db.BeginTransaction()
            let inner = db.BeginTransaction()
            Expect.throws (fun () -> outer.Commit()) "Cannot commit below an active nested scope"
            Expect.throws (fun () -> outer.Rollback()) "Cannot roll back below an active nested scope"
            inner.Rollback()
            outer.Commit()
            Expect.throws (fun () -> outer.Commit()) "Completed transaction cannot be reused"

    testCase "callback leaving an unfinished inner scope rolls back all callback work" <| fun _ ->
        setup <| fun db ->
            Expect.throws
                (fun () -> db.WithTransaction(fun () ->
                    db.Execute("INSERT INTO items VALUES (1, 'outer')")
                    db.BeginTransaction() |> ignore
                    db.Execute("INSERT INTO items VALUES (2, 'unfinished inner')")))
                "Callback cannot implicitly commit an unfinished inner scope"
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "Both scopes cleaned up"
            db.WithTransaction(fun () -> db.Execute("INSERT INTO items VALUES (3, 'fresh')"))
            Expect.equal (integer db "SELECT count(*) FROM items") 1L "Connection stack usable after cleanup"

    testCase "scripts rejected inside active scope without disturbing it" <| fun _ ->
        setup <| fun db ->
            let scope = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (1, 'pending')")
            Expect.throws (fun () -> db.ExecuteScript("INSERT INTO items VALUES (2, 'script');")) "Script cannot precommit scope"
            scope.Rollback()
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "Original scope remained rollbackable"

    testCase "deferred foreign-key commit failure rolls back callback work" <| fun _ ->
        withDatabase <| fun db ->
            db.Execute("CREATE TABLE parent (id INTEGER PRIMARY KEY)")
            db.Execute("CREATE TABLE child (parent_id REFERENCES parent(id) DEFERRABLE INITIALLY DEFERRED)")
            Expect.throws
                (fun () -> db.WithTransaction(fun () -> db.Execute("INSERT INTO child VALUES (7)")))
                "Deferred constraint fails on commit"
            Expect.equal (integer db "SELECT count(*) FROM child") 0L "Commit failure rolled back"
            db.WithTransaction(fun () -> db.Execute("INSERT INTO parent VALUES (7)"); db.Execute("INSERT INTO child VALUES (7)"))
            Expect.equal (integer db "SELECT count(*) FROM child") 1L "New transaction usable after failed commit"

    testCase "backend rollback invalidates stale scopes and permits fresh work" <| fun _ ->
        setup <| fun db ->
            let stale = db.BeginTransaction()
            db.Execute("INSERT INTO items VALUES (1, 'first')")
            Expect.throws (fun () -> db.Execute("INSERT OR ROLLBACK INTO items VALUES (1, 'duplicate')")) "SQLite rolls back entire transaction"
            Expect.throws (fun () -> stale.Commit()) "Cannot commit stale transaction"
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "Backend rolled back original insertion"
            db.WithTransaction(fun () -> db.Execute("INSERT INTO items VALUES (2, 'fresh')"))
            Expect.equal (integer db "SELECT count(*) FROM items") 1L "Stack state recovered"

    testCase "caught backend rollback cannot turn remaining callback work into autocommit writes" <| fun _ ->
        setup <| fun db ->
            let mutable writeRejected = false
            let mutable beginRejected = false
            Expect.throws
                (fun () -> db.WithTransaction(fun () ->
                    db.Execute("INSERT INTO items VALUES (1, 'first')")
                    try db.Execute("INSERT OR ROLLBACK INTO items VALUES (1, 'duplicate')")
                    with _ -> ()
                    try db.Execute("INSERT INTO items VALUES (2, 'must not autocommit')")
                    with _ -> writeRejected <- true
                    try db.BeginTransaction() |> ignore
                    with _ -> beginRejected <- true))
                "Callback whose transaction was lost cannot complete"
            Expect.isTrue writeRejected "Further work inside failed callback must be rejected"
            Expect.isTrue beginRejected "Cannot replace the failed callback's transaction"
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "No implicit autocommit write escaped"
            db.WithTransaction(fun () -> db.Execute("INSERT INTO items VALUES (3, 'fresh callback')"))
            Expect.equal (integer db "SELECT count(*) FROM items") 1L "New callback after failed frame exits is allowed"

    testCase "closing connection rolls back unfinished nested work" <| fun _ ->
        let db = Sqlite.OpenInMemory()
        db.Execute("CREATE TABLE close_scope (value)")
        let outer = db.BeginTransaction()
        let inner = db.BeginTransaction()
        db.Execute("INSERT INTO close_scope VALUES (1)")
        db.Close()
        inner.Close()
        outer.Close()
        Expect.throws (fun () -> inner.Commit()) "Closed nested scope cannot commit"

#if !FABLE_COMPILER
    testCase "known Task callback is rejected before invoking its body" <| fun _ ->
        setup <| fun db ->
            let mutable invoked = false
            Expect.throws
                (fun () -> db.WithTransaction(fun () ->
                    invoked <- true
                    db.Execute("INSERT INTO items VALUES (1, 'must not run')")
                    System.Threading.Tasks.Task.FromResult(1)) |> ignore)
                "Task-returning callbacks are not synchronous"
            Expect.isFalse invoked "Declared Task callback must not be invoked"
            Expect.equal (integer db "SELECT count(*) FROM items") 0L "No write from rejected callback"
#endif
]
