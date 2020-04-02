namespace PayrollCalculus.Application

open NBB.Core.Effects.FSharp
open PayrollCalculus.PublishedLanguage
open NBB.Application.DataContracts
open PayrollCalculus.Domain
open NBB.Core.Evented.FSharp
open System

module AddDbElemDefinition =
    let handler (command: AddDbElemDefinition) =
        effect {
            let! store = ElemDefinitionStoreRepo.loadCurrent
            let! Evented(store', events) = 
                ElemDefinitionStore.addDbElem 
                    (command.ElemCode|> ElemCode) 
                    {TableName = command.Table; ColumnName = command.Column} 
                    (command.DataType |> Type.GetType) 
                    store
            do! ElemDefinitionStoreRepo.save (store', events)
            do! Mediator.dispatchEvents events

            let event: ElemDefinitionAdded = {ElemCode=command.ElemCode; Metadata = EventMetadata.Default()}
            do! MessageBus.publish event
        }

    
