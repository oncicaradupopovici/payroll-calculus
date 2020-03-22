namespace PayrollCalculus.Application

open NBB.Messaging.Effects
open NBB.Core.Effects.FSharp


type ApplicationError = ApplicationError of string

// TODO: Find a place for MesageBus wrapper
module MessageBus =
    let publish (obj: 'TMessage) =  MessageBus.Publish (obj :> obj) |> Effect.wrap |> Effect.ignore
    
//TODO implement a mediator like solution for events
//module Mediator = 
//    let dispatchEvent (_event:'e) = Effect.pure' ()
//    let dispatchEvents (events: 'e list) = List.traverseEffect dispatchEvent events |> Effect.ignore

module Mediator = 
    open NBB.Core.Effects
    type EventMediator<'e> = {
        dispatchEvent: 'e -> Effect<Result<unit, ApplicationError>>;
        dispatchEvents: 'e list -> Effect<Result<unit, ApplicationError>>
    }
    type GetEventMediatorSideEffect<'e>() = 
        interface ISideEffect<EventMediator<'e>>

    let getEventMediator<'e> = Effect.Of (GetEventMediatorSideEffect<'e> ()) |> Effect.wrap