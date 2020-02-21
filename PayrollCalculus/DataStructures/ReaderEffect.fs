namespace DataStructures

open NBB.Core.Effects.FSharp
open NBB.Core.Effects

type ReaderEffect<'s, 't> = 's -> IEffect<'t>
module ReaderEffect =
    let run (x: ReaderEffect<'s, 't>) : 's -> IEffect<'t> = 
        x 
    let map (f: 't->'u) (m : ReaderEffect<'s, 't>) : ReaderEffect<'s,'u> = 
        m >> Effect.map f
    let bind (f: 't-> ReaderEffect<'s, 'u>) (m : ReaderEffect<'s, 't>) : ReaderEffect<'s, 'u> = 
        fun s -> Effect.bind (fun a -> run (f a) s) (run m s)
    let apply (f: ReaderEffect<'s, ('t -> 'u)>) (m: ReaderEffect<'s, 't>) : ReaderEffect<'s, 'u> = 
        fun s -> Effect.bind (fun g -> Effect.map (fun (a: 't) -> (g a)) (run m s)) (f s)
    let pure' x = fun _ -> Effect.pure' x
    let lift (eff : IEffect<'t>) : ReaderEffect<'s, 't> =
        fun _ -> eff
    let hoist (reader : Reader<'s, 't>) : ReaderEffect<'s, 't> =
        fun s -> Effect.pure' (reader s)

module ReaderEffectBulder =
    type ReaderEffectBulder() =
        member _.Bind (m, f) = ReaderEffect.bind f m                    : ReaderEffect<'s,'u>
        member _.Return x = ReaderEffect.pure' x                        : ReaderEffect<'s,'u>
        member _.ReturnFrom x = x                                       : ReaderEffect<'s,'u>
        member _.Combine (m1, m2) = ReaderEffect.bind (fun _ -> m1) m2  : ReaderEffect<'s,'u>
        member _.Zero () = ReaderEffect.pure' ()                        : ReaderEffect<'s, unit>

    let stessss = new ReaderEffectBulder()

[<AutoOpen>]
module ReaderEffects =
    let rde = new ReaderEffectBulder.ReaderEffectBulder()

    let (<!>) = ReaderEffect.map
    let (<*>) = ReaderEffect.apply
    let (>>=) eff func = ReaderEffect.bind func eff