module IntegrationTests

open System
open System.IO
open FsUnit.Xunit
open Microsoft.Extensions.Configuration
open NBB.Core.Effects
open NBB.Core.Effects.FSharp
open Xunit
open DbUp
open PayrollCalculus.Infra
open SideEffectMediator
open DataAccess
open PayrollCalculus.Application.Evaluation
open PayrollCalculus.Migrations
open System.Threading.Tasks

let configuration =
    let configurationBuilder = 
        ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
    configurationBuilder.Build()

let payrollConnString = configuration.GetConnectionString "PayrollCalculus"
let hcmConnectionString = configuration.GetConnectionString "Hcm"

type MockSideEffectMediator() =
    interface ISideEffectMediator with
        member _.Run<'TOutput>(sideEffect, cancellationToken) : Task<'TOutput> = 
            match sideEffect with
            | :? Thunk.SideEffect<'TOutput> as tse -> tse.ImpureFn.Invoke(cancellationToken)
            | _ -> failwith "Handler not found"


[<Fact>]
let ``It shoud evaluate formula with params (integration)`` () =

    // Arrange
    Migrator.upgradeDatabase true payrollConnString
    let populatePayrollDb = 
        DeployChanges.To
            .SqlDatabase(payrollConnString, null) 
            .WithScript("PopulateData", 
                "SET IDENTITY_INSERT [dbo].[ElemDefinition] ON 
                GO
                INSERT [dbo].[ElemDefinition] ([ElemDefinitionId], [Code], [DataType]) VALUES (1, N'SalariuBrut', N'System.Decimal')
                GO
                INSERT [dbo].[ElemDefinition] ([ElemDefinitionId], [Code], [DataType]) VALUES (2, N'Impozit', N'System.Decimal')
                GO
                INSERT [dbo].[ElemDefinition] ([ElemDefinitionId], [Code], [DataType]) VALUES (3, N'SalariuNet', N'System.Decimal')
                GO
                SET IDENTITY_INSERT [dbo].[ElemDefinition] OFF
                GO
                SET IDENTITY_INSERT [dbo].[DbElemDefinition] ON 
                GO
                INSERT [dbo].[DbElemDefinition] ([DbElemDefinitionId], [TableName], [ColumnName], [ElemDefinitionId]) VALUES (1, N'Salarii   ', N'Brut      ', 1)
                GO
                SET IDENTITY_INSERT [dbo].[DbElemDefinition] OFF
                GO
                SET IDENTITY_INSERT [dbo].[FromulaElemDefinition] ON 
                GO
                INSERT [dbo].[FromulaElemDefinition] ([FormulaId], [Formula], [ElemDefinitionId]) VALUES (1, N'SalariuBrut * 0.1m', 2)
                GO
                INSERT [dbo].[FromulaElemDefinition] ([FormulaId], [Formula], [ElemDefinitionId]) VALUES (2, N'SalariuBrut - Impozit', 3)
                GO
                SET IDENTITY_INSERT [dbo].[FromulaElemDefinition] OFF
                GO
                SET IDENTITY_INSERT [dbo].[ElemDependency] ON 
                GO
                INSERT [dbo].[ElemDependency] ([ElemDependencyId], [ElemDefinitionId], [DependencyElemDefinitionId]) VALUES (1, 2, 1)
                GO
                INSERT [dbo].[ElemDependency] ([ElemDependencyId], [ElemDefinitionId], [DependencyElemDefinitionId]) VALUES (2, 3, 1)
                GO
                INSERT [dbo].[ElemDependency] ([ElemDependencyId], [ElemDefinitionId], [DependencyElemDefinitionId]) VALUES (3, 3, 2)
                GO
                SET IDENTITY_INSERT [dbo].[ElemDependency] OFF
                GO")    
            .LogToConsole()
            .Build()
            .PerformUpgrade()

    if (not populatePayrollDb.Successful) then raise populatePayrollDb.Error
    
    DropDatabase.For.SqlDatabase(hcmConnectionString);
    EnsureDatabase.For.SqlDatabase(hcmConnectionString);
    let createHcmDb = 
        DeployChanges.To
            .SqlDatabase(hcmConnectionString, null) 
            .WithScript("CreateObjects", 
                "CREATE TABLE [dbo].[Salarii](
            	    [PersonId] [uniqueidentifier] NOT NULL,
            	    [Month] [tinyint] NOT NULL,
            	    [Year] [smallint] NOT NULL,
            	    [Brut] [decimal](18, 0) NOT NULL
                ) ON [PRIMARY]
                GO")
            .WithScript("PopulateData", 
                "INSERT [dbo].[Salarii] ([PersonId], [Month], [Year], [Brut]) VALUES (N'33733a83-d4a9-43c8-ab4e-49c53919217d', 1, 2009, CAST(1000 AS Decimal(18, 0)))
                GO")
            .LogToConsole()
            .Build()
            .PerformUpgrade()

    if (not createHcmDb.Successful) then raise createHcmDb.Error

    let query : EvaluateMultipleCodes.Query = 
        { ElemCodes = ["SalariuNet"; "Impozit"]; PersonId = Guid.Parse("33733a83-d4a9-43c8-ab4e-49c53919217d"); Year=2009; Month=1;}

    let sideEffectMediator = makeSideEffectMediatorDecorator (MockSideEffectMediator()) [
                FormulaParser.parse                                                     |> toHandlerReg;
                ElemDefinitionStoreRepo.loadCurrent payrollConnString                   |> toHandlerReg;
                DbElemValue.loadValue hcmConnectionString                               |> toHandlerReg;
            ]

    let interpreter = Interpreter(sideEffectMediator)

    let eff = EvaluateMultipleCodes.handler query

    // Act
    let result = eff |> Effect.interpret interpreter |> Async.RunSynchronously

    // Assert  
    result |> should equal (Ok ([900m :> obj; 100m :> obj]) : Result<obj list, string>)
    