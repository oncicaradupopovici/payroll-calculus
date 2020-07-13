namespace Mediator1
open NBB.Core.Effects.FSharp

type NotificationHandler<'i> = 'i -> Effect<unit>

[<AutoOpen>]
module NotificationHandler =
    let skip : Effect<unit> = effect { return () }

    let liftOptionEffect (eff : Effect<'o>) : Effect<'o option> = 
        effect { 
            let! o = eff
            return Some o 
        }

    let choose (handlers : NotificationHandler<'i> list) : NotificationHandler<'i>=
        fun notif ->
            //handlers |> List.map (fun h -> h notif) |> List.sequenceEffect |> Effect.map(fun _ -> ())
            effect {
                let effects = handlers |> List.map (fun h -> h notif)
                let! _units = (List.sequenceEffect effects)
                return ()
            }

    let create (handlerFunc : NotificationHandler<'t>) : NotificationHandler<'i> = 
        fun notif ->
            match box notif with
                | :? 't as notif' -> handlerFunc notif'
                | _ -> skip
               
    let dispatch (pipeline: NotificationHandler<'i>) : NotificationHandler<'i> =
        pipeline
    