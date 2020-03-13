namespace PayrollCalculus.Application

open PayrollCalculus.PublishedLanguage.Queries

module Evaluation =
    open NBB.Core.Effects.FSharp
    open PayrollCalculus.Domain.DomainTypes
    open PayrollCalculus.Domain.DomainImpl
    open PayrollCalculus.Domain.SideEffects
       
    let handleEvaluateSingleCode (query: EvaluateSingleCode) =
        let code = ElemCode query.ElemCode
        let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

        effect {
            let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
            let! result = evaluateElem elemDefinitionCache code ctx

            return result;
        }

    let handleEvaluateMultipleCodes (query: EvaluateMultipleCodes) =
        let codes = query.ElemCodes |> List.map ElemCode
        let ctx = {PersonId = PersonId(query.PersonId); YearMonth = {Year = query.Year; Month = query.Month}}

        effect {
            let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
            let! result = evaluateElems elemDefinitionCache codes ctx

            return result;
        }

    //let handle  = function
    //    | EvaluateSingleCode q -> handleEvaluateSingleCode q
    //    | EvaluateMultipleCodes q -> handleEvaluateMultipleCodes q
