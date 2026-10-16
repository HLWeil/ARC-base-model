module internal ARCManagement.Helper.Identifier

open Fable.Core

#if FABLE_COMPILER_PYTHON
[<Emit("__import__('uuid').uuid4().hex")>]
let newId () : string = nativeOnly
#else
let newId () = System.Guid.NewGuid().ToString("N")
#endif
