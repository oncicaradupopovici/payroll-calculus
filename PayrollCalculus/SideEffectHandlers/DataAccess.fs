namespace PayrollCalculus.SideEffectHandlers

module DataAccess =
    open System
    open FSharp.Data
    open PayrollCalculus.Domain  



    module ElemDefinitionRepo =
        type SelectContractCommand = SqlCommandProvider<"SELECT * FROM VW_ElemDefinitions" , "name=PayrollCalculus", DataDirectory = "SQL">
    
        let handleLoadDefinitions (connectionString: string) (_: ElemDefinitionRepo.LoadDefinitionsSideEffect)  =
            use cmd = new SelectContractCommand(connectionString)

            let results = cmd.Execute ()
            let dictionary =
                results 
                |> Seq.map (
                    fun item  -> 
                        let elemCode = ElemCode(item.Code)
                        let elemDefinition = {
                                Code = elemCode;
                                Type = 
                                    match item.Type with
                                    | Some "Formula" -> Formula {formula = item.Formula.Value; deps= item.FormulaDeps.Value.Split(';') |> Array.toList}
                                    | Some "Db" -> Db { table = item.Table.Value; column = item.Column.Value}
                                    | _ -> failwith "DB configuration errror"
                                DataType = Type.GetType(item.DataType)
                            } 
                        (elemCode, elemDefinition)
                    )
                |> Map.ofSeq

            dictionary 

    module ElemValueRepo =
        open System.Data.SqlClient

        module SqlCommandHelper =
            let private exec connection bind (query: string) (parametres: (string * obj) list)  = 
                use conn = new SqlConnection (connection)
                conn.Open()
                use cmd = new SqlCommand (query, conn)
                do parametres |> List.iter (fun (key, value) -> cmd.Parameters.AddWithValue(key, value) |> ignore) 
                bind cmd

            let execute connection = exec connection <| fun c -> c.ExecuteNonQuery() |> ignore
            let executeScalar connection = exec connection <| fun c -> c.ExecuteScalar()

        let handleLoadValue (connectionString: string) ({definition=definition; ctx=ctx} : ElemValueRepo.LoadSideEffect) : Result<obj, string> =
            let executeCommand  = SqlCommandHelper.executeScalar connectionString   
            let {table=table; column=column} = definition
            let (PersonId personId) = ctx.PersonId
            let result = 
                executeCommand
                    (sprintf "SELECT TOP 1 %s FROM %s WHERE PersonId=@PersonId AND Month=@Month AND Year=@Year" column table)
                    ["@PersonId", box personId; "@Month", box ctx.YearMonth.Month; "@Year", box ctx.YearMonth.Year] 

            Result.Ok result 
