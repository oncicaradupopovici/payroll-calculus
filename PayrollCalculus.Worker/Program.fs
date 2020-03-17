// Learn more about F# at http://fsharp.org

open System
open System.IO
open System.Threading
open System.Linq
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Serilog
open Serilog.Events
open NBB.Correlation.Serilog
open NBB.Messaging.Host
open NBB.Messaging.Nats
open NBB.Messaging.Host.MessagingPipeline;
open PayrollCalculus.PublishedLanguage
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Reflection
open PayrollCalculus.Infra.CommandHandler
open PayrollCalculus.Infra.Interpreter
open PayrollCalculus
open NBB.Core.Pipeline
open NBB.Messaging.DataContracts
open NBB.Messaging.Effects
open System.Threading.Tasks
open NBB.Core.Abstractions
open NBB.Core.Effects.FSharp
open NBB.Core.Effects
open NBB.Resiliency


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

[<EntryPoint>]
let main argv =

    // App configuration
    let appConfig (context : HostBuilderContext) (configApp : IConfigurationBuilder) =
        configApp
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional = true)
            .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, optional = true)
            .AddEnvironmentVariables()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .AddCommandLine(argv)
            |> ignore

    // Services configuration
    let serviceConfig (context : HostBuilderContext) (services : IServiceCollection) =
        services.AddScoped<CommandHandler>(Func<IServiceProvider, CommandHandler>(fun _sp -> 
             commandHandler [
                        Application.ElemDefinition.AddElemDefinition.handle |> toCommandHandlerReg
             ]
        )) |> ignore

        services.AddSingleton<IInterpreter>(fun sp ->
                let publisher = sp.GetRequiredService<NBB.Messaging.Abstractions.IMessageBusPublisher>() 
                //let publishHandler =  new SideEffectHandlerWrapper<Unit>(PublishMessage.Handler(publisher))
                let publishHandler = fun (msg: PublishMessage.SideEffect) -> publisher.PublishAsync(msg.Message) |> Async.AwaitTask |> Async.RunSynchronously; Unit()


                let interpreter = interpreter [
                    //(typeof<PublishMessage.SideEffect>,  publishHandler :> ISideEffectHandler)  
                    publishHandler |> toHandlerReg
                ]

                interpreter :> IInterpreter
            ) |> ignore

        services.AddResiliency() |> ignore
        services.AddNatsMessaging() |> ignore
        services
            .AddMessagingHost()
                .AddSubscriberServices(fun config -> config.AddTypes(typeof<Commands.AddElemDefinition>) |> ignore)
                .WithDefaultOptions()
                .UsePipeline(fun pipelineBuilder -> 
                    pipelineBuilder
                        .UseCorrelationMiddleware()
                        .UseExceptionHandlingMiddleware()
                        .UseDefaultResiliencyMiddleware()
                        //.Use(commandMidleware)
                        .UseMiddleware<CommandMiddleware>()
                        |> ignore
                )
            |> ignore

    // Logging configuration
    let loggingConfig (context : HostBuilderContext) (loggingBuilder : ILoggingBuilder) =
        //let logger = 
        //    LoggerConfiguration()
        //            .MinimumLevel.Debug()
        //            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        //            .Enrich.FromLogContext()
        //            .Enrich.With<CorrelationLogEventEnricher>()
        //            .WriteTo.Console()
        //            .CreateLogger() :> ILogger
        
        //Log.Logger = logger |> ignore

        //loggingBuilder
        //    .AddSerilog()
        //    |> ignore
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

    Log.Information "Starting NBB.Worker"

    host.RunAsync()  |> Async.AwaitTask |> Async.RunSynchronously


    0

