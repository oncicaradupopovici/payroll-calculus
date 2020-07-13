namespace Mediator

open NBB.Core.Effects.FSharp

type RequestHandler<'i, 'o> = 'i -> Effect<'o>
//type Middeware<'i, 'o> = RequestHandler<'i, 'o> -> RequestHandler<'i, 'o>

module RequestHandler =
    let preProcess<'i, 'o> (action: 'i -> Effect<'i>) (handler: RequestHandler<'i, 'o>) : RequestHandler<'i, 'o> =
        fun request ->
            effect {
                let! request = action request
                return! handler request
            }

    let postProcess<'i, 'o> (action: 'o -> Effect<'o>) (handler: RequestHandler<'i, 'o>) : RequestHandler<'i, 'o> =
        fun request ->
            effect {
                let! response = handler request
                return! action response
            }

    let both handler = 
        handler |> preProcess (fun x -> effect { return x }) |> postProcess (fun y -> effect { return y })
