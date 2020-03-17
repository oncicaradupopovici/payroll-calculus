namespace PayrollCalculus.Domain

open System
open NBB.Core.Effects.FSharp
open NBB.Core.FSharp.Data.Reader
open NBB.Core.Effects.FSharp.Data.ReaderEffect
open NBB.Core.Effects.FSharp.Data.ReaderStateEffect
open NBB.Core.Effects.FSharp.Data.StateEffect
open NBB.Core.Effects
open NBB.Core.FSharp.Data

type Elem<'T> = ReaderStateEffect<ComputationCtx, ElemValueCache, Result<'T, string>>
and ComputationCtx = {
    PersonId: PersonId
    YearMonth: YearMonth
}
and YearMonth = {
    Year: int
    Month: int
}
and PersonId = PersonId of Guid
and ElemValueCache = Map<ElemCode, Result<obj, string>>

type ElemCache = Map<ElemCode, Elem<obj>>

module Elem = 
    let liftFunc (func: obj[] -> obj) (arr: Elem<obj> []) : Elem<obj> =
        arr 
            |> Array.toList
            |> List.sequenceReaderStateEffect
            |> ReaderStateEffect.map (List.sequenceResult >> Result.map (List.toArray >> func))
        
    let flattenResult (elemResult: Result<Elem<obj>, string>) : Elem<obj> = 
        elemResult
            |> Result.sequenceReaderStateEffect 
            |> ReaderStateEffect.map (Result.join)

module Parser =
    type ParseFormulaSideEffect = {
        formula:string;
        definitions: ElemDefinitionCache;
    }
    with interface ISideEffect<ParseFormulaResult>
    and ParseFormulaResult = {
        func: obj [] -> obj
        parameters: string list
    }

    let parseFormula definitions formula = (Effect.Of {formula=formula; definitions=definitions}) |> Effect.wrap

module ElemDefinitionRepo =
    type LoadDefinitionsSideEffect () =
        interface ISideEffect<Map<ElemCode, ElemDefinition>>

    let loadDefinitions () = (Effect.Of  (LoadDefinitionsSideEffect ())) |> Effect.wrap

module ElemValueRepo = 
    type LoadSideEffect = {
        definition: DbElemDefinition
        ctx: ComputationCtx
    }
    with interface ISideEffect<Result<obj, string>>

    let load definition ctx = (Effect.Of {definition=definition; ctx=ctx}) |> Effect.wrap
                    


module ElemComputingService =

    type private ComputeElem  = ElemDefinitionCache -> ElemCode -> StateEffect<ElemCache, Elem<obj>>
    let rec private computeElem: ComputeElem =
        fun elemDefinitionCache elemCode -> 
            let buildFormulaElem ({formula=formula}: FormulaElemDefinition)  =
                stateEffect {
                    let! {func=func;parameters=parameters} = formula |> Parser.parseFormula elemDefinitionCache |> StateEffect.lift
                    let! paramElems = parameters |> List.traverseStateEffect (ElemCode >> computeElem elemDefinitionCache)

                    return Elem.liftFunc func (paramElems |> List.toArray)
                }

            let buildDbElem (dbElemDefinition) =
                stateEffect { 
                    return (ElemValueRepo.load dbElemDefinition) |> Reader.map StateEffect.lift
                }
              
            let buildElem elemDefinition = 
                match elemDefinition.Type with
                    | Db dbElemDefinition           -> buildDbElem dbElemDefinition
                    | Formula formulaElemDefinition -> buildFormulaElem formulaElemDefinition

            stateEffect {
                let! elemResult = 
                    ElemDefinitionCache.findElemDefinition elemDefinitionCache elemCode
                    |> Result.traverseStateEffect buildElem

                return elemResult 
                    |> Elem.flattenResult
                    |> ReaderStateEffect.addCaching elemCode

            } |> StateEffect.addCaching elemCode     

    type EvaluateElem = ElemDefinitionCache -> ElemCode -> ComputationCtx -> Effect<Result<obj, string>>
    let evaluateElem : EvaluateElem = 
        fun elemDefinitionCache elemCode ctx ->
            effect {
                let! (elem, _) = StateEffect.run (computeElem elemDefinitionCache elemCode) Map.empty
                let! (elemValue, _) = ReaderStateEffect.run elem ctx Map.empty
                return elemValue
            }
       
    type EvaluateElems = ElemDefinitionCache -> ElemCode list -> ComputationCtx  -> Effect<Result<obj list, string>>
    let evaluateElems : EvaluateElems = 
        fun elemDefinitionCache elemCodes ctx ->
            effect {
                let statefulElems = elemCodes |> List.traverseStateEffect (computeElem elemDefinitionCache)
                let! (elems, _) = StateEffect.run statefulElems Map.empty
                let! (elemValues, _) =  ReaderStateEffect.run (elems |> List.sequenceReaderStateEffect) ctx Map.empty
                let result = elemValues |> List.sequenceResult
                return result
            }

    type EvaluateElemsMultipleContexts = ElemDefinitionCache -> ElemCode list -> ComputationCtx list -> Effect<Result<obj list list, string>>
    let evaluateElemsMultipleContexts : EvaluateElemsMultipleContexts =
        fun elemDefinitionCache elemCodes ctxs ->
            effect {
                let x = evaluateElems elemDefinitionCache elemCodes 
                let! results = ctxs |> List.traverseEffect (ReaderEffect.run x)
                let result = results |> List.sequenceResult
                return result
            }



