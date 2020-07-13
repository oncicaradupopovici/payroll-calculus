namespace Dispatcher

open Mediator1

open NBB.Core.Abstractions
open NBB.Core.Effects.FSharp

type CommandHandler = RequestHandler<NBB.Core.Abstractions.ICommand, unit>
type CommandHandler<'T> = RequestHandler<'T :> NBB.Core.Abstractions.ICommand, unit>
type CommandPipelineHandler = PipelineHandler<NBB.Core.Abstractions.ICommand, unit>

type EventHandler = RequestHandler<NBB.Core.Abstractions.IEvent, unit>
type EventHandler<'T> = RequestHandler<'T :> NBB.Core.Abstractions.IEvent, unit>
type EventPipelineHandler = PipelineHandler<NBB.Core.Abstractions.IEvent, unit>

type MessagePipeline = PipelineHandler<obj, unit>
type MessageHandler = RequestHandler<obj, unit>

module ApplicationDispatcher =
    let create (commandhandler: CommandPipelineHandler) (eventhandler: EventPipelineHandler) : MessagePipeline =
        fun next message ->
            match box message with
                | :? ICommand as command -> commandhandler next command 
                | :? IEvent as event -> eventhandler next event
                | _ -> failwith "Invalid message"

    let dispatch (pipeline: MessagePipeline) : MessageHandler =
        fun message ->
            effect {
                let! res = pipeline empty message
                return 
                    match res with
                    | Some x -> x
                    | None -> failwith("No handler found for message")
            }

    
