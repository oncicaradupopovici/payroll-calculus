namespace PayrollCalculus

open System
open NBB.Core.Effects.FSharp
open DataStructures
open FSharpPlus

module DomainTypes =
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

    type Elem<'T> = ComputationCtx -> Effect<Result<'T,string>> //Data.ReaderT<ComputationCtx, Effect<Result<'T,string>>> 
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
        open FSharpPlus.Data
        let liftFunc (func: obj[] -> obj) (arr: Elem<obj> []) (ctx:ComputationCtx) : Effect<Result<obj, string>> =
            arr 
                |> map ((|>) ctx)
                |> Array.toList
                |> List.sequence
                |> map (List.sequence >> map (List.toArray >> func))

        let flattenResult (elem:Elem<Result<'a,string>>) :Elem<'a> = elem >> map join
                    
    type ElemValuesCache = Map<ElemCode, obj>

    type ElemCache = Map<ElemCode, Elem<obj>>

    module Result = 
        let traverseElem (f: 'a-> Elem<'c>) (result:Result<'a,'b>) : Elem<Result<'c, 'b>> = 
            let pure' x = fun ctx -> Effect.pure' x
            let map f elem = elem >> Effect.map f

            match result with
                |Error err -> map Result.Error (pure' err)
                |Ok v -> map Result.Ok (f v)

        let sequenceElem result = traverseElem id result
