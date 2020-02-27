namespace PayrollCalculus

open System
open NBB.Core.Effects
open NBB.Core.Effects.FSharp
open System.Threading.Tasks

module Infra =
    type HandlerFunc<'TSideEffect, 'TOutput when 'TSideEffect:> ISideEffect<'TOutput>> = ('TSideEffect -> 'TOutput)
    type HandlerRegistration = (Type * ISideEffectHandler)
    
    type HandlerWrapper<'TSideEffect, 'TOutput when 'TSideEffect:> ISideEffect<'TOutput>> (handlerFunc : HandlerFunc<'TSideEffect, 'TOutput>) = 
        interface ISideEffectHandler<ISideEffect<'TOutput>,'TOutput> with
            member _.Handle(sideEffect, _cancellationToken) = 
                match sideEffect with
                    | :? 'TSideEffect as sideEffect -> handlerFunc(sideEffect) |> Task.FromResult
                    | _ -> failwith "Wrong type"


    type SideEffectHandlerFactory(handlerRegistrations: seq<HandlerRegistration>) =
        let handlersMap = handlerRegistrations |> Seq.map (fun (key, value) -> (key.FullName, value)) |> Map.ofSeq

        interface ISideEffectHandlerFactory with
            member _.GetSideEffectHandlerFor<'TOutput>(sideEffect) = 
                let handlerOption = handlersMap |> Map.tryFind (sideEffect.GetType().FullName)
                match handlerOption with
                | Some handler ->
                    match handler with 
                    | :?  ISideEffectHandler<ISideEffect<'TOutput>,'TOutput> as handler -> handler
                    | _ -> failwith "Wrong type"
                | _ -> failwith "Invalid handler"

    let toHandlerReg (func: HandlerFunc<'TSideEffect, 'TOutput>) : HandlerRegistration =
        (typeof<'TSideEffect>,  HandlerWrapper(func) :> ISideEffectHandler)      

    let interpreter = Interpreter << SideEffectHandlerFactory : (seq<HandlerRegistration> -> Interpreter)

    let show interpreter eff = eff |> Effect.interpret interpreter |> Async.RunSynchronously |> printfn "%A"




    //module HandlerBuilder =
    //    let empty : HandlerRegistration list = []
    //    let add (func: HandlerFunc<'TSideEffect, 'TOutput>) (handlerRegistrations: HandlerRegistration list) =
    //        (typeof<'TSideEffect>,  HandlerWrapper(func) :> ISideEffectHandler) :: handlerRegistrations


