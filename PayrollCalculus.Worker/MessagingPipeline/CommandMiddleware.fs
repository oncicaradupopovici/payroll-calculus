namespace PayrollCalculus.Worker.MessagingPipeline

open NBB.Messaging.DataContracts
open NBB.Core.Pipeline
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open NBB.Core.Effects
open NBB.Core.Effects.FSharp
open System
open System.Threading


type CommandMiddleware(interpreter: IInterpreter, messageHandler: Dispatcher.MessageHandler) = 
    interface IPipelineMiddleware<MessagingEnvelope> with
        member _.Invoke (message: MessagingEnvelope, cancellationToken: CancellationToken, next: Func<Task>) : Task =
            task {
                let effect = messageHandler message.Payload

                do! interpreter.Interpret (effect |> Effect.unWrap)
                do! next.Invoke()
            } :> Task