namespace PayrollCalculus.DataStructures

open NBB.Core.FSharp.Data.Reader
open NBB.Core.Effects.FSharp.Data.StateEffect
open NBB.Core.Effects.FSharp.Data.ReaderStateEffect

module Cache =
    module StateEffect =
        let addCaching (key: 'k) (stateEff: StateEffect<Map<'k, 'v>, 'v>) : StateEffect<Map<'k, 'v>, 'v> =
            stateEffect {
                let! cache = StateEffect.get ()
                match (cache.TryFind key) with
                    | Some value -> 
                        return value
                    | None -> 
                        let! value = stateEff
                        do! StateEffect.modify(fun cache -> cache.Add (key, value))
                        return value     
            } 

    module ReaderStateEffect =
        let addCaching (key: 'k) (readerStateEff: ReaderStateEffect<'r, Map<'k, 'v>, 'v>) : ReaderStateEffect<'r, Map<'k, 'v>, 'v> =
            reader {
                let! stateEff = readerStateEff
                return stateEff |> StateEffect.addCaching key 
            }