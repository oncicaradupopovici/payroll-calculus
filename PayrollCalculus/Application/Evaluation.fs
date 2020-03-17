namespace PayrollCalculus.Application.Evaluation

open System
open NBB.Core.Effects.FSharp
open PayrollCalculus.Domain

module SingleCodeEvaluation =
    type Query =
        {ElemCode:string; PersonId: Guid; Year: int; Month: int}

    let handler (query: Query) =
           let code = ElemCode query.ElemCode
           let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

           effect {
               let! elemDefinitionStore = ElemDefinitionStoreRepo.loadCurrentElemDefinitionStore ()
               let! result = ElemComputingService.evaluateElem elemDefinitionStore code ctx

               return result;
           }

module MultipleCodesEvaluation =
    type Query =
        {ElemCodes:string list; PersonId: Guid; Year: int; Month: int}

    let handler (query: Query) =
           let codes =  query.ElemCodes |> List.map ElemCode
           let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

           effect {
               let! elemDefinitionStore = ElemDefinitionStoreRepo.loadCurrentElemDefinitionStore ()
               let! result = ElemComputingService.evaluateElems elemDefinitionStore codes ctx

               return result;
           }
