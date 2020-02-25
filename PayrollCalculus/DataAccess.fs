namespace PayrollCalculus.Data

module DataAccess =
    open System
    open FSharp.Data
    open PayrollCalculus.Domain
    open NBB.Core.Effects.FSharp

    [<Literal>]
    let connectionString = 
        @"YOUR_CONNECTION_STRING"

    type SelectContractCommand = SqlCommandProvider<"
        SELECT Code, [Type], DataType, [Table], [Column], Formula, FormulaDeps
        FROM VW_ElemDefinitions
        " , connectionString>
    
    let loadElemDefinitions() =
        use cmd = new SelectContractCommand(connectionString)

        let results = cmd.Execute ()
        let dictionary =
            results 
            |> Seq.map (
                fun item  -> 
                    let elemCode = ElemCode(item.Code)
                    let elemDefinition =  
                        {
                            Code = elemCode; 
                            Type = 
                                match item.Type with
                                | Some "Formula" -> Formula {formula = item.Formula.Value; deps= item.FormulaDeps.Value.Split(';') |> Array.toList}
                                | Some "Db" -> Db { table = item.Table.Value; column = item.Column.Value}
                                | _ -> raise (Exception("DB configuration errror"))
                            DataType = Type.GetType(item.DataType)
                        } 
                        
                    (elemCode, elemDefinition)
                )
            |> Map.ofSeq

        Effect.pure' dictionary 