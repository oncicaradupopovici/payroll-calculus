// Learn more about F# at http://fsharp.org

open System
open System.Linq
open DbUp
open System.Reflection

[<EntryPoint>]
let main argv =
    let connectionString =
           match argv.FirstOrDefault() with
           | null -> "Server=(localdb)\\MSSQLLocalDB; Database=test8; Trusted_connection=true"
           | _ as conStr -> conStr

    //DropDatabase.For.SqlDatabase(connectionString);
    //EnsureDatabase.For.SqlDatabase(connectionString);

    let upgrader = DeployChanges.To
                    .SqlDatabase(connectionString, null) 
                    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                    .LogToConsole()
                    .Build()
      
    
    let result = upgrader.PerformUpgrade()
    match result.Successful with
    | true -> printfn "Success!"
    | false-> printfn "Error! \n %A" (result.Error.Message)

    0 // return an integer exit code
