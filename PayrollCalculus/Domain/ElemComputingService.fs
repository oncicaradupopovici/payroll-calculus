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
        Formula: string
        ElemDefinitions: Map<ElemCode, ElemDefinition>
    }
    with interface ISideEffect<ParseFormulaResult>
    and ParseFormulaResult = {
        Func: obj [] -> obj
        Parameters: string list
    }

    let parseFormula definitions formula = (Effect.Of {Formula=formula; ElemDefinitions=definitions}) |> Effect.wrap

module ElemValueRepo = 
    type LoadSideEffect = {
        Definition: DbElemDefinition
        Ctx: ComputationCtx
    }
    with interface ISideEffect<Result<obj, string>>

    let load definition ctx = (Effect.Of {Definition=definition; Ctx=ctx}) |> Effect.wrap
                    


module ElemComputingService =

    type private ComputeElem  = ElemDefinitionStore -> ElemCode -> StateEffect<ElemCache, Elem<obj>>
    let rec private computeElem: ComputeElem =
        fun elemDefinitionStore elemCode -> 
            let buildFormulaElem ({formula=formula}: FormulaElemDefinition)  =
                stateEffect {
                    let! {Func=func;Parameters=parameters} = formula |> Parser.parseFormula elemDefinitionStore.ElemDefinitions |> StateEffect.lift
                    let! paramElems = parameters |> List.traverseStateEffect (ElemCode >> computeElem elemDefinitionStore)

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
                    ElemDefinitionStore.findElemDefinition elemDefinitionStore elemCode
                    |> Result.traverseStateEffect buildElem

                return elemResult 
                    |> Elem.flattenResult
                    |> ReaderStateEffect.addCaching elemCode

            } |> StateEffect.addCaching elemCode     

    type EvaluateElem = ElemDefinitionStore -> ElemCode -> ComputationCtx -> Effect<Result<obj, string>>
    let evaluateElem : EvaluateElem = 
        fun elemDefinitionStore elemCode ctx ->
            effect {
                let! (elem, _) = StateEffect.run (computeElem elemDefinitionStore elemCode) Map.empty
                let! (elemValue, _) = ReaderStateEffect.run elem ctx Map.empty
                return elemValue
            }
       
    type EvaluateElems = ElemDefinitionStore -> ElemCode list -> ComputationCtx  -> Effect<Result<obj list, string>>
    let evaluateElems : EvaluateElems = 
        fun elemDefinitionStore elemCodes ctx ->
            effect {
                let statefulElems = elemCodes |> List.traverseStateEffect (computeElem elemDefinitionStore)
                let! (elems, _) = StateEffect.run statefulElems Map.empty
                let! (elemValues, _) =  ReaderStateEffect.run (elems |> List.sequenceReaderStateEffect) ctx Map.empty
                let result = elemValues |> List.sequenceResult
                return result
            }

    type EvaluateElemsMultipleContexts = ElemDefinitionStore -> ElemCode list -> ComputationCtx list -> Effect<Result<obj list list, string>>
    let evaluateElemsMultipleContexts : EvaluateElemsMultipleContexts =
        fun elemDefinitionStore elemCodes ctxs ->
            effect {
                let x = evaluateElems elemDefinitionStore elemCodes 
                let! results = ctxs |> List.traverseEffect (ReaderEffect.run x)
                let result = results |> List.sequenceResult
                return result
            }



