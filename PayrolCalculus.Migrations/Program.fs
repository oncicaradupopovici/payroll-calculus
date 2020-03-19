namespace PayrollCalculus.Migrations

module Migrator =
    open System
    open System.Linq
    open DbUp
    open System.Reflection
    open Microsoft.Extensions.Configuration
    open System.IO

    let getConfigConnectionString () =
        let configuration =
            let configurationBuilder = 
                ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json")
                    .AddUserSecrets(Assembly.GetExecutingAssembly())
            configurationBuilder.Build()

        configuration.GetConnectionString "PayrollCalculus"

    let upgradeDatabase drop connectionString =
        if (drop) then
               DropDatabase.For.SqlDatabase(connectionString);
               EnsureDatabase.For.SqlDatabase(connectionString);
       

        let upgrader = DeployChanges.To
                        .SqlDatabase(connectionString, null) 
                        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                        .LogToConsole()
                        .Build()
         
       
        let result = upgrader.PerformUpgrade()
        match result.Successful with
        | true -> printfn "Success!"
        | false-> printfn "Error! \n %A" (result.Error.Message)


    [<EntryPoint>]
    let main argv =
    
        let (drop, connectionString) =
            match argv with
            | [| |]                             -> (false, getConfigConnectionString ())
            | [| "--drop"; connectionString |]  -> (true, connectionString)
            | [| connectionString |]            -> (false, connectionString)
            | _ -> failwith "Invalid args. Ussage: PayrollCalculus.Migrations [--drop] [connectionString]"

        upgradeDatabase drop connectionString
   
        0 // return an integer exit code
