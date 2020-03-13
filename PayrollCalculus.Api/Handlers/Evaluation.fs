namespace PayrollCalculus.Api.Handlers

open System
open NBB.Core.Effects.FSharp
open PayrollCalculus
open PayrollCalclulus.Api

open HandlerUtils
open Domain.DomainTypes
open Giraffe


type Evaluation =
    {
        ElemCode: string;
        PersonId: Guid;
        Year: int;
        Month: int
    }

module Evaluation =
    let private evaluate (evaluation: Evaluation)  =
        effect {
            let! result = 
                Application.Evaluation.evaluate 
                    evaluation.ElemCode
                    {PersonId= (PersonId evaluation.PersonId); YearMonth = {Year= evaluation.Year; Month = evaluation.Month}}

            return 
                match result with
                | Ok value -> json value
                | Error err -> setError err
        }

    let handler : HttpHandler = 
        subRoute "/evaluation" (
            choose [
                POST >=> route  ""  >=> bindJson<Evaluation> (evaluate >> interpret)
            ])