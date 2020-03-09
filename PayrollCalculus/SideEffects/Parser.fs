namespace PayrollCalculus.SideEffects

open PayrollCalculus.DomainTypes
open NBB.Core.Effects.FSharp
open NBB.Core.Effects

module Parser =
    type ParseFormulaSideEffect = {
        formula:string;
        definitions: ElemDefinitionCache;
    }
    with interface ISideEffect<ParseFormulaResult>
    and ParseFormulaResult = {
        func: obj [] -> obj
        parameters: string list
    }

    let parseFormula definitions formula = (Effect.Of {formula=formula; definitions=definitions}) |> Effect.wrap