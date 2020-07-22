namespace PayrollCalculus.Infra

open System
open NBB.Core.Effects
open System.Threading.Tasks

module SideEffectMediator =
    type HandlerFunc<'TSideEffect, 'TOutput when 'TSideEffect:> ISideEffect<'TOutput>> = ('TSideEffect -> 'TOutput)
    type HandlerRegistration = (Type * ISideEffectHandler)
    
    type HandlerWrapper<'TSideEffect, 'TOutput when 'TSideEffect:> ISideEffect<'TOutput>> (handlerFunc : HandlerFunc<'TSideEffect, 'TOutput>) = 
        interface ISideEffectHandler<ISideEffect<'TOutput>,'TOutput> with
            member _.Handle(sideEffect, _cancellationToken) = 
                match sideEffect with
                    | :? 'TSideEffect as sideEffect -> handlerFunc(sideEffect) |> Task.FromResult
                    | _ -> failwith "Wrong type"

    type SideEffectMediatorDecorator (innerMediator: ISideEffectMediator, handlerRegistrations: seq<HandlerRegistration>) =
        let handlersMap = handlerRegistrations |> Seq.map (fun (key, value) -> (key.FullName, value)) |> Map.ofSeq

        interface ISideEffectMediator with
            member _.Run<'TOutput>(sideEffect, cancellationToken) = 
                let handlerOption = handlersMap |> Map.tryFind (sideEffect.GetType().FullName)
                match handlerOption with
                | Some handler ->
                    match handler with 
                    | :?  ISideEffectHandler<ISideEffect<'TOutput>,'TOutput> as handler -> handler.Handle(sideEffect, cancellationToken)
                    | _ -> failwith "Wrong type"
                | _ -> innerMediator.Run(sideEffect, cancellationToken)

    let toHandlerReg (func: HandlerFunc<'TSideEffect, 'TOutput>) : HandlerRegistration =
        (typeof<'TSideEffect>,  HandlerWrapper(func) :> ISideEffectHandler)      

    let makeSideEffectMediatorDecorator innerMediator handlerRegistrations = 
        SideEffectMediatorDecorator(innerMediator, handlerRegistrations) :> ISideEffectMediator

    //let createInterpreter sideEffectMediator = Interpreter(sideEffectMediator)
    //let show interpreter eff = eff |> Effect.interpret interpreter |> Async.RunSynchronously |> printfn "%A"
        
    //module HandlerBuilder =
    //    let empty : HandlerRegistration list = []
    //    let add (func: HandlerFunc<'TSideEffect, 'TOutput>) (handlerRegistrations: HandlerRegistration list) =
    //        (typeof<'TSideEffect>,  HandlerWrapper(func) :> ISideEffectHandler) :: handlerRegistrations


