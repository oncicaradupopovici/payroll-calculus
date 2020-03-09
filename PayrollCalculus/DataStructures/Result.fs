namespace DataStructures

open FSharpPlus

[<RequireQualifiedAccess>]
module Result =
    let inline traverse (f: 'T->'``Applicative<'U>``) (res:Result<'T, 'E>) : '``Applicative<Result<'U, 'E>>`` = 
        match res with
            |Error err -> result (Error err)
            |Ok v -> map Result.Ok (f v)

    let inline sequence (res: Result<'``Applicative<'T>``, 'E>) : '``Applicative<list<'T>, 'E>`` = 
        traverse id res
