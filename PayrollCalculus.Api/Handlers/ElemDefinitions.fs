namespace PayrollCalculus.Api.Handlers

open PayrollCalclulus.Api
open PayrollCalculus.PublishedLanguage
open HandlerUtils
open Giraffe
open NBB.Messaging.Effects


module MessageBus =
    open NBB.Core.Effects.FSharp

    let publish (obj: 'TMessage) =  MessageBus.Publish (obj :> obj) |> Effect.wrap |> Effect.map(fun _ -> ())

module ElemDefinitions =
    

    let interpretCommand handler command = 
        let resultHandler _ = commandResult command
        command |> handler |> interpret resultHandler

    let handler : HttpHandler = 
        subRoute "/elemDefinitions" (
            choose [
                POST >=> route  "/add"  >=> bindJson<Commands.AddElemDefinition> (MessageBus.publish |> interpretCommand)
            ])