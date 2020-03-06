namespace PayrollCalculus.Api

open System
open System.Reflection
open Giraffe
open Giraffe.Serialization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open NBB.Core.Effects
open PayrollCalculus
open PayrollCalculus.SideEffectHandlers
open DataAccess
open Infra

// ---------------------------------
// Web app
// ---------------------------------

module App =
    let webApp =
        choose [
            route "/" >=>  text "Hello"
            subRoute "/api"
                (choose [
                    Handlers.Evaluation.handler
                    //ContractsAlternative.handler
                ])
            setStatusCode 401 >=> text "Not Found" ]

    // ---------------------------------
    // Error handler
    // ---------------------------------

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    // ---------------------------------
    // Config and Main
    // ---------------------------------

    let configureCors (builder : CorsPolicyBuilder) =
        builder.WithOrigins("http://localhost:8080")
               .AllowAnyMethod()
               .AllowAnyHeader()
               |> ignore

    let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IHostingEnvironment>()
        (match env.IsDevelopment() with
        | true  -> app.UseDeveloperExceptionPage()
        | false -> app.UseGiraffeErrorHandler errorHandler)
            .UseCors(configureCors)
            .UseGiraffe(webApp)

    let configureServices (context: WebHostBuilderContext) (services : IServiceCollection) =
        let payrollConnString = context.Configuration.GetConnectionString "PayrollCalculus"
        let hcmConnectionString = context.Configuration.GetConnectionString "Hcm"

        let interpreter = interpreter [
                   FormulaParser.handle                                        |> toHandlerReg;
                   ElemDefinitionRepo.handleLoadDefinitions payrollConnString  |> toHandlerReg;
                   ElemValueRepo.handleLoadValue hcmConnectionString           |> toHandlerReg;
               ]

        services.AddCors()
            .AddGiraffe() 
            .AddSingleton<IJsonSerializer>(
                NewtonsoftJsonSerializer(NewtonsoftJsonSerializer.DefaultSettings))
            .AddSingleton<IInterpreter>(interpreter)
            |> ignore


    let configureAppConfiguration  (context: WebHostBuilderContext) (config: IConfigurationBuilder) =  
        config
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName, true)
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .AddEnvironmentVariables() |> ignore

    let configureLogging (builder : ILoggingBuilder) =
        builder.AddFilter(fun l -> l.Equals LogLevel.Error)
               .AddConsole()
               .AddDebug() |> ignore

    [<EntryPoint>]
    let main _ =
        WebHostBuilder()
            .UseKestrel()
            .ConfigureAppConfiguration(configureAppConfiguration)
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .ConfigureLogging(configureLogging)
            .Build()
            .Run()
        0