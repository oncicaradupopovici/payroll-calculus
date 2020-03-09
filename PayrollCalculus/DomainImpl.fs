namespace PayrollCalculus

open NBB.Core.Effects.FSharp
open PayrollCalculus.SideEffects
open Parser
open DomainTypes
open FSharpPlus
open FSharpPlus.Data
open DataStructures

module DomainImpl =
    
    // ---------------------------
    // Types
    // ---------------------------

    type EvaluateElem = 
        ElemDefinitionCache                 // Dependency
         -> ElemCode                        // Input
         -> ComputationCtx                  // Input
         -> Effect<Result<obj, string>>     // Output


    type EvaluateElems = ElemDefinitionCache -> ElemCode list -> ComputationCtx -> Effect<Result<obj list, string>>
    type EvaluateElemsMultipleContexts = ElemDefinitionCache -> ElemCode list -> ComputationCtx list -> Effect<Result<obj list list, string>>

    type private ComputeElem  = ElemDefinitionCache -> ElemCode -> StateT<ElemCache, Effect<Elem<obj> * ElemCache>>


    // ---------------------------
    // Implementation
    // ---------------------------

    let rec private computeElem: ComputeElem =
        fun elemDefinitionCache elemCode -> 
            let computeFormula ({formula=formula}: FormulaElemDefinition)  =
                monad {
                    let! {func=func;parameters=parameters} = formula |> Parser.parseFormula elemDefinitionCache |> lift
                    let! paramElems =  parameters |> traverse (ElemCode >> computeElem elemDefinitionCache)                    

                    return paramElems |> List.toArray |> Elem.liftFunc func
                }
               
            let matchElemDefinition elemDefinition = 
                match elemDefinition.Type with
                | Db(dbElemDefinition) -> dbElemDefinition |> ElemValueRepo.load |> ReaderT |> result |> lift
                | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition

            let compute() = 
                monad {
                    let! elemResult = 
                        elemCode 
                        |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
                        |> Result.traverse matchElemDefinition

                    return elemResult |> Result.sequence |> map join
                }

            let result =
                monad {
                    let! (elemCache: ElemCache) = get 
                    
                    match (elemCache.TryFind elemCode) with
                    | Some elem -> 
                        return elem
                    | None -> 
                        let! elem = compute ()
                        do! modify (fun (cache: ElemCache) -> cache.Add (elemCode, elem))
                        return elem        
                } 

            result

    let evaluateElem : EvaluateElem = 
        fun elemDefinitionCache elemCode ctx ->
            effect {
                let! (elem, _) = StateT.run (computeElem elemDefinitionCache elemCode) Map.empty
                return! ReaderT.run elem ctx
            }
        
    let evaluateElems : EvaluateElems = 
        fun elemDefinitionCache elemCodes ctx ->
            effect {
                let statefulElems = elemCodes |> traverse (computeElem elemDefinitionCache)
                let! (elems, _) = StateT.run statefulElems Map.empty
                let! results = elems |> traverse (fun elem -> ReaderT.run elem ctx)
                let result = results |> sequence
                return result
            }

    //let evaluateElemsMultipleContexts : EvaluateElemsMultipleContexts =
    //    fun elemDefinitionCache elemCodes ctxs ->
    //        effect {
    //            let x = evaluateElems elemDefinitionCache elemCodes 
    //            let! results = ctxs |> traverse (fun ctx -> ReaderEffect.run x ctx)
    //            let result = results |> sequence
    //            return result
    //        }
                  




