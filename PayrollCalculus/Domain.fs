namespace PayrollCalculus

open System
open NBB.Core.Effects.FSharp
open NBB.Core.Effects

//type StateResult<'s, 't> = 's -> Result<'t * 's, string>
//module StateResult =
//    let run (x: StateResult<'s, 't>) : 's -> Result<'t * 's, string> = 
//        x 
//    let map (f: 't->'u) (m : StateResult<'s, 't>) : StateResult<'s,'u> = 
//        fun s -> Result.map (fun (a, s') -> (f a, s')) (run m s)
//    let bind (f: 't-> StateResult<'s, 'u>) (m : StateResult<'s, 't>) : StateResult<'s, 'u> = 
//        fun s -> Result.bind (fun (a, s') -> run (f a) s') (run m s)
//    let apply (f: StateResult<'s, ('t -> 'u)>) (m: StateResult<'s, 't>) : StateResult<'s, 'u> = 
//        fun s -> Result.bind (fun (g, s') -> Result.map (fun (a: 't, s'': 's) -> ((g a), s'')) (run m s')) (f s)

//    let retn x = fun s -> Result.Ok (x, s)


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

    module Result = 
        let traverseElem (f: 'a-> Elem<'c>) (result:Result<'a,'b>) : Elem<Result<'c, 'b>> = 
            let pure' x = fun ctx -> Effect.pure' x
            let map f elem = elem >> Effect.map f

            match result with
                |Error err -> map Result.Error (pure' err)
                |Ok v -> map Result.Ok (f v)


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
                
               
             







