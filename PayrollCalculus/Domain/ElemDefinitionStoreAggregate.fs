namespace PayrollCalculus.Domain

open System
open NBB.Core.Effects
open NBB.Core.Effects.FSharp

[<CustomEquality; NoComparison>]
type ElemDefinitionStore = {
    Id: ElemDefinitionStoreId
    ElemDefinitions: Map<ElemCode, ElemDefinition>
}
with
    override this.Equals(obj) =
        match obj with
        | :? ElemDefinitionStore as eds -> this.Id = eds.Id
        | _ -> false
    override this.GetHashCode() =
        hash this.Id
and ElemDefinitionStoreId = ElemDefinitionStoreId of Guid
and ElemCode = ElemCode of string
and 
    [<CustomEquality; NoComparison>]
    ElemDefinition = {
        Code: ElemCode
        Type: ElemType
        DataType: Type
    }
    with
    override this.Equals(obj) =
        match obj with
        | :? ElemDefinition as ed -> this.Code = ed.Code
        | _ -> false
    override this.GetHashCode() =
        hash this.Code
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

