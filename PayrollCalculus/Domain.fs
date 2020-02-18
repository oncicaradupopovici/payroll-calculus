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


    type Elem<'T> = ComputationCtx -> IEffect<'T>
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
        let liftFunc (func: obj[] -> obj) (arr: (ComputationCtx -> IEffect<Result<obj, string>>) []) (ctx:ComputationCtx) =
                arr 
                    |> Array.map (fun fn -> fn ctx)
                    |> Array.toList 
                    |> List.sequenceEffect
                    |> Effect.map (List.sequenceResult >> Result.map (List.toArray >> func))
                    
    type ElemValuesCache = Map<ElemCode, obj>


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

    

    type ParseElemDefinition  = ElemDefinitionCache -> ElemCode -> ComputationCtx -> IEffect<Result<obj,string>>
    and ElemDefinitionCache = Map<ElemCode, ElemDefinition>

    let rec parseElemDefinition: ParseElemDefinition =
        fun elemDefinitionCache elemCode ->

            let parseFormula {formula=formula} : ComputationCtx -> IEffect<Result<obj,string>> =
                (fun ctx -> 
                    formula
                        |> Parser.parseFormula
                        |> Effect.map 
                            (fun {func=func;parameters=parameters} ->
                                let parsedParameters = parameters|> List.map (fun p -> parseElemDefinition elemDefinitionCache (ElemCode p)) |> List.toArray
                                let liftedFunc = Elem.liftFunc func
                                liftedFunc parsedParameters
                            )
                        |> Effect.bind (fun fn -> fn ctx)
                )

            let elemDefinition = elemDefinitionCache.TryFind elemCode
            match elemDefinition with
                | None -> (fun _-> "could not find definition" |> Result.Error |> Effect.pure')
                | Some elemDefinition -> 
                    match elemDefinition.Type with
                    | Db(dbElemDefinition) -> ElemValueRepo.load dbElemDefinition
                    | Formula(formulaElemDefinition) -> parseFormula formulaElemDefinition

            


    //type ComputeElem<'T> = ElemCache -> ElemCode -> ComputationCtx -> ElemValuesCache -> 'T * ElemValuesCache
             







