namespace PayrollCalculus.Api.Handlers

open PayrollCalclulus.Api
open HandlerUtils
open Giraffe
open PayrollCalculus.PublishedLanguage.Queries
open PayrollCalculus.Application.Evaluation

module Evaluation =
    let handler : HttpHandler = 
        subRoute "/evaluation" (
            choose [
                POST >=> route  "/evaluateSingleCode"  >=> bindJson<EvaluateSingleCode> (handleEvaluateSingleCode >> (interpret jsonResult))
                POST >=> route  "/evaluateMultipleCodes"  >=> bindJson<EvaluateMultipleCodes> (handleEvaluateMultipleCodes >> (interpret jsonResult))
            ])