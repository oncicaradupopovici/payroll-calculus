namespace PayrollCalculus.Domain

open System
open NBB.Core.Effects.FSharp
open NBB.Core.FSharp.Data
open NBB.Core.Effects.FSharp.Data.ReaderEffect
open NBB.Core.Effects.FSharp.Data.ReaderStateEffect
open PayrollCalculus.DataStructures

//open FSharpPlus

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

    type ElemValueCache = Map<ElemCode, Result<obj, string>>

    type Elem<'T> = ReaderStateEffect<ComputationCtx, ElemValueCache, Result<'T, string>>
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
        let liftFunc (func: obj[] -> obj) (arr: Elem<obj> []) : Elem<obj> =
            arr 
                |> Array.toList
                |> List.sequenceReaderStateEffect
                |> ReaderStateEffect.map (List.sequenceResult >> Result.map (List.toArray >> func))
        
        let flattenResult (elemResult: Result<Elem<obj>, string>) : Elem<obj> = 
            elemResult
                |> Result.sequenceReaderStateEffect 
                |> ReaderStateEffect.map (Result.join)
                    
    type ElemValuesCache = Map<ElemCode, obj>

    type ElemCache = Map<ElemCode, Elem<obj>>
