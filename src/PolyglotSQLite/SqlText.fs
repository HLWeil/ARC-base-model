namespace PolyglotSQLite

open System

/// A boundary lexer, not a SQL grammar or query rewriter. Providers still validate SQL syntax.
module internal SqlText =
    let private asciiLetter value =
        (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || value = '_'

    let private digit value = value >= '0' && value <= '9'

    // SQLite identifiers may contain non-ASCII characters and '$'. Parameter names deliberately cannot.
    let private identifierStart value = asciiLetter value || int value >= 128
    let private identifierPart value = identifierStart value || digit value || value = '$'

    /// Validate one actual statement before preparing anything, including PRAGMAs with prepare-time effects.
    let validateSingle (sql: string) =
        if isNull sql then nullArg "sql"
        if sql.IndexOf('\000') >= 0 then invalidArg "sql" "SQL text must not contain NUL characters."
        let fail message = invalidArg "sql" message
        let mutable index = 0
        let mutable hasStatement = false
        let mutable completed = false
        let mutable statementStart = 0
        let mutable statementEnd = sql.Length
        // Header states recognize CREATE [TEMP|TEMPORARY] TRIGGER, optionally after EXPLAIN [QUERY PLAN].
        let mutable header = 0
        let mutable trigger = false
        let mutable afterTriggerSemicolon = false
        let mutable afterTriggerEnd = false

        let token start (word: string option) =
            if completed then fail "Expected exactly one SQLite statement; use ExecuteScript for scripts."
            let upper = word |> Option.map (fun value -> value.ToUpperInvariant())
            if not hasStatement then
                match upper with
                | Some "BEGIN" | Some "COMMIT" | Some "END" | Some "ROLLBACK" | Some "SAVEPOINT" | Some "RELEASE" ->
                    fail "Use BeginTransaction or WithTransaction instead of raw transaction-control statements."
                | _ -> ()
                hasStatement <- true
                statementStart <- start

            if trigger then
                // A trigger terminates at '; END', not at an END inside an expression. This mirrors
                // SQLite's completion rule and avoids interpreting identifiers named CASE or END as nesting.
                afterTriggerEnd <- afterTriggerSemicolon && upper = Some "END"
                afterTriggerSemicolon <- false
            else
                match header, upper with
                | 0, Some "CREATE" -> header <- 1
                | 0, Some "EXPLAIN" -> header <- 10
                | 10, Some "CREATE" -> header <- 1
                | 10, Some "QUERY" -> header <- 11
                | 11, Some "PLAN" -> header <- 12
                | 12, Some "CREATE" -> header <- 1
                | 1, Some "TEMP" | 1, Some "TEMPORARY" -> header <- 2
                | 1, Some "TRIGGER" | 2, Some "TRIGGER" -> trigger <- true
                | _ -> header <- -1

        let quoted start delimiter doubledEscapes =
            let mutable cursor = start + 1
            let mutable closed = false
            while cursor < sql.Length && not closed do
                if sql[cursor] = delimiter then
                    if doubledEscapes && cursor + 1 < sql.Length && sql[cursor + 1] = delimiter then
                        cursor <- cursor + 2
                    else
                        cursor <- cursor + 1
                        closed <- true
                else cursor <- cursor + 1
            if not closed then fail "Unterminated SQL string or quoted identifier."
            cursor

        while index < sql.Length do
            let current = sql[index]
            if current = ' ' || current = '\t' || current = '\r' || current = '\n' || current = '\011' || current = '\012' || current = '\uFEFF' then
                index <- index + 1
            elif current = '-' && index + 1 < sql.Length && sql[index + 1] = '-' then
                index <- index + 2
                while index < sql.Length && sql[index] <> '\n' do index <- index + 1
            elif current = '/' && index + 1 < sql.Length && sql[index + 1] = '*' then
                index <- index + 2
                let mutable closed = false
                while index < sql.Length && not closed do
                    if sql[index] = '*' && index + 1 < sql.Length && sql[index + 1] = '/' then
                        index <- index + 2
                        closed <- true
                    else index <- index + 1
                // SQLite treats an unterminated block comment as extending to the end of input.
            elif current = '\'' || current = '"' || current = '`' then
                let start = index
                index <- quoted index current true
                token start None
            elif current = '[' then
                let start = index
                index <- quoted index ']' false
                token start None
            elif current = ';' then
                index <- index + 1
                if hasStatement && not completed then
                    if trigger && not afterTriggerEnd then afterTriggerSemicolon <- true
                    else
                        completed <- true
                        statementEnd <- index
            elif current = '@' || current = ':' || current = '?' then
                fail "Only canonical $name parameters are supported; positional, @name, and :name bindings are unavailable."
            elif current = '$' then
                let start = index
                index <- index + 1
                if index >= sql.Length || not (asciiLetter sql[index]) then
                    fail "Parameter names must match $[A-Za-z_][A-Za-z0-9_]*."
                while index < sql.Length && (asciiLetter sql[index] || digit sql[index]) do index <- index + 1
                if index < sql.Length && (identifierPart sql[index] || sql[index] = ':' || sql[index] = '(') then
                    fail "Parameter names must match $[A-Za-z_][A-Za-z0-9_]*; SQLite extended variable names are unsupported."
                token start None
            elif identifierStart current then
                let start = index
                index <- index + 1
                while index < sql.Length && identifierPart sql[index] do index <- index + 1
                token start (Some (sql.Substring(start, index - start)))
            else
                let start = index
                index <- index + 1
                token start None

        if not hasStatement then fail "Expected one nonempty SQLite statement."
        // Python sqlite3 rejects otherwise harmless extra empty statements. Execute only the validated
        // statement slice, after validating the entire original input so later statements cannot run.
        sql.Substring(statementStart, statementEnd - statementStart)
