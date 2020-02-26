namespace PayrollCalculus

open System
open NBB.Core.Effects.FSharp
open NBB.Core.Effects
open DataStructures

module List =
    let traverseResult f list =
        let pure' = Result.Ok
        let (<*>) fn = Result.bind (fun x-> Result.map (fun f -> f x) fn) 
        let cons head tail = head :: tail  
        let initState = pure' []
        let folder head tail = pure' cons <*> (f head) <*> tail
        List.foldBack folder list initState

    let sequenceResult list = traverseResult id list


module Domain =

    type ElemDefinition = {
        Code: ElemCode
        Type: ElemType
        DataType: Type
    }
    and ElemCode = ElemCode of string
    and ElemType = 
        | Db of DbElemDefinition
        | Formula of FormulaElemDefinition
    and DbElemDefinition = {table:string; column:string}
    and FormulaElemDefinition = {formula:string; deps: string list}

    type ElemDefinitionCache = Map<ElemCode, ElemDefinition>
    module ElemDefinitionCache =
        let findElemDefinition (elemDefinitionCache:ElemDefinitionCache) elemCode = 
            match (elemDefinitionCache.TryFind elemCode) with
                | None -> "could not find definition" |> Result.Error
                | Some elemDefinition -> Result.Ok elemDefinition



    type Elem<'T> = ComputationCtx -> IEffect<Result<'T,string>>
    and ComputationCtx = {
        PersonId: PersonId
        YearMonth: YearMonth
    }
    and YearMonth = {
        Year: int
        Month: int
    }
    and PersonId = PersonId of Guid

    module Elem = 
        let liftFunc (func: obj[] -> obj) (arr: Elem<obj> []) (ctx:ComputationCtx) =
                arr 
                    |> Array.map (fun fn -> fn ctx)
                    |> Array.toList 
                    |> List.sequenceEffect
                    |> Effect.map (List.sequenceResult >> Result.map (List.toArray >> func))

        let flattenResult (elem:Elem<Result<'a,string>>) :Elem<'a> = elem >> Effect.map (Result.bind id)
                    
    type ElemValuesCache = Map<ElemCode, obj>

    type ElemCache = Map<ElemCode,Elem<obj>>

    module Result = 
        let traverseElem (f: 'a-> Elem<'c>) (result:Result<'a,'b>) : Elem<Result<'c, 'b>> = 
            let pure' x = fun ctx -> Effect.pure' x
            let map f elem = elem >> Effect.map f

            match result with
                |Error err -> map Result.Error (pure' err)
                |Ok v -> map Result.Ok (f v)

        let sequenceElem result = traverseElem id result

        let traverseEffect (f: 'a-> IEffect<'c>) (result:Result<'a,'b>) : IEffect<Result<'c, 'b>> = 
            match result with
                |Error err -> Effect.map Result.Error (Effect.pure' err)
                |Ok v -> Effect.map Result.Ok (f v)

        let sequenceEffect result = traverseEffect id result


    module ElemDefinitionRepo = 
        type LoadDefinitionsSideEffect () =
            interface ISideEffect<Map<ElemCode, ElemDefinition>>

        let loadDefinitions = Effect.Of << LoadDefinitionsSideEffect 
    

    module ElemValueRepo = 
        type LoadSideEffect = {
            definition: DbElemDefinition
            ctx: ComputationCtx
        }
        with interface ISideEffect<Result<obj, string>>

        let load definition ctx = Effect.Of {definition=definition; ctx=ctx}

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

        let parseFormula definitions formula = Effect.Of {formula=formula; definitions=definitions}


    

    type ComputeElem  = ElemDefinitionCache -> ElemCode -> Elem<obj>
    let rec computeElem: ComputeElem =
        fun elemDefinitionCache elemCode ->

            let computeFormula {formula=formula} : Elem<obj> =
                (fun ctx -> 
                    formula
                        |> Parser.parseFormula elemDefinitionCache
                        |> Effect.map 
                            (fun {func=func;parameters=parameters} ->
                                let parsedParameters = parameters|> List.map (fun p -> computeElem elemDefinitionCache (ElemCode p)) |> List.toArray
                                let liftedFunc = Elem.liftFunc func
                                liftedFunc parsedParameters
                            )
                        |> Effect.bind (fun fn -> fn ctx)
                )

            elemCode 
            |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
            |> Result.traverseElem
                (fun elemDefinition ->
                    match elemDefinition.Type with
                    | Db(dbElemDefinition) -> ElemValueRepo.load dbElemDefinition
                    | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition
                )
            |> Elem.flattenResult


    type ComputeElem2  = ElemDefinitionCache -> ElemCode -> IEffect<Elem<obj>>
    let rec computeElem2: ComputeElem2 =
        fun elemDefinitionCache elemCode ->

            let computeFormula {formula=formula} : IEffect<Elem<obj>> =
                formula
                    |> Parser.parseFormula elemDefinitionCache
                    |> Effect.bind 
                        (fun {func=func;parameters=parameters} ->
                            parameters
                                |> List.traverseEffect (ElemCode >> computeElem2 elemDefinitionCache)
                                |> Effect.map (List.toArray >> Elem.liftFunc func)
                    )

                
            let matchElemDefinition elemDefinition = 
                match elemDefinition.Type with
                | Db(dbElemDefinition) -> dbElemDefinition |> ElemValueRepo.load |> Effect.pure'
                | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition


            elemCode 
                |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
                |> Result.traverseEffect matchElemDefinition
                |> Effect.map (Result.sequenceElem >> Elem.flattenResult)


    type ComputeElem3  = ElemDefinitionCache -> ElemCode -> StateEffect<ElemCache, Elem<obj>>
    let rec computeElem3: ComputeElem3 =
        fun elemDefinitionCache elemCode -> 

            let computeFormula {formula=formula} :StateEffect<ElemCache, Elem<obj>> =
                fun elemCache -> 
                    formula
                        |> Parser.parseFormula elemDefinitionCache
                        |> Effect.bind
                            (fun {func=func;parameters=parameters} ->
                                let stateEffect = 
                                    parameters
                                        |> List.traverseStateEffect (ElemCode >> computeElem3 elemDefinitionCache)
                                        |> StateEffect.map (List.toArray >> Elem.liftFunc func)

                                StateEffect.run stateEffect elemCache
                            )

                
            let matchElemDefinition elemDefinition = 
                match elemDefinition.Type with
                | Db(dbElemDefinition) -> dbElemDefinition |> ElemValueRepo.load |> StateEffect.pure'
                | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition


            elemCode
                |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
                |> Result.traverseStateEffect matchElemDefinition
                |> StateEffect.map (Result.sequenceElem >> Elem.flattenResult)
                |> StateEffect.bind (fun elem cache -> Effect.pure' (elem, cache.Add(elemCode, elem)))




    type ComputeElem4  = ElemDefinitionCache -> ElemCode -> StateEffect<ElemCache, Elem<obj>>
    let rec computeElem4: ComputeElem4 =
       fun elemDefinitionCache elemCode elemCache -> 
           let computeFormula {formula=formula} :StateEffect<ElemCache, Elem<obj>> =
               fun elemCache -> 
                   formula
                       |> Parser.parseFormula elemDefinitionCache
                       |> Effect.bind
                           (fun {func=func;parameters=parameters} ->
                               let stateEffect = 
                                   parameters
                                       |> List.traverseStateEffect (ElemCode >> computeElem4 elemDefinitionCache)
                                       |> StateEffect.map (List.toArray >> Elem.liftFunc func)

                               StateEffect.run stateEffect elemCache
                           )

               
           let matchElemDefinition elemDefinition = 
               match elemDefinition.Type with
               | Db(dbElemDefinition) -> dbElemDefinition |> ElemValueRepo.load |> StateEffect.pure'
               | Formula(formulaElemDefinition) -> computeFormula formulaElemDefinition


           let compute() = 
               elemCode
               |> ElemDefinitionCache.findElemDefinition elemDefinitionCache
               |> Result.traverseStateEffect matchElemDefinition
               |> StateEffect.map (Result.sequenceElem >> Elem.flattenResult)
               |> StateEffect.bind (fun elem cache -> Effect.pure' (elem, cache.Add(elemCode, elem)))


           match (elemCache.TryFind elemCode) with
               | Some elem -> Effect.pure' (elem, elemCache)
               | None -> StateEffect.run (compute()) elemCache

                        
    type ComputeElem5  = ElemDefinitionCache -> ElemCode -> StateEffect<ElemCache, Elem<obj>>
    let rec computeElem5: ComputeElem4 =
       fun elemDefinitionCache elemCode -> 
           let computeFormula {formula=formula} :StateEffect<ElemCache, Elem<obj>> =
                stateEffect {
                    let! {func=func;parameters=parameters} = formula |> Parser.parseFormula elemDefinitionCache |> StateEffect.lift
                    let! paramElems =  parameters |> List.traverseStateEffect (ElemCode >> computeElem5 elemDefinitionCache)
                    
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
    

    let evaluateElem elemDefinitionCache elemCode ctx =
        effect {
            let! (elem, _) = StateEffect.run (computeElem5 elemDefinitionCache elemCode) Map.empty
            return! Reader.run elem ctx
        }
        
    let evaluateElems elemDefinitionCache elemCodes ctx =
        effect {
            let statefulElems = elemCodes |> List.traverseStateEffect (computeElem5 elemDefinitionCache)
            let! (elems, _) = StateEffect.run statefulElems Map.empty
            //let! results =  elems |> List.traverseEffect (fun elem -> Reader.run elem ctx)
            let! results = ReaderEffect.run (elems |> ReaderEff.List.sequenceReaderEffect) ctx
            return results
        }

    let evaluateElemsMultipleContexts elemDefinitionCache elemCodes ctxs =
        effect {
            let x = evaluateElems elemDefinitionCache elemCodes 
            let! results = ctxs |> List.traverseEffect (fun ctx -> ReaderEffect.run x ctx)
            return results
        }
  
    let loadElemDefinitions() = Effect.pure' Map.empty<ElemCode, ElemDefinition>

    let h elemCode1 elemCode2 ctx = 
        effect{
            let! elemDefinitionCache = loadElemDefinitions()
            let! (elem1,cache) = computeElem4 elemDefinitionCache elemCode1 Map.empty<ElemCode, Elem<obj>>
            let! (elem2,cache) = computeElem4 elemDefinitionCache elemCode2 cache
            let! value = elem2 ctx
            return value
        }

    let h1 elemCode1 elemCode2 ctx = 
      effect{
            let! elemDefinitionCache = loadElemDefinitions()

            let parser = stateEffect {
                let! elem1 = computeElem4 elemDefinitionCache elemCode1
                let! elem2 = computeElem4 elemDefinitionCache elemCode2 

                return elem2
            }

            let! (elem2, _) = StateEffect.run parser Map.empty

            let! value = elem2 ctx
            return value
        }
                
               
    let h2 elemCode1 elemCode2 ctx = 
       stateEffect {
            let! elemDefinitionCache = StateEffect.lift (loadElemDefinitions())

           
            let! elem1 = computeElem4 elemDefinitionCache elemCode1
            let! elem2 = computeElem4 elemDefinitionCache elemCode2 


            let value = elem2 ctx
            return value
        }

    let h3 elemCodes ctxs = 
        effect{
            let! elemDefinitionCache = loadElemDefinitions()

            let parser = elemCodes |> List.traverseStateEffect (computeElem4 elemDefinitionCache)
            let! (elems, _) = StateEffect.run parser Map.empty

            let! values = ctxs |> List.traverseEffect (fun ctx -> elems |> List.traverseEffect (fun elem -> elem ctx))

           return values
        }
                




