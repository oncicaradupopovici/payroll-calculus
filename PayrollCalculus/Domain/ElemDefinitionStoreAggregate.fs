namespace PayrollCalculus.Domain

open System
open NBB.Core.Effects
open NBB.Core.Effects.FSharp

type ElemDefinitionStore = {
    Id: ElemDefinitionStoreId
    ElemDefinitions: Map<ElemCode, ElemDefinition>
}
and ElemDefinitionStoreId = ElemDefinitionStoreId of Guid
and ElemCode = ElemCode of string
and ElemDefinition = {
    Code: ElemCode
    Type: ElemType
    DataType: Type
}
and ElemType = 
    | Db of DbElemDefinition
    | Formula of FormulaElemDefinition
and DbElemDefinition = {table:string; column:string}
and FormulaElemDefinition = {formula:string; deps: string list}


module ElemDefinitionStore =
    let create (elemDefs: ElemDefinition seq) = 
        let elemDefinitions = elemDefs |> Seq.map (fun elemDef -> (elemDef.Code, elemDef)) |> Map.ofSeq
        in {Id = Guid.Empty |> ElemDefinitionStoreId; ElemDefinitions = elemDefinitions}

    let findElemDefinition ({ElemDefinitions=elemDefinitions}) elemCode = 
        match (elemDefinitions.TryFind elemCode) with
            | None -> "could not find definition" |> Result.Error
            | Some elemDefinition -> Result.Ok elemDefinition


module ElemDefinitionStoreRepo =
    type LoadCurrentDefinitionStoreSideEffect () =
        interface ISideEffect<ElemDefinitionStore>
    
    let loadCurrentElemDefinitionStore () = Effect.Of (LoadCurrentDefinitionStoreSideEffect ()) |> Effect.wrap

