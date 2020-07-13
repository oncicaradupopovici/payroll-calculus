// Learn more about F# at http://fsharp.org

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open NBB.Messaging.Effects
open NBB.Messaging.Host
open NBB.Messaging.Nats
open NBB.Messaging.Host.MessagingPipeline;
open NBB.Core.Effects
open NBB.Resiliency
open PayrollCalculus
open PayrollCalculus.Worker.MessagingPipeline
open PayrollCalculus.PublishedLanguage
open PayrollCalculus.Infra
open Interpreter
open Mediator1.RequestPipeline
open Dispatcher.ApplicationDispatcher
open PayrollCalculus.Infra.DataAccess
open NBB.Core.Effects.FSharp

//type CommandHandler = Mediator.RequestHandler<NBB.Core.Abstractions.ICommand, unit>
//type CommandHandler<'T> = Mediator.RequestHandler<'T :> NBB.Core.Abstractions.ICommand, unit>


type CommandHandler = Mediator1.RequestHandler<NBB.Core.Abstractions.ICommand, unit>
type CommandHandler<'T> = Mediator1.RequestHandler<'T :> NBB.Core.Abstractions.ICommand, unit>
type CommandPipelineHandler = Mediator1.PipelineHandler<NBB.Core.Abstractions.ICommand, unit>

[<EntryPoint>]
let main argv =

    // App configuration
    let appConfig (context : HostBuilderContext) (configApp : IConfigurationBuilder) =
        configApp
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional = true)
            .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, optional = true)
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .AddEnvironmentVariables()
            .AddCommandLine(argv)
            |> ignore

    let middleware : CommandPipelineHandler = 
        fun next req -> effect { return! (next req) } 

    let commandPipeline = middleware >> choose [
        create1 Application.AddDbElemDefinition.handler
    ]

    let eventPipeline = (fun _ -> empty)

    let messagePipeline = create commandPipeline eventPipeline

    // Services configuration
    let serviceConfig (context : HostBuilderContext) (services : IServiceCollection) =
    
        let payrollConnString = context.Configuration.GetConnectionString "PayrollCalculus"

        services.AddScoped<Dispatcher.MessageHandler>(Func<IServiceProvider, Dispatcher.MessageHandler>(fun _sp -> 
            dispatch messagePipeline
        )) |> ignore

        services.AddSingleton<IInterpreter>(fun sp ->
                let publishHandler = PublishMessage.Handler(sp.GetRequiredService<NBB.Messaging.Abstractions.IMessageBusPublisher>())
                let publish = publishHandler.Handle >> Async.AwaitTask >> Async.RunSynchronously >> (fun _unit -> Unit())

                let interpreter = createInterpreter [
                    publish |> toHandlerReg
                    ElemDefinitionStoreRepo.loadCurrent payrollConnString |> toHandlerReg
                    ElemDefinitionStoreRepo.save payrollConnString |> toHandlerReg
                    Common.handleException |> toHandlerReg
                ]

                interpreter :> IInterpreter
            ) |> ignore

        services.AddResiliency() |> ignore
        services.AddNatsMessaging() |> ignore
        services
            .AddMessagingHost()
                .AddSubscriberServices(fun config -> config.AddTypes(typeof<AddDbElemDefinition>) |> ignore)
                .WithDefaultOptions()
                .UsePipeline(fun pipelineBuilder -> 
                    pipelineBuilder
                        .UseCorrelationMiddleware()
                        .UseExceptionHandlingMiddleware()
                        .UseDefaultResiliencyMiddleware()
                        .UseMiddleware<CommandMiddleware>()
                        |> ignore
                )
            |> ignore

    // Logging configuration
    let loggingConfig (context : HostBuilderContext) (loggingBuilder : ILoggingBuilder) =
        loggingBuilder
            .AddConsole()
            .AddDebug()
            |> ignore

    let host = 
        HostBuilder()
            .ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> appConfig)
            .ConfigureServices(Action<HostBuilderContext, IServiceCollection> serviceConfig)
            .ConfigureLogging(Action<HostBuilderContext, ILoggingBuilder> loggingConfig)
            .UseConsoleLifetime()
            .Build()

    host.RunAsync()  |> Async.AwaitTask |> Async.RunSynchronously
    0

