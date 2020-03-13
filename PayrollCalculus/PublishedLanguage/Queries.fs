namespace PayrollCalculus.PublishedLanguage

module Queries =
    open System
    open NBB.Application.DataContracts
    open NBB.Core.Effects.FSharp

    type EvaluateSingleCode(elemCode, personId, year, month, ?metadata) =
        inherit Query<Effect<Result<obj, string>>> (metadata |> Option.defaultValue null)     
        member _.ElemCode with get() : string = elemCode
        member _.PersonId with get() : Guid = personId
        member _.Year with get() : int = year
        member _.Month with get(): int = month

    type EvaluateMultipleCodes(elemCodes , personId, year, month, ?metadata) =
        inherit Query<Effect<Result<obj, string>>> (metadata |> Option.defaultValue null)     
        member _.ElemCodes with get() : string list = elemCodes
        member _.PersonId with get() : Guid = personId
        member _.Year with get() : int = year
        member _.Month with get(): int = month
    

    //type Query = 
    //    | EvaluateSingleCode of EvaluateSingleCode
    //    | EvaluateMultipleCodes of EvaluateMultipleCodes