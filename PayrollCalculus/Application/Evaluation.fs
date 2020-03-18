namespace PayrollCalculus.Application.Evaluation

open System
open NBB.Core.Effects.FSharp
open PayrollCalculus.Domain

module EvaluateSingleCode =
    type Query =
        {ElemCode:string; PersonId: Guid; Year: int; Month: int}

    let handler (query: Query) =
           let code = ElemCode query.ElemCode
           let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

           effect {
               let! elemDefinitionStore = ElemDefinitionStoreRepo.loadCurrentElemDefinitionStore ()
               let! result = ElemEvaluationService.evaluateElem elemDefinitionStore code ctx

               return result;
           }

module EvaluateMultipleCodes =
    type Query =
        {ElemCodes:string list; PersonId: Guid; Year: int; Month: int}

    let handler (query: Query) =
           let codes =  query.ElemCodes |> List.map ElemCode
           let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

           effect {
               let! elemDefinitionStore = ElemDefinitionStoreRepo.loadCurrentElemDefinitionStore ()
               let! result = ElemEvaluationService.evaluateElems elemDefinitionStore codes ctx

               return result;
           }
