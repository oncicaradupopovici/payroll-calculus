namespace PayrollCalclulus.Api

module HandlerUtils =
    open Giraffe
    open NBB.Core.Effects
    open Microsoft.AspNetCore.Http
    open FSharp.Control.Tasks.V2
    open Microsoft.Extensions.DependencyInjection

    type Effect<'a> = NBB.Core.Effects.FSharp.Effect<'a>
    module Effect = NBB.Core.Effects.FSharp.Effect
    

    let setError errorText = 
        (clearResponse >=> setStatusCode 500 >=> text errorText)

    let interpret<'TResult> (effect: Effect<HttpHandler>) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let interpreter = ctx.RequestServices.GetRequiredService<IInterpreter>()
                let! handler = interpreter.Interpret(effect |> Effect.unWrap)
                return! handler next ctx
            }   

