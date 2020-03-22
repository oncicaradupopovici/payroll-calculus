namespace PayrollCalculus.Infra

open System
open NBB.Core.Effects.FSharp
open NBB.Core.Effects.FSharp.Data.ReaderEffect
open PayrollCalculus.Application
open NBB.Core.FSharp.Data

type DomainEventHandler  = (obj -> Effect<Result<unit, ApplicationError>>)
module DomainEventHandler =
    type HandlerFunc<'TEvent when 'TEvent:> obj> = ('TEvent -> Effect<Result<unit, ApplicationError>>)
    
    type HandlerRegistration = (Type * DomainEventHandler)

    let private wrap<'TEvent when 'TEvent:> obj> (handlerFunc : HandlerFunc<'TEvent>) : DomainEventHandler = 
        fun event ->
            match event with
                | :? 'TEvent as event -> handlerFunc(event) //|> Task.FromResult
                | _ -> failwith "Wrong type"


    let createDomainEventHandler(handlerRegistrations: seq<HandlerRegistration>) : DomainEventHandler =
        let handlersMap = 
            handlerRegistrations 
            |> Seq.groupBy fst 
            |> Seq.map (fun (key, values) -> (key.FullName, values |> Seq.map snd)) 
            |> Map.ofSeq

        fun (event) ->
            let handlerOption = handlersMap |> Map.tryFind (event.GetType().FullName)
            match handlerOption with
            | Some handlers -> 
                event
                |> (handlers |> Seq.toList |> List.sequenceReaderEffect) 
                |> Effect.map (List.sequenceResult >> Result.map (fun _ -> ()))
            | _ -> failwith "Invalid handler"

    let toDomainEventHandlerReg (func: HandlerFunc<'TEvent>) : HandlerRegistration =
        (typeof<'TEvent>,  wrap(func))   