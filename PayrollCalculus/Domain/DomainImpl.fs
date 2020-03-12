namespace PayrollCalculus.Domain

open NBB.Core.Effects.FSharp
open NBB.Core.Effects.FSharp.Data
open NBB.Core.FSharp.Data.ReaderState
open NBB.Core.Effects.FSharp.Data.StateEffect
open PayrollCalculus.Domain.SideEffects
open PayrollCalculus.DataStructures
open DomainTypes
open Cache
open NBB.Core.Effects.FSharp.Data.ReaderEffect
open NBB.Core.FSharp.Data
open NBB.Core.Effects.FSharp.Data.ReaderStateEffect
open NBB.Core.FSharp.Data.Reader

module DomainImpl =
    
    // ---------------------------
    // Types
    // ---------------------------

    type EvaluateElem = 
        ElemDefinitionCache                 // Dependency
         -> ElemCode                        // Input
         -> ComputationCtx                  // Input
         -> Effect<Result<obj, string>>     // Output


    type EvaluateElems = ElemDefinitionCache -> ElemCode list -> ComputationCtx  -> Effect<Result<obj list, string>>
    type EvaluateElemsMultipleContexts = ElemDefinitionCache -> ElemCode list -> ComputationCtx list -> Effect<Result<obj list list, string>>

    type private ComputeElem  = ElemDefinitionCache -> ElemCode -> StateEffect<ElemCache, Elem<obj>>


    // ---------------------------
    // Implementation
    // ---------------------------

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


    let evaluateElem : EvaluateElem = 
        fun elemDefinitionCache elemCode ctx ->
            effect {
                let! (elem, _) = StateEffect.run (computeElem elemDefinitionCache elemCode) Map.empty
                let! (elemValue, _) = ReaderStateEffect.run elem ctx Map.empty
                return elemValue
            }
        
    let evaluateElems : EvaluateElems = 
        fun elemDefinitionCache elemCodes ctx ->
            effect {
                let statefulElems = elemCodes |> List.traverseStateEffect (computeElem elemDefinitionCache)
                let! (elems, _) = StateEffect.run statefulElems Map.empty
                let! (elemValues, _) =  ReaderStateEffect.run (elems |> List.sequenceReaderStateEffect) ctx Map.empty
                let result = elemValues |> List.sequenceResult
                return result
            }

    let evaluateElemsMultipleContexts : EvaluateElemsMultipleContexts =
        fun elemDefinitionCache elemCodes ctxs ->
            effect {
                let x = evaluateElems elemDefinitionCache elemCodes 
                let! results = ctxs |> List.traverseEffect (ReaderEffect.run x)
                let result = results |> List.sequenceResult
                return result
            }
                  




