namespace PayrollCalculus.Domain

open System

type ElemDefinitionCache = Map<ElemCode, ElemDefinition>
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


module ElemDefinitionCache =
    let findElemDefinition (elemDefinitionCache:ElemDefinitionCache) elemCode = 
        match (elemDefinitionCache.TryFind elemCode) with
            | None -> "could not find definition" |> Result.Error
            | Some elemDefinition -> Result.Ok elemDefinition

