﻿// Learn more about F# at http://fsharp.org

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
open PayrollCalculus.PublishedLanguage
open PayrollCalculus.Infra
open CommandHandler
open PayrollCalculus.Infra.DataAccess
open NBB.Core.Abstractions
open NBB.Core.Effects.FSharp

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

    // Services configuration
    let serviceConfig (context : HostBuilderContext) (services : IServiceCollection) =

        let payrollConnString = context.Configuration.GetConnectionString "PayrollCalculus"

        let applicationPipeline = 
            let commandHandler = createCommandHandler [
                Application.AddDbElemDefinition.handler |> toCommandHandlerReg
            ]

            fun message ->
                match box message with
                | :? ICommand as command -> commandHandler command 
                | _ -> failwith "Invalid message"

        services.AddEffects() |> ignore
        services.AddMessagingEffects() |> ignore
        services
            .AddSideEffectHandler(ElemDefinitionStoreRepo.loadCurrent payrollConnString)
            .AddSideEffectHandler(ElemDefinitionStoreRepo.save payrollConnString)
            .AddSideEffectHandler(Common.handleException)
            |> ignore;

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
                        .UseEffectMiddleware(fun m -> m |> applicationPipeline |> Effect.unWrap |> EffectExtensions.ToUnit)
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

