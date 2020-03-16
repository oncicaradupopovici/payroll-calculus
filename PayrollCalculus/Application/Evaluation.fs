namespace PayrollCalculus.Application.Evaluation

open System
open NBB.Core.Effects.FSharp
open PayrollCalculus.Domain.DomainTypes
open PayrollCalculus.Domain.SideEffects
open PayrollCalculus.Domain.DomainImpl

module SingleCodeEvaluation =
    type Query =
        {ElemCode:string; PersonId: Guid; Year: int; Month: int}

    let handler (query: Query) =
           let code = ElemCode query.ElemCode
           let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

           effect {
               let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
               let! result = evaluateElem elemDefinitionCache code ctx

               return result;
           }

module MultipleCodesEvaluation =
    type Query =
        {ElemCodes:string list; PersonId: Guid; Year: int; Month: int}

    let handler (query: Query) =
           let codes =  query.ElemCodes |> List.map ElemCode
           let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

           effect {
               let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
               let! result = evaluateElems elemDefinitionCache codes ctx

               return result;
           }
