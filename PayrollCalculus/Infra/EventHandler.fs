namespace PayrollCalculus.Infra

open System
open NBB.Core.Abstractions
open NBB.Core.Effects.FSharp
open NBB.Core.Effects.FSharp.Data.ReaderEffect
open PayrollCalculus.Application
open NBB.Core.FSharp.Data

type EventHandler  = (IEvent -> Effect<Result<unit, ApplicationError>>)
module EventHandler =
    type HandlerFunc<'TEvent when 'TEvent:> IEvent> = ('TEvent -> Effect<Result<unit, ApplicationError>>)
    
    type HandlerRegistration = (Type * EventHandler)

    let private wrap<'TEvent when 'TEvent:> IEvent> (handlerFunc : HandlerFunc<'TEvent>) : EventHandler = 
        fun event ->
            match event with
                | :? 'TEvent as event -> handlerFunc(event) //|> Task.FromResult
                | _ -> failwith "Wrong type"


    let createEventHandler(handlerRegistrations: seq<HandlerRegistration>) : EventHandler =
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

    let toEventHandlerReg (func: HandlerFunc<'TEvent>) : HandlerRegistration =
        (typeof<'TEvent>,  wrap(func))   