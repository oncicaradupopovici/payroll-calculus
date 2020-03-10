namespace DataStructures

open NBB.Core.Effects.FSharp.Data.StateEffect

[<RequireQualifiedAccess>]
module StateEffect =
    let inline modify (f: 's -> 's) : StateEffect<'s, unit> =
       let cache = StateEffect.get()
       cache >>= (fun s -> StateEffect.put (f s))

