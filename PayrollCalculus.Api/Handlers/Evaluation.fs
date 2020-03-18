namespace PayrollCalculus.Api.Handlers

open PayrollCalclulus.Api
open HandlerUtils
open Giraffe
open PayrollCalculus.Application.Evaluation

module Evaluation =
    let handler : HttpHandler = 
        subRoute "/evaluation" (
            choose [
                POST >=> route  "/evaluateSingleCode"  >=> bindJson<EvaluateSingleCode.Query> (EvaluateSingleCode.handler >> (interpret jsonResult))
                POST >=> route  "/evaluateMultipleCodes"  >=> bindJson<EvaluateMultipleCodes.Query> (EvaluateMultipleCodes.handler >> (interpret jsonResult))
            ])