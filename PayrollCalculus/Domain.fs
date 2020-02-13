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

    type ProcessElem  = ElemDefinitionCache -> ElemCode -> ElemCache -> Result<(Elem<obj> * ElemCache), string>



     
    


    let rec processElem: ProcessElem = 
        fun elemDefinitionCache elemCode elemCache ->
            let elemDefinition = elemDefinitionCache.TryFind elemCode

            let processDataAccess table column = Elem (fun computationCtx -> Effect.pureEffect (Object()))

            let processFormula formula =
                let parser = LambdaParser()
                let expression = parser.Parse formula
                let parameters = LambdaParser.GetExpressionParameters expression
                let lambdaExpression = Expression.Lambda(expression, parameters)
                let compiled = lambdaExpression.Compile();
                let liftedDelegate = Elem.liftDelegate compiled

                parameters |> Array.map (fun p -> processElem elemDefinitionCache p.Name

 
            match elemDefinition.Type with
                | DataAccess(table,column) -> processDataAccess table column
                | Formula(formula, _) -> processFormula formula


    //type ComputeElem<'T> = ElemCache -> ElemCode -> ComputationCtx -> ElemValuesCache -> 'T * ElemValuesCache


    





