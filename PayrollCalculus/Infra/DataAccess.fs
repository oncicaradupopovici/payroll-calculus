﻿namespace PayrollCalculus.Infra

open System
open FSharp.Data
open PayrollCalculus.Domain

module DataAccess =

    module ElemDefinitionStoreRepo =
        type SelectContractCommand = SqlCommandProvider<"SELECT * FROM VW_ElemDefinitions" , "name=PayrollCalculus", DataDirectory = "Infra\\SQL">
    
        let loadCurrent (connectionString: string) (_: ElemDefinitionStoreRepo.LoadCurrentElemDefinitionStoreSideEffect)  =
            use cmd = new SelectContractCommand(connectionString)

            let results = cmd.Execute ()
            in results |> Seq.map (
                fun item  -> 
                    let elemCode = ElemCode(item.Code)
                    in {
                        Code = elemCode;
                        Type = 
                            match item.Type with
                            | Some "Formula" -> Formula {formula = item.Formula.Value; deps= item.FormulaDeps.Value.Split(';') |> Array.toList}
                            | Some "Db" -> Db { Table = item.Table.Value; Column = item.Column.Value}
                            | _ -> failwith "DB configuration errror"
                        DataType = Type.GetType(item.DataType)
                    }
                )
            |> ElemDefinitionStore.create

        let save (_connectionString: string) (_sideEffect: ElemDefinitionStoreRepo.SaveElemDefinitionStoreSideEffect) = ()

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
            let {Table=table; Column=column} = definition
            let (PersonId personId) = ctx.PersonId
            let result = 
                executeCommand
                    (sprintf "SELECT TOP 1 %s FROM %s WHERE PersonId=@PersonId AND Month=@Month AND Year=@Year" column table)
                    ["@PersonId", box personId; "@Month", box ctx.YearMonth.Month; "@Year", box ctx.YearMonth.Year] 

            Result.Ok result 
