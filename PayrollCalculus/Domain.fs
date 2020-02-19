namespace PayrollCalculus

open System
open NBB.Core.Effects.FSharp
open NBB.Core.Effects

type StateEffect<'s, 't> = 's -> IEffect<'t * 's>
module StateEffect =
    let run (x: StateEffect<'s, 't>) : 's -> IEffect<'t * 's> = 
        x 
    let map (f: 't->'u) (m : StateEffect<'s, 't>) : StateEffect<'s,'u> = 
        fun s -> Effect.map (fun (a, s') -> (f a, s')) (run m s)
    let bind (f: 't-> StateEffect<'s, 'u>) (m : StateEffect<'s, 't>) : StateEffect<'s, 'u> = 
        fun s -> Effect.bind (fun (a, s') -> run (f a) s') (run m s)
    let apply (f: StateEffect<'s, ('t -> 'u)>) (m: StateEffect<'s, 't>) : StateEffect<'s, 'u> = 
        fun s -> Effect.bind (fun (g, s') -> Effect.map (fun (a: 't, s'': 's) -> ((g a), s'')) (run m s')) (f s)

    let pure' x = fun s -> Effect.pure' (x, s)


type State<'s, 't> = 's -> 't * 's
module State =
    let run (x: State<'s, 't>) : 's -> 't * 's = 
        x 
    let map (f: 't->'u) (m : State<'s, 't>) : State<'s,'u> = 
        fun s -> let (a, s') = run m s in (f a, s')
    let bind (f: 't-> State<'s, 'u>) (m : State<'s, 't>) : State<'s, 'u> = 
        fun s -> let (a, s') = run m s in run (f a) s'
    let apply (f: State<'s, ('t -> 'u)>) (m: State<'s, 't>) : State<'s, 'u> = 
        fun s -> let (f, s') = run f s in let (a, s'') = run m s' in (f a, s'')

    let get : State<'s, 's> = 
        fun s -> (s, s)   
    let put (x: 's) : State<'s, unit> = 
        fun _ -> ((), x)

    let pure' x = fun s -> (x, s)

module List =
    let traverseResult f list =
        let pure' = Result.Ok
        let (<*>) fn = Result.bind (fun x-> Result.map (fun f -> f x) fn) 
        let cons head tail = head :: tail  
        let initState = pure' []
        let folder head tail = pure' cons <*> (f head) <*> tail
        List.foldBack folder list initState

    let sequenceResult list = traverseResult id list

    let traverseState f list =
        let pure' = State.pure'
        let (<*>) = State.apply
        let cons head tail = head :: tail  
        let initState = pure' []
        let folder head tail = pure' cons <*> (f head) <*> tail
        List.foldBack folder list initState

    let sequenceState list = traverseState id list
   
    let traverseStateEffect f list =
        let pure' = StateEffect.pure'
        let (<*>) = StateEffect.apply
        let cons head tail = head :: tail  
        let initState = pure' []
        let folder head tail = pure' cons <*> (f head) <*> tail
        List.foldBack folder list initState

    let sequenceStateEffect list = traverseStateEffect id list

module Domain =

    type ElemDefinition = {
        Code: ElemCode
        Type: ElemType
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

        let traverseState (f: 'a-> State<'s, 'b>) (result:Result<'a,'e>) : State<'s, Result<'b, 'e>> = 
            match result with
                |Error err -> State.map Result.Error (State.pure' err)
                |Ok v -> State.map Result.Ok (f v)

        let sequenceState result = traverseState id result

        let traverseStateEffect (f: 'a-> StateEffect<'s, 'b>) (result:Result<'a,'e>) : StateEffect<'s, Result<'b, 'e>> = 
            match result with
                |Error err -> StateEffect.map Result.Error (StateEffect.pure' err)
                |Ok v -> StateEffect.map Result.Ok (f v)

        let sequenceStateEffect result = traverseStateEffect id result


    module ElemValueRepo = 
        type LoadSideEffect = {
            definition: DbElemDefinition
            ctx: ComputationCtx
        }
        with interface ISideEffect<Result<obj, string>>

        let load definition ctx = Effect.Of {definition=definition; ctx=ctx}

    module Parser =
        type ParseFormulaSideEffect = {
            formula:string
        }
        with interface ISideEffect<ParseFormulaResult>
        and ParseFormulaResult = {
            func: obj [] -> obj
            parameters: string list
        }

        let parseFormula formula = Effect.Of {formula=formula}


    

    type ComputeElem  = ElemDefinitionCache -> ElemCode -> Elem<obj>
    let rec computeElem: ComputeElem =
        fun elemDefinitionCache elemCode ->

            let computeFormula {formula=formula} : Elem<obj> =
                (fun ctx -> 
                    formula
                        |> Parser.parseFormula
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
                    |> Parser.parseFormula
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
                        |> Parser.parseFormula
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
                       |> Parser.parseFormula
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

    
    
    
    
    let loadElemDefinitions() = Effect.pure' Map.empty<ElemCode, ElemDefinition>

    let h elemCode1 elemCode2 ctx = 
        effect{
            let! elemDefinitionCache = loadElemDefinitions()
            let! (elem1,cache) = computeElem4 elemDefinitionCache elemCode1 Map.empty<ElemCode, Elem<obj>>
            let! (elem2,cache) = computeElem4 elemDefinitionCache elemCode2 cache
            let! value = elem2 ctx
            return value
        }
                
               
             







