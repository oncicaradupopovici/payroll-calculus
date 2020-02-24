module Tests

open System
open Xunit
open PayrollCalculus.Domain
open PayrollCalculus
open FsUnit.Xunit
open NBB.Core.Effects.FSharp
open DataStructures

module Handlers =
    open Domain.ElemValueRepo
    open Domain.Parser
    open NBB.Core.Effects
    open System.Threading

    type DbResult = Result<obj, string>

    type GenericSideEffectHandler(dbHandler : LoadSideEffect -> DbResult, formulaHandler: ParseFormulaSideEffect -> ParseFormulaResult) =
        interface ISideEffectHandler 
        member _.Handle(sideEffect: obj, _ : CancellationToken) =
           match (sideEffect) with
                | :? LoadSideEffect as lse -> dbHandler(lse) :> obj
                | :? ParseFormulaSideEffect as pfe -> formulaHandler (pfe) :> obj
                | _ -> null

    let getHandlerFactory (dbHandler, formulaHandler)  =
        { new ISideEffectHandlerFactory with
            member this.GetSideEffectHandlerFor<'TOutput> (_: ISideEffect<'TOutput>) = 
               SideEffectHandlerWrapper(new GenericSideEffectHandler(dbHandler, formulaHandler)) :> ISideEffectHandler<ISideEffect<'TOutput>, 'TOutput>
        }


[<Fact>]
let ``It shoud evaluate data access element`` () =
    
    // Arrange
    let code1 = ElemCode "code1"
    let loadElemDefinitions () =
        let elemDefinitionCache : ElemDefinitionCache = 
               Map.empty
                   .Add(code1, {Code = code1; Type = Db {table="aa"; column ="bb"} })

        Effect.pure' elemDefinitionCache

    let ctx: ComputationCtx = {PersonId = PersonId (Guid.NewGuid()); YearMonth = {Year = 2009; Month = 1}}

    let factory = Handlers.getHandlerFactory((fun _ -> Result.Ok (1:> obj)) , (fun _ -> {func= (fun _ -> (1:>obj)); parameters= []}))
    let interpreter = NBB.Core.Effects.Interpreter(factory)

    let eff = effect {
          let! elemDefinitionCache = loadElemDefinitions ()
          let! value = evaluateElem elemDefinitionCache code1 ctx

          return value
      }

    // Act

    let result = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    let expected:Result<obj,string> = Ok (1:>obj)
    result |> should equal expected



[<Fact>]
let ``It shoud evaluate formula without params`` () =
    
    // Arrange
    let code1 = ElemCode "code1"
    let loadElemDefinitions () =
        let elemDefinitionCache : ElemDefinitionCache = 
               Map.empty
                   .Add(code1, {Code = code1; Type = Formula {formula="1 + 2"; deps =[]} })

        Effect.pure' elemDefinitionCache

    let ctx: ComputationCtx = {PersonId = PersonId (Guid.NewGuid()); YearMonth = {Year = 2009; Month = 1}}

    let factory = Handlers.getHandlerFactory((fun _ -> Result.Ok (1:> obj)) , (fun _ -> {func= (fun _ -> (3 :> obj)); parameters= []}))
    let interpreter = NBB.Core.Effects.Interpreter(factory)

    let eff = effect {
          let! elemDefinitionCache = loadElemDefinitions ()
          let! value = evaluateElem elemDefinitionCache code1 ctx

          return value
    }

    // Act

    let result = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    let expected:Result<obj,string> = Ok (3:>obj)
    result |> should equal expected


[<Fact>]
let ``It shoud evaluate formula with params`` () =
    
    // Arrange
    let code1 = ElemCode "code1"
    let code2 = ElemCode "code2"
    let code3 = ElemCode "code3"
    let loadElemDefinitions () =
        let elemDefinitionCache : ElemDefinitionCache = 
               Map.empty
                   .Add(code1, {Code = code1; Type = Formula {formula="1 + code2 + code3"; deps =[]} })
                   .Add(code2, {Code = code2; Type = Db {table="aa"; column ="bb"} })
                   .Add(code3, {Code = code3; Type = Formula {formula="1 + code2"; deps =[]} })           

        Effect.pure' elemDefinitionCache

    let ctx: ComputationCtx = {PersonId = PersonId (Guid.NewGuid()); YearMonth = {Year = 2009; Month = 1}}

    let formulaHandler ({formula=formula} : Parser.ParseFormulaSideEffect) : Parser.ParseFormulaResult =
        match formula with
        | "1 + code2 + code3" -> {func= (fun ([|code2; code3|]) -> box(1 + (unbox<int> code2) +  (unbox<int> code3))); parameters=["code2"; "code3"] }
        | "1 + code2" -> {func= (fun ([|code2|]) -> box(1 + (unbox<int> code2))); parameters=["code2"] }
        | _ -> {func= (fun _ -> (1:>obj)); parameters= []}

    let factory = Handlers.getHandlerFactory((fun _ -> Result.Ok (1:> obj)) , formulaHandler)
    let interpreter = NBB.Core.Effects.Interpreter(factory)

    let eff = effect {
          let! elemDefinitionCache = loadElemDefinitions ()

          let! value1::value2::_ = evaluateElems elemDefinitionCache [code1; code2] ctx

          return (value1, value2)
      }

    // Act

    let (result1 , result2) = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    let expected1:Result<obj, string> = Ok (4:>obj)
    result1 |> should equal expected1

    let expected2:Result<obj, string> = Ok (1:>obj)
    result2 |> should equal expected2
