namespace PayrollCalculus

open System
open NReco.Linq
open System.Linq.Expressions
open NBB.Core.Effects.FSharp
open NBB.Core.Effects

module Effect = 
    let sequenceList list =
        // define the applicative functions
        let apply (func: IEffect<'a->'b>) eff = Effect.Apply(Effect.map (fun fn -> Func<'a,'b>(fn)) func, eff)
        let (<*>) = apply
        //let retn = tupleReturn 
    
        // define a "cons" function
        let cons head tail = head :: tail
    
        // right fold over the list
        let initState = Effect.pureEffect []
        let folder head tail = Effect.pureEffect cons <*> head <*> tail
    
        List.foldBack folder list initState


//type StateResult<'s, 't> = StateResult of ('s -> Result<'t * 's, string>)
//module StateResult =
//    let run (StateResult x) : 's -> Result<'t * 's, string> = 
//        x 
//    let map (f: 't->'u) (m : StateResult<'s, 't>) : StateResult<'s,'u> = 
//        StateResult (fun s -> Result.map (fun (a, s') -> (f a, s')) (run m s)) 
//    let bind (f: 't-> StateResult<'s, 'u>) (m : StateResult<'s, 't>) : StateResult<'s, 'u> = 
//        StateResult (fun s -> Result.bind (fun (a, s') -> run (f a) s') (run m s))
//    let apply (StateResult f: StateResult<'s, ('t -> 'u)>) (m: StateResult<'s, 't>) : StateResult<'s, 'u> = 
//        StateResult (fun s -> Result.bind (fun (g, s') -> Result.map (fun (a: 't, s'': 's) -> ((g a), s'')) (run m s')) (f s)) 


type StateResult<'s, 't> = 's -> Result<'t * 's, string>
module StateResult =
    let run (x: StateResult<'s, 't>) : 's -> Result<'t * 's, string> = 
        x 
    let map (f: 't->'u) (m : StateResult<'s, 't>) : StateResult<'s,'u> = 
        fun s -> Result.map (fun (a, s') -> (f a, s')) (run m s)
    let bind (f: 't-> StateResult<'s, 'u>) (m : StateResult<'s, 't>) : StateResult<'s, 'u> = 
        fun s -> Result.bind (fun (a, s') -> run (f a) s') (run m s)
    let apply (f: StateResult<'s, ('t -> 'u)>) (m: StateResult<'s, 't>) : StateResult<'s, 'u> = 
        fun s -> Result.bind (fun (g, s') -> Result.map (fun (a: 't, s'': 's) -> ((g a), s'')) (run m s')) (f s)

    let retn x = fun s -> Result.Ok (x, s)


module Domain = 
    type ElemCode = ElemCode of string

    type ElemType = 
        | DataAccess of table:string * column:string
        | Formula of formula:string * deps: string list

    type ElemDefinition = {
        Code: ElemCode
        Type: ElemType
    }
    
    type PersonId = PersonId of Guid

    type YearMonth = {
        Year: int
        Month: int
    }

    type ComputationCtx = {
        PersonId: PersonId
        YearMonth: YearMonth
    }

    type Elem<'T> = Elem of (ComputationCtx -> IEffect<'T>)

    module Elem = 
        let liftDelegate (deleg: Delegate) = 
            fun ([<ParamArray>] arr: Elem<obj> array) ->
                Elem (fun ctx ->
                        arr 
                            |> Array.map (fun (Elem fn) -> fn ctx)
                            |> Array.toList 
                            |> Effect.sequenceList 
                            |> Effect.map (List.toArray >> (fun arr' -> deleg.DynamicInvoke arr'))
                    )
                


                        
                
            

    type ElemDefinitionCache = Map<ElemCode, ElemDefinition>

    type ElemCache = Map<ElemCode, Elem<obj>>

    type ElemValuesCache = Map<ElemCode, obj>

    type ProcessElemResult = StateResult<ElemCache, Elem<obj>>

    type ProcessElem  = ElemDefinitionCache -> ElemCode -> ProcessElemResult



     
    


    let rec processElem: ProcessElem = 
        fun elemDefinitionCache elemCode ->
            let elemDefinition = elemDefinitionCache.TryFind elemCode

            let processDataAccess table column : ProcessElemResult = 
                let elem = Elem (fun computationCtx -> Effect.pureEffect (Object()))
                fun (s:ElemCache) -> Result.Ok(elem, s.Add (elemCode,elem))
            

            let processFormula formula : ProcessElemResult =
                let parser = LambdaParser()
                let expression = parser.Parse formula
                let parameters = LambdaParser.GetExpressionParameters expression
                let lambdaExpression = Expression.Lambda(expression, parameters)
                let compiled = lambdaExpression.Compile();
                let liftedDelegate = Elem.liftDelegate compiled

                

                let results = 
                    parameters|> Array.map (fun p -> processElem elemDefinitionCache (ElemCode p.Name))

                let folder =
                    fun (current: ProcessElemResult) (acc: StateResult<ElemCache, Elem<obj> list>) ->
                        acc |> StateResult.bind (fun elems -> current |> StateResult.map (fun elem -> elem :: elems))

                let results' = Array.foldBack folder results (StateResult.retn [])

                results' |> StateResult.bind 
                    (fun paramElems -> 
                        let resultElem = paramElems |> List.toArray |> liftedDelegate
                        (fun s -> Result.Ok(resultElem, s.Add (elemCode,resultElem))))

            
            match elemDefinition with
                | None -> (fun _-> Result.Error "could not find definition")
                | Some elemDefinition -> 
                    match elemDefinition.Type with
                    | DataAccess(table,column) -> processDataAccess table column
                    | Formula(formula, _) -> processFormula formula

            


    //type ComputeElem<'T> = ElemCache -> ElemCode -> ComputationCtx -> ElemValuesCache -> 'T * ElemValuesCache


    





