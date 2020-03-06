namespace PayrollCalculus

open NBB.Core.Effects
open NBB.Core.Effects.FSharp
open PayrollCalculus.SideEffects
open DomainTypes
open DataStructures

module DomainImpl =
    
    // ---------------------------
    // Types
    // ---------------------------

    type EvaluateElem = 
        ElemDefinitionCache                 // Dependency
         -> ElemCode                        // Input
         -> ComputationCtx                  // Input
         -> IEffect<Result<obj, string>>    // Output


    type EvaluateElems = ElemDefinitionCache -> ElemCode list -> ComputationCtx -> IEffect<Result<obj list, string>>
    type EvaluateElemsMultipleContexts = ElemDefinitionCache -> ElemCode list -> ComputationCtx list -> IEffect<Result<obj list list, string>>

    type private ComputeElem  = ElemDefinitionCache -> ElemCode -> StateEffect<ElemCache, Elem<obj>>


    // ---------------------------
    // Implementation
    // ---------------------------

    let rec private computeElem: ComputeElem =
        fun elemDefinitionCache elemCode -> 
            let computeFormula {formula=formula} :StateEffect<ElemCache, Elem<obj>> =
                stateEffect {
                    let! {func=func;parameters=parameters} = formula |> Parser.parseFormula elemDefinitionCache |> StateEffect.lift
                    let! paramElems =  parameters |> List.traverseStateEffect (ElemCode >> computeElem elemDefinitionCache)
                    
                    return paramElems |> List.toArray |> Elem.liftFunc func
                }
               
            let matchElemDefinition elemDefinition = 
                match elemDefinition.Type with
                | Db(dbElemDefinition) -> dbElemDefinition |> ElemValueRepo.load |> StateEffect.pure'
                | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition


            let compute() = 
                stateEffect {
                    let! elemResult = 
                        elemCode 
                        |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
                        |> Result.traverseStateEffect matchElemDefinition

                    return elemResult |> Result.sequenceElem |> Elem.flattenResult
                }


            stateEffect {
                let! elemCache = StateEffect.get ()

                return! 
                    match (elemCache.TryFind elemCode) with
                    | Some elem -> StateEffect.pure' elem
                    | None -> 
                        stateEffect { 
                            let! elem = compute ()
                            do! StateEffect.put (elemCache.Add (elemCode, elem))
                            return elem
                        }
            }

    let evaluateElem : EvaluateElem = 
        fun elemDefinitionCache elemCode ctx ->
            effect {
                let! (elem, _) = StateEffect.run (computeElem elemDefinitionCache elemCode) Map.empty
                return! Reader.run elem ctx
            }
        
    let evaluateElems : EvaluateElems = 
        fun elemDefinitionCache elemCodes ctx ->
            effect {
                let statefulElems = elemCodes |> List.traverseStateEffect (computeElem elemDefinitionCache)
                let! (elems, _) = StateEffect.run statefulElems Map.empty
                let! results = ReaderEffect.run (elems |> List.sequenceReaderEffect) ctx
                let result = results |> List.sequenceResult
                return result
            }

    let evaluateElemsMultipleContexts : EvaluateElemsMultipleContexts =
        fun elemDefinitionCache elemCodes ctxs ->
            effect {
                let x = evaluateElems elemDefinitionCache elemCodes 
                let! results = ctxs |> List.traverseEffect (fun ctx -> ReaderEffect.run x ctx)
                let result = results |> List.sequenceResult
                return result
            }
                  




