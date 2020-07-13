namespace Mediator1

open NBB.Core.Effects.FSharp

type RequestHandler<'i, 'o> = 'i -> Effect<'o>
type PipelineHandlerFunc<'i, 'o> = 'i -> Effect<'o option>
type PipelineHandler<'i, 'o> = PipelineHandlerFunc<'i, 'o> -> PipelineHandlerFunc<'i, 'o>

type PipelineHandler1<'i, 'a, 'o> = PipelineHandlerFunc<'i, 'a> -> PipelineHandlerFunc<'a, 'o>


[<AutoOpen>]
module RequestPipeline =
    let skipPipeline<'o> : Effect<'o option> = effect { return None }
    let returnEarly value : PipelineHandlerFunc<'i, 'o> = fun _ -> effect {return Some value}
    let empty : PipelineHandlerFunc<'i, 'o> = fun _ -> skipPipeline

    let liftOptionEffect (eff : Effect<'o>) : Effect<'o option> = 
        effect { 
            let! o = eff
            return Some o 
        }

    let preProcess<'i, 'o> (action: 'i -> Effect<'i>) (handler: PipelineHandlerFunc<'i, 'o>) : PipelineHandlerFunc<'i, 'o> =
        fun request ->
            effect {
                let! request = action request
                return! handler request
            }

    let postProcess<'i, 'o> (action: 'o -> Effect<'o>) (handler: PipelineHandlerFunc<'i, 'o>) : PipelineHandlerFunc<'i, 'o> =
        fun request ->
            effect {
                let! response = handler request
                match response with 
                    | Some resp -> 
                        let! response' = action resp
                        return Some response'
                    | None -> return None
            }

    let route<'t, 'i, 'o> : PipelineHandler<'i, 'o> = 
        let t = typeof<'t>
        fun (next) (req) ->
            match req.GetType() with
                | t -> next req
                | _ -> skipPipeline
            

    let rec private chooseHandlerFunc (handlers : PipelineHandlerFunc<'i, 'o> list) : PipelineHandlerFunc<'i, 'o> =
        fun (req : 'i) ->
            effect {
                match handlers with
                | [] -> return None
                | handler :: tail ->
                    let! result = handler req
                    match result with
                    | Some c -> return Some c
                    | None   -> return! chooseHandlerFunc tail req
            }

    let choose (handlers : PipelineHandler<'i, 'o> list) : PipelineHandler<'i, 'o>=
        fun (next : PipelineHandlerFunc<'i, 'o>) ->
            let funcs = handlers |> List.map (fun h -> h next)
            fun (req : 'i) ->
                chooseHandlerFunc funcs req

    let create1 (handlerFunc : RequestHandler<'t, 'o>) : PipelineHandler<'i, 'o>= 
        fun _next  req ->
            match box req with
                | :? 't as req' -> handlerFunc req' |> liftOptionEffect
                | _ -> skipPipeline


    //let compose (handler1 : PipelineHandler<'i, 'o>) (handler2 : PipelineHandler<'i, 'o>) : PipelineHandler<'i, 'o> =
    //    fun (final : PipelineHandlerFunc<'i, 'o>) ->
    //       final |> handler2 |> handler1           
                
    let dispatch (pipeline: PipelineHandler<'i, 'o>) : RequestHandler<'i, 'o> =
        fun req ->
            effect {
                let! res = pipeline empty req
                return 
                    match res with
                    | Some x -> x
                    | None -> failwith("No handler found")
            }

    //let (>>=>>) = compose