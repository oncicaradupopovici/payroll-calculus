namespace PayrollCalculus.Application

module Evaluation =
    open NBB.Core.Effects.FSharp
    open PayrollCalculus.DomainTypes
    open PayrollCalculus.DomainImpl
    open PayrollCalculus.SideEffects

    let evaluate (code: string) (ctx: ComputationCtx) =
        effect {
            let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
            let! result = evaluateElem elemDefinitionCache (ElemCode code) ctx

            return result;
        }

    let evaluateCodes (codes: string list) (ctx: ComputationCtx) =
        effect {
            let! elemDefinitionCache = ElemDefinitionRepo.loadDefinitions ()
            let! result = evaluateElems elemDefinitionCache (codes |> List.map ElemCode) ctx

            return result;
        }