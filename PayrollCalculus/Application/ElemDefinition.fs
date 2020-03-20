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
            let result = 
                ElemDefinitionStore.addDbElem 
                    (command.ElemCode|> ElemCode) 
                    {TableName = command.Table; ColumnName = command.Column} 
                    (command.DataType |> Type.GetType) 
                    store
            match result with
            |Error (DomainError err) -> return Some (ApplicationError err)
            |Ok (Evented(store', events)) ->
                do! ElemDefinitionStoreRepo.save (store', events)
                do! Mediator.dispatchEvents events

                let event: ElemDefinitionAdded = {ElemCode=command.ElemCode; Metadata = EventMetadata.Default()}
                do! MessageBus.publish event

                return None
        }

    
