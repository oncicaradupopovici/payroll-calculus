namespace PayrollCalculus.Infra

open System
open FSharp.Data
open PayrollCalculus.Domain

module DataAccess =

    module ElemDefinitionStoreRepo =
        
        type SelectElemDefinitionsCommand = SqlCommandProvider<"SELECT * FROM VW_ElemDefinitions" , "name=PayrollCalculus">
        type PayrollCalculusDb = SqlProgrammabilityProvider<"name=PayrollCalculus">

        let loadCurrent (connectionString: string) (_: ElemDefinitionStoreRepo.LoadCurrentElemDefinitionStoreSideEffect)  =
            use cmd = new SelectElemDefinitionsCommand(connectionString)

            let results = cmd.Execute ()
            in results |> Seq.map (
                fun item  -> 
                    let elemCode = ElemCode(item.Code)
                    in {
                        Code = elemCode
                        Type = 
                            match item.Type with
                            | Some "Formula" -> Formula {Formula = item.Formula.Value; Deps= item.FormulaDeps.Value.Split(';') |> Array.toList}
                            | Some "Db" -> Db { TableName = item.TableName.Value; ColumnName = item.ColumnName.Value}
                            | _ -> failwith "DB configuration errror"
                        DataType = Type.GetType(item.DataType)
                    }
                )
            |> ElemDefinitionStore.create

        let save (connectionString: string) (sideEffect: ElemDefinitionStoreRepo.SaveElemDefinitionStoreSideEffect) =
            use conn = new System.Data.SqlClient.SqlConnection (connectionString)
            conn.Open()

            let insertElemDefinition elemDefinition = 
                let (ElemCode code) = elemDefinition.Code
                let dataType = elemDefinition.DataType.FullName
                let elemDefinitions = new PayrollCalculusDb.dbo.Tables.ElemDefinition()
                let newRow = elemDefinitions.NewRow(code, dataType)
                elemDefinitions.Rows.Add newRow
                elemDefinitions.Update(conn) |> ignore
                newRow.ElemDefinitionId

            let insertDbElemDefinition (dbElemDefinition: DbElemDefinition) elemDefinitionId =
                let dbElemDefinitions = new PayrollCalculusDb.dbo.Tables.DbElemDefinition()
                dbElemDefinitions.AddRow(dbElemDefinition.TableName, dbElemDefinition.ColumnName, elemDefinitionId)
                dbElemDefinitions.Update(conn) |> ignore

            let processEvent event = 
                match event with
                | ElemDefinitionAdded (_elemDefinitionStoreId, elemDefinition) ->
                    let elemDefinitionId = insertElemDefinition elemDefinition
                    match elemDefinition.Type with
                        |Db dbElemDefinition -> insertDbElemDefinition dbElemDefinition elemDefinitionId
                        |Formula _formulaElemDefinition -> ()
                | ElemDefinitionStoreCreated _ -> ()

            let (ElemDefinitionStoreRepo.SaveElemDefinitionStoreSideEffect (_store, events)) = sideEffect
            events |> List.map processEvent |> ignore


    module DbElemValue =
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

        let loadValue (connectionString: string) ({Definition=definition; Ctx=ctx} : DbElemValue.LoadSideEffect) : Result<obj, string> =
            let executeCommand  = SqlCommandHelper.executeScalar connectionString   
            let {TableName=table; ColumnName=column} = definition
            let (PersonId personId) = ctx.PersonId
            let result = 
                executeCommand
                    (sprintf "SELECT TOP 1 %s FROM %s WHERE PersonId=@PersonId AND Month=@Month AND Year=@Year" column table)
                    ["@PersonId", box personId; "@Month", box ctx.YearMonth.Month; "@Year", box ctx.YearMonth.Year] 

            Result.Ok result 
