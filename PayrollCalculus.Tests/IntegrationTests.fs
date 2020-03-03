module IntegrationTests

open System
open System.IO
open System.Reflection
open FsUnit.Xunit
open Microsoft.Extensions.Configuration
open NBB.Core.Effects.FSharp
open Xunit

open PayrollCalculus
open PayrollCalculus.SideEffects
open PayrollCalculus.SideEffectHandlers
open Infra
open DataAccess
open DomainTypes
open DomainImpl


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

    let eff = effect {
        let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
        let! result = evaluateElems elemDefinitionCache [ElemCode "SalariuNet"; ElemCode "Impozit"] ctx

        return 
            match result with
            | value1::value2::[] -> (value1, value2)
            | _ -> failwith "Invalid result"
      }

    // Act
    let (result1 , result2) = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    result1 |> should equal (Ok (900m :> obj) : Result<obj, string>)
    result2 |> should equal (Ok (100m :> obj) : Result<obj, string>)