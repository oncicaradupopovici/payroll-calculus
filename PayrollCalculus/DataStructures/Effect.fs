namespace DataStructures

open NBB.Core.Effects
open NBB.Core.Effects.FSharp

module Effect =
    module Result = 
        let traverseEffect (f: 'a-> IEffect<'c>) (result:Result<'a,'b>) : IEffect<Result<'c, 'b>> = 
            match result with
                |Error err -> Effect.map Result.Error (Effect.pure' err)
                |Ok v -> Effect.map Result.Ok (f v)

        let sequenceEffect result = traverseEffect id result
