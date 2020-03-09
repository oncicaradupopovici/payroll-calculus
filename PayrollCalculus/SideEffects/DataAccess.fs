namespace PayrollCalculus.SideEffects

open PayrollCalculus.DomainTypes
open NBB.Core.Effects.FSharp
open NBB.Core.Effects

module ElemDefinitionRepo = 
    type LoadDefinitionsSideEffect () =
        interface ISideEffect<Map<ElemCode, ElemDefinition>>

    let loadDefinitions () = (Effect.Of  (LoadDefinitionsSideEffect ())) |> Effect.wrap


module ElemValueRepo = 
    type LoadSideEffect = {
        definition: DbElemDefinition
        ctx: ComputationCtx
    }
    with interface ISideEffect<Result<obj, string>>

    let load definition ctx = (Effect.Of {definition=definition; ctx=ctx}) |> Effect.wrap
