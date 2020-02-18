namespace PayrollCalculus

open System
open NReco.Linq
open DynamicExpresso
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


    type Elem<'T> = Elem of (ComputationCtx -> IEffect<'T>)
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
        let liftDelegate (deleg: obj[] -> obj) = 
            fun ([<ParamArray>] arr: Elem<obj> array) ->
                Elem (fun ctx ->
                        arr 
                            |> Array.map (fun (Elem fn) -> fn ctx)
                            |> Array.toList 
                            |> Effect.sequenceList 
                            //|> Effect.map (List.toArray >> (fun arr' -> deleg.DynamicInvoke arr'))
                            |> Effect.map (List.toArray >> (fun arr' -> deleg arr'))
                    )

        let flattenEffect<'T> (x:IEffect<Elem<'T>>) = 
            Elem (fun ctx -> Effect.bind x (fun (Elem fn) -> fn ctx))

        let map (f: 'a-> 'b) ((Elem fn) : Elem<'a>) : Elem<'b> = 
            Elem(fn >> Effect.map f)

        let bind (f: 'a-> Elem<'b>) ((Elem fn) : Elem<'a>) : Elem<'b> = 
            Elem(fun ctx -> fn ctx >>= 
                    (fun a -> 
                        let (Elem fn') = f a
                        fn' ctx
                    )
                )

    type ElemValuesCache = Map<ElemCode, obj>


    module ElemValueRepo = 
        type LoadSideEffect = {
            definition: DbElemDefinition
            ctx: ComputationCtx
        }
        with interface ISideEffect<obj>

        let load definition =  Elem(fun ctx -> Effect.Of {definition=definition; ctx=ctx})

    module Parser =
        type ParseFormulaSideEffect = {
            formula:string
        }
        with interface ISideEffect<ParseFormulaResult>
        and ParseFormulaResult = {
            func: Delegate
            parameters: string list
        }

        let parseFormula formula = Effect.Of {formula=formula}

    

    type ParseElemDefinition  = ElemDefinitionCache -> ElemCode -> ParseElemDefinitionResult
    and ParseElemDefinitionResult = Elem<StateResult<ElemCache, obj>>
    and ElemDefinitionCache = Map<ElemCode, ElemDefinition>
    and ElemCache = Map<ElemCode, Elem<obj>>

    let rec parseElemDefinition: ParseElemDefinition =
        fun elemDefinitionCache elemCode ->
            let elemDefinition = elemDefinitionCache.TryFind elemCode

            let parseFormulaResultToElem  (result:Parser.ParseFormulaResult) =
                let liftedDelegate = Elem.liftDelegate result.func

                let interpreter = DynamicExpresso.Interpreter();
                let parameters = interpreter.DetectIdentifiers(formula).UnknownIdentifiers |> Seq.map (fun param -> Parameter(param, typeof<int>)) |> Seq.toArray
                let parseResult = interpreter.Parse(formula, parameters)
                let liftedDelegate = Elem.liftDelegate parseResult.Invoke
                let results = 
                    result.parameters|> Array.map (fun p -> parseElemDefinition elemDefinitionCache (ElemCode p))
                
                let folder =
                    fun (current: Elem<StateResult<ElemCache, obj>>) (acc: Elem<StateResult<ElemCache, obj list>>) ->
                        Elem.bind (StateResult.bind (fun elems -> current |> Elem.map (StateResult.map (fun elem -> elem :: elems))) current
                
                let results' = Array.foldBack folder results (StateResult.retn [])
                
                results' |> StateResult.bind 
                    (fun paramElems -> 
                        let resultElem = paramElems |> List.toArray |> liftedDelegate
                        (fun s -> Result.Ok(resultElem, s.Add (elemCode,resultElem))))


            
            let parseFormula {formula=formula} : ParseElemDefinitionResult =
                let parserResult = 
                    formula 
                        |> Parser.parseFormula
                        //|> Effect.map parseFormulaResultToElem
                        |> Effect.map 
                            (fun result ->
                                let liftedDelegate = Elem.liftDelegate result.func
                                let results = 
                                    result.parameters
                                        |> Array.map (fun p -> parseElemDefinition elemDefinitionCache (ElemCode p))
                            )
                                

                

            
            match elemDefinition with
                | None -> (fun _-> Result.Error "could not find definition")
                | Some elemDefinition -> 
                    match elemDefinition.Type with
                    | Db(dbElemDefinition) -> ElemValueRepo.load dbElemDefinition
                    | Formula(formulaElemDefinition) -> processFormula formula

            


    //type ComputeElem<'T> = ElemCache -> ElemCode -> ComputationCtx -> ElemValuesCache -> 'T * ElemValuesCache
             







