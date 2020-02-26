module IntegrationTests
open System
open Xunit
open PayrollCalculus.Domain
open PayrollCalculus
open FsUnit.Xunit
open NBB.Core.Effects.FSharp

[<Fact>]
let ``It shoud evaluate formula with params (integration)`` () =
    
    // Arrange
    let ctx: ComputationCtx = {PersonId = PersonId (Guid.Parse("33733a83-d4a9-43c8-ab4e-49c53919217d")); YearMonth = {Year = 2009; Month = 1}}
 

    let interpreter = Infra.interpreter [
            SideEffectHandlers.FormulaParser.handle |> Infra.toHandlerReg;
            SideEffectHandlers.DataAccess.ElemDefinitionRepo.handleLoadDefinitions |> Infra.toHandlerReg;
            SideEffectHandlers.DataAccess.ElemValueRepo.handleLoadValue |> Infra.toHandlerReg;
        ]

    let eff = effect {
        let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
        let! result = evaluateElems elemDefinitionCache [ElemCode "SalariuNet"; ElemCode "Impozit"] ctx

        return 
            match result with
            | value1::value2::[] -> (value1, value2)
            | _ -> raise (Exception "Invalid result") 
      }

    // Act
    let (result1 , result2) = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    result1 |> should equal (Ok (900m :> obj) : Result<obj, string>)
    result2 |> should equal (Ok (100m :> obj) : Result<obj, string>)