namespace PayrollCalculus.Api.Handlers

open PayrollCalclulus.Api
open PayrollCalculus.PublishedLanguage
open HandlerUtils
open Giraffe

module ElemDefinitions =
    let handler : HttpHandler = 
        subRoute "/elemDefinitions" (
            choose [
                POST >=> route  "/add"  >=> bindJson<AddElemDefinition> publishCommand
            ])