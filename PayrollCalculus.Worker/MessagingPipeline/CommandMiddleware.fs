﻿namespace PayrollCalculus.Worker.MessagingPipeline

open NBB.Messaging.DataContracts
open PayrollCalculus.Infra.CommandHandler
open NBB.Core.Pipeline
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open NBB.Core.Effects
open NBB.Core.Effects.FSharp
open System
open NBB.Core.Abstractions
open System.Threading

type CommandMiddleware(interpreter: IInterpreter, commandhandler: CommandHandler) = 
    interface IPipelineMiddleware<MessagingEnvelope> with
        member _.Invoke (message: MessagingEnvelope, cancellationToken: CancellationToken, next: Func<Task>) : Task =
            task {
                let effect = 
                    match message.Payload with
                        | :? ICommand as command -> commandhandler command 
                        | _ -> failwith "Invalid message"

                do! interpreter.Interpret (effect |> Effect.unWrap)
                do! next.Invoke()
            } :> Task