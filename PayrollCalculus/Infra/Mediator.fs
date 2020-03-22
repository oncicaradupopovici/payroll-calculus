namespace PayrollCalculus.Infra

open PayrollCalculus.Application.Mediator
open NBB.Core.Effects.FSharp
open PayrollCalculus.Application
open NBB.Core.FSharp.Data

module Mediator =
    let getEventMediator (eventHandler: obj -> Effect<Result<unit, ApplicationError>>) (_: GetEventMediatorSideEffect<'e>) : EventMediator<'e>  =
        let dispatch = fun e -> eventHandler (e :> obj)

        { dispatchEvent = dispatch;
          dispatchEvents = List.traverseEffect dispatch  >> Effect.map (List.sequenceResult >> Result.map (fun _ -> ())) }