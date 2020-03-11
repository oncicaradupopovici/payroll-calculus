namespace PayrollCalculus

open NBB.Core.Effects.FSharp
open NBB.Core.Effects.FSharp.Data
open NBB.Core.Effects.FSharp.Data.StateEffect
open PayrollCalculus.SideEffects
open Parser
open DomainTypes
open DataStructures
open NBB.Core.Effects.FSharp.Data.ReaderEffect
open NBB.Core.FSharp.Data
open NBB.Core.Effects.FSharp.Data.ReaderStateEffect

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
            let computeFormula ({formula=formula}: FormulaElemDefinition)  =
                stateEffect {
                    let! {func=func;parameters=parameters} = formula |> Parser.parseFormula elemDefinitionCache |> StateEffect.lift
                    let! paramElems =  parameters |> List.traverseStateEffect (ElemCode >> computeElem elemDefinitionCache)
                    
                    return paramElems |> List.toArray |> Elem.liftFunc func
                }
               
            let matchElemDefinition elemDefinition = 
                match elemDefinition.Type with
                | Db(dbElemDefinition) -> 
                    dbElemDefinition |> ElemValueRepo.load |> ReaderStateEffect.hoistReaderEffect  |> Effect.pure' |> StateEffect.lift
                | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition


            let addCaching elem =
                readerStateEffect {
                    let! (valueCache: ElemValueCache) = ReaderStateEffect.get ()
                    match (valueCache.TryFind elemCode) with
                        | Some result -> 
                            return result
                        | None -> 
                            let! result = elem
                            do! ReaderStateEffect.modify(fun cache -> cache.Add (elemCode, result))
                            return result
                 }

            let compute() = 
                stateEffect {
                    let! elemResult = 
                        elemCode 
                        |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
                        |> Result.traverseStateEffect matchElemDefinition

                    return elemResult |> Result.sequenceReaderStateEffect |> ReaderStateEffect.map (Result.join) |> addCaching
                }         

            stateEffect {
                let! elemCache = StateEffect.get ()
                    
                match (elemCache.TryFind elemCode) with
                | Some elem -> 
                    return elem
                | None -> 
                    let! elem = compute () 
                    do! StateEffect.modify(fun cache -> cache.Add (elemCode, elem))
                    return elem     
            } 


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
                  




