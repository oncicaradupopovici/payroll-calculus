namespace PayrollCalculus.Api.Handlers

open PayrollCalclulus.Api
open HandlerUtils
open Giraffe
open PayrollCalculus.Application.Evaluation

module Evaluation =
    let handler : HttpHandler = 
        subRoute "/evaluation" (
            choose [
                POST >=> route  "/evaluateSingleCode"  >=> bindJson<SingleCodeEvaluation.Query> (SingleCodeEvaluation.handler >> (interpret jsonResult))
                POST >=> route  "/evaluateMultipleCodes"  >=> bindJson<MultipleCodesEvaluation.Query> (MultipleCodesEvaluation.handler >> (interpret jsonResult))
            ])