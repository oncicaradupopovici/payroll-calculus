namespace PayrollCalculus

open System
open NBB.Core.Effects.FSharp
open DataStructures
open FSharpPlus
open FSharpPlus.Data

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

    type Elem<'T> = ReaderT<ComputationCtx, Effect<Result<'T,string>>>
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
        let liftFunc (func: obj[] -> obj) (arr: Elem<obj> []) : Elem<obj> =
            arr 
            |> Array.toList
            |> List.sequence
            |> map (List.sequence >> map (List.toArray >> func))

                    
    type ElemValuesCache = Map<ElemCode, obj>

    type ElemCache = Map<ElemCode, Elem<obj>>
