namespace PayrollCalclulus.Api

module HandlerUtils =
    open Giraffe
    open NBB.Core.Effects
    open Microsoft.AspNetCore.Http
    open FSharp.Control.Tasks.V2
    open Microsoft.Extensions.DependencyInjection

    let setError errorText = 
        (clearResponse >=> setStatusCode 500 >=> text errorText)

    let interpret<'TResult> (effect: IEffect<HttpHandler>) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let interpreter1 = ctx.RequestServices.GetRequiredService<IInterpreter>()
                let! handler = interpreter1.Interpret(effect)
                return! handler next ctx
            }   

