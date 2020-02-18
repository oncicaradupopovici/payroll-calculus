module Tests

open System
open Xunit
open PayrollCalculus.Domain
open PayrollCalculus
open FsUnit.Xunit

[<Fact>]
let ``It shoud evaluate data access element`` () =
    let code1 = ElemCode "code1"
    let elemDefinitionCache : ElemDefinitionCache = 
        Map.empty
            .Add(code1, {Code = code1; Type = DataAccess ("aa", "bb")} )

    let elemCache : ElemCache = Map.empty
           
    let processElem = processElem elemDefinitionCache
    let processElemResult = processElem code1
    let elemResult = StateResult.run processElemResult elemCache

    let res = match elemResult with 
        | Result.Error e -> e
        | Result.Ok (Elem computation, elemCache) -> 
            let ctx: ComputationCtx = {PersonId = PersonId (Guid.NewGuid()); YearMonth = {Year = 2009; Month = 1}}
            let effect = computation ctx
            let interpreter = NBB.Core.Effects.Interpreter(null)
            interpreter.Interpret(effect).GetAwaiter().GetResult().ToString()
   
    res |> should equal "1"    

[<Fact>]
let ``It shoud evaluate formula without params`` () =
    let code1 = ElemCode "code1"
    let elemDefinitionCache : ElemDefinitionCache = 
        Map.empty
            .Add(code1, {Code = code1; Type = Formula("1 + 2", [])} )

    let elemCache : ElemCache = Map.empty
           
    let processElem = processElem elemDefinitionCache
    let processElemResult = processElem code1
    let elemResult = StateResult.run processElemResult elemCache

    let res = match elemResult with 
        | Result.Error e -> e
        | Result.Ok (Elem computation, elemCache) -> 
            let ctx: ComputationCtx = {PersonId = PersonId (Guid.NewGuid()); YearMonth = {Year = 2009; Month = 1}}
            let effect = computation ctx
            let interpreter = NBB.Core.Effects.Interpreter(null)
            interpreter.Interpret(effect).GetAwaiter().GetResult().ToString()

    res |> should equal "3"    

[<Fact>]
let ``It shoud evaluate formula with params `` () =
    let code1 = ElemCode "code1"
    let code2 = ElemCode "code2"
    let code3 = ElemCode "code3"
    let elemDefinitionCache : ElemDefinitionCache = 
        Map.empty
            .Add(code1, {Code = code1; Type = Formula("1 + code2 + code3", [])} )
            .Add(code2, {Code = code2; Type = DataAccess("a", "b")} )
            .Add(code3, {Code = code3; Type = Formula("1 + 2", [])} )
    
    let elemCache : ElemCache = Map.empty
               
    let processElem = processElem elemDefinitionCache
    let processElemResult = processElem code1
    let elemResult = StateResult.run processElemResult elemCache
    
    let res = match elemResult with 
        | Result.Error e -> e
        | Result.Ok (Elem computation, elemCache) -> 
            let ctx: ComputationCtx = {PersonId = PersonId (Guid.NewGuid()); YearMonth = {Year = 2009; Month = 1}}
            let effect = computation ctx
            let interpreter = NBB.Core.Effects.Interpreter(null)
            interpreter.Interpret(effect).GetAwaiter().GetResult().ToString()
    
    res |> should equal "5"    