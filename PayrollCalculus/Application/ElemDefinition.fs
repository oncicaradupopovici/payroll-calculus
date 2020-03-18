namespace PayrollCalculus.Application.ElemDefinition

open NBB.Core.Effects.FSharp
open PayrollCalculus.PublishedLanguage
open NBB.Messaging.Effects
open NBB.Application.DataContracts

// TODO: Find a place for MesageBus wrapper
module MessageBus =
    let publish (obj: 'TMessage) =  MessageBus.Publish (obj :> obj) |> Effect.wrap |> Effect.ignore

module AddElemDefinition =
    let handler (command: AddElemDefinition) =
        effect {
            //do! ElemDefinitionRepo.saveDefinition {ElemCode = command.ElemCode |> ElemCode}

            let event: ElemDefinitionAdded = {ElemCode=command.ElemCode; Metadata = EventMetadata.Default()}
            do! MessageBus.publish event

            return ()
        }
