module IntegrationTests

open System
open System.IO
open System.Reflection
open FsUnit.Xunit
open Microsoft.Extensions.Configuration
open NBB.Core.Effects.FSharp
open Xunit

open PayrollCalculus
open PayrollCalculus.Domain
open PayrollCalculus.Domain.SideEffects
open PayrollCalculus.SideEffectHandlers
open Infra
open DataAccess
open DomainTypes

let configuration =
    let configurationBuilder = 
        ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddUserSecrets(Assembly.GetExecutingAssembly())
    configurationBuilder.Build()

let payrollConnString = configuration.GetConnectionString "PayrollCalculus"
let hcmConnectionString = configuration.GetConnectionString "Hcm"

[<Fact>]
let ``It shoud evaluate formula with params (integration)`` () =

    // Arrange
    let ctx: ComputationCtx = {PersonId = PersonId (Guid.Parse("33733a83-d4a9-43c8-ab4e-49c53919217d")); YearMonth = {Year = 2009; Month = 1}}

    let interpreter = interpreter [
            FormulaParser.handle                                        |> toHandlerReg;
            ElemDefinitionRepo.handleLoadDefinitions payrollConnString  |> toHandlerReg;
            ElemValueRepo.handleLoadValue hcmConnectionString           |> toHandlerReg;
        ]

    let eff = Application.Evaluation.evaluateCodes ["SalariuNet"; "Impozit"]  ctx

    // Act
    let result = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert  
    result |> should equal (Ok ([900m :> obj; 100m :> obj]) : Result<obj list, string>)
    