module Tests

open System
open Xunit
open PayrollCalculus.Domain
open PayrollCalculus
open FsUnit.Xunit
open NBB.Core.Effects.FSharp

module Handlers =
    open Domain.ElemValueRepo
    open Domain.Parser
    open NBB.Core.Effects
    open System.Threading
    open System.Threading.Tasks
    open Parser
    open Microsoft.Extensions.DependencyInjection;

    type DbResult = Result<obj, string>

    type DbSideEffectHandler(dbHandler: LoadSideEffect -> DbResult) =
        interface ISideEffectHandler<LoadSideEffect, DbResult> with
            member this.Handle(sideEffect, _) =
                Task.FromResult (Result.Ok (1 :> obj))
        member this.Handle(sideEffect : ISideEffect<DbResult>, _) : DbResult =
            let sideEffect =  sideEffect :?> LoadSideEffect
            dbHandler sideEffect

    type FormulaSideEffectHandler(formulaHandler : ParseFormulaSideEffect -> ParseFormulaResult) =   
        interface ISideEffectHandler<ParseFormulaSideEffect, ParseFormulaResult> with
            member this.Handle(sideEffect, _) =
                Task.FromResult {func= (fun _ -> (1:>obj)); parameters= ["a"; "b"]}
        member this.Handle(sideEffect: ISideEffect<ParseFormulaResult>, _ : CancellationToken) =
            let sideEffect =  sideEffect :?> ParseFormulaSideEffect
            formulaHandler sideEffect
    
    let getIoCFactory(dbHandler, formulaHandler) =
        let formulaHandler' = Func<IServiceProvider, ISideEffectHandler<ParseFormulaSideEffect, ParseFormulaResult>>(fun _ -> (new FormulaSideEffectHandler(formulaHandler) :> ISideEffectHandler<ParseFormulaSideEffect, ParseFormulaResult>))
        let dbHandler' = Func<IServiceProvider, ISideEffectHandler<LoadSideEffect, DbResult>>(fun _ -> (new DbSideEffectHandler(dbHandler) :> ISideEffectHandler<LoadSideEffect, DbResult>))

        let services = new ServiceCollection()
        services.AddScoped<ISideEffectHandler<LoadSideEffect, DbResult>>(dbHandler') |> ignore
        services.AddScoped<ISideEffectHandler<ParseFormulaSideEffect, ParseFormulaResult>>(formulaHandler') |> ignore
        //use container = services.BuildServiceProvider()
        let container = services.BuildServiceProvider()
        new SideEffectHandlerFactory(container)


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

    let factory = Handlers.getIoCFactory((fun _ -> Result.Ok (1:> obj)) , (fun _ -> {func= (fun _ -> (1:>obj)); parameters= []}))
    let interpreter = NBB.Core.Effects.Interpreter(factory)

    let eff = effect {
          let! elemDefinitionCache = loadElemDefinitions ()
          let! (elem1, _) = computeElem4 elemDefinitionCache code1 Map.empty
          let! value = elem1 ctx

          return value
      }

    // Act

    let result = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    let res = match result with
                    | Error e -> e :> obj
                    | Ok x -> x

    res |> should equal (1 :> obj)



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

    let factory = Handlers.getIoCFactory((fun _ -> Result.Ok (1:> obj)) , (fun _ -> {func= (fun _ -> (3 :> obj)); parameters= []}))
    let interpreter = NBB.Core.Effects.Interpreter(factory)

    let eff = effect {
          let! elemDefinitionCache = loadElemDefinitions ()
          let! (elem1, _) = computeElem4 elemDefinitionCache code1 Map.empty
          let! value = elem1 ctx

          return value
    }

    // Act

    let result = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    let res = match result with
                    | Error e -> e :> obj
                    | Ok x -> x

    res |> should equal (3 :> obj)


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

    let factory = Handlers.getIoCFactory((fun _ -> Result.Ok (1:> obj)) , formulaHandler)
    let interpreter = NBB.Core.Effects.Interpreter(factory)

    let eff = effect {
          let! elemDefinitionCache = loadElemDefinitions ()
          let! (elem1, cache) = computeElem4 elemDefinitionCache code1 Map.empty
          let! (elem2, cache') = computeElem4 elemDefinitionCache code2 cache
          let! value1 = elem1 ctx
          let! value2 = elem2 ctx

          return (value1, value2)
      }

    // Act

    let (result1, result2) = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert
    let res1 = match result1 with
                    | Error e -> e :> obj
                    | Ok x -> x

    let res2 = match result2 with
                    | Error e -> e :> obj
                    | Ok x -> x

    res1 |> should equal (4 :> obj)
    res2 |> should equal (1 :> obj)
