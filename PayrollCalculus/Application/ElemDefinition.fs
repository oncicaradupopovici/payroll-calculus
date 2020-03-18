namespace PayrollCalculus.Application.ElemDefinition

open NBB.Core.Effects.FSharp
open PayrollCalculus.PublishedLanguage.Commands
open PayrollCalculus.PublishedLanguage.Events
open NBB.Messaging.Effects
open NBB.Application.DataContracts

module AddElemDefinition =
    let private publish (obj: 'TMessage) =  MessageBus.Publish (obj :> obj) |> Effect.wrap |> Effect.map(fun _ -> ())

    let handler ({ElemCode=elemCode}: AddElemDefinition) =
        effect {
            //do! elemDefinitionCache = ElemDefinitionRepo.saveDefinition ()
            let event: ElemDefinitionAdded = {ElemCode=elemCode; Metadata = EventMetadata.Default()}
            do! publish event

            return ()
        }
