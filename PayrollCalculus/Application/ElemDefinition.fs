﻿namespace PayrollCalculus.Application.ElemDefinition

open NBB.Core.Effects.FSharp
open PayrollCalculus.PublishedLanguage
open NBB.Messaging.Effects
open NBB.Application.DataContracts
open PayrollCalculus.Domain
open NBB.Core.Evented.FSharp

// TODO: Find a place for MesageBus wrapper
module MessageBus =
    let publish (obj: 'TMessage) =  MessageBus.Publish (obj :> obj) |> Effect.wrap |> Effect.ignore
    
//TODO implement a mediator like solution for events
module Mediator = 
    let dispatchEvent<'e> (_event:'e) = Effect.pure' ()
    let dispatchEvents<'e> (events: 'e list) = List.traverseEffect dispatchEvent events |> Effect.ignore

module AddDbElemDefinition =
    let handler (command: AddDbElemDefinition) =
        effect {
            let! store = ElemDefinitionStoreRepo.loadCurrent
            let (store', events) = 
                ElemDefinitionStore.addDbElem (command.ElemCode|> ElemCode) {Table = command.Table; Column = command.Column} command.DataType store
                |> Evented.run

            do! ElemDefinitionStoreRepo.save store'
            do! Mediator.dispatchEvents events

            let event: ElemDefinitionAdded = {ElemCode=command.ElemCode; Metadata = EventMetadata.Default()}
            do! MessageBus.publish event

            return ()
        }

    
