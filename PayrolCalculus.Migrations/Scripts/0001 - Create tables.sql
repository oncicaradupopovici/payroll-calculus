/****** Object:  Table [dbo].[DbElemDefinition]    Script Date: 2/26/2020 2:12:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON 
GO
CREATE TABLE [dbo].[DbElemDefinition](
	[DbElemDefinitionId] [int] IDENTITY(1,1) NOT NULL,
	[Table] [nchar](10) NOT NULL,
	[Column] [nchar](10) NOT NULL,
	[ElemDefinitionId] [int] NOT NULL,
 CONSTRAINT [PK_DbElemDefinition] PRIMARY KEY CLUSTERED 
(
	[DbElemDefinitionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ElemDefinition]    Script Date: 2/26/2020 2:12:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ElemDefinition](
	[ElemDefinitionId] [int] IDENTITY(1,1) NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[DataType] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK_ElemDefinition] PRIMARY KEY CLUSTERED 
(
	[ElemDefinitionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FormulaDependency]    Script Date: 2/26/2020 2:12:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FormulaDependency](
	[FormulaDependencyId] [int] IDENTITY(1,1) NOT NULL,
	[FormulaId] [int] NOT NULL,
	[ElemDefinitionId] [int] NOT NULL,
 CONSTRAINT [PK_FormulaDependency] PRIMARY KEY CLUSTERED 
(
	[FormulaDependencyId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FromulaElemDefinition]    Script Date: 2/26/2020 2:12:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FromulaElemDefinition](
	[FormulaId] [int] IDENTITY(1,1) NOT NULL,
	[Formula] [nvarchar](500) NOT NULL,
	[ElemDefinitionId] [int] NOT NULL,
 CONSTRAINT [PK_FromulaElemDefinition] PRIMARY KEY CLUSTERED 
(
	[FormulaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[VW_ElemDefinitions]    Script Date: 2/26/2020 2:12:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [dbo].[VW_ElemDefinitions] AS
	select 
		ed.Code, 
		ed.DataType, 
		(CASE 
			WHEN ded.DbElemDefinitionId IS NOT NULL THEN 'Db' 
			WHEN fed.FormulaId IS NOT NULL THEN 'Formula' END) as [Type],
		ded.[Table], 
		ded.[Column], 
		fed.Formula,
		STUFF((
			select ';'+ ed1.Code 
				from dbo.FormulaDependency fd
				inner join dbo.ElemDefinition ed1 on fd.ElemDefinitionId = ed1.ElemDefinitionId
				where fd.FormulaId = fed.FormulaId
			FOR XML PATH('')
			),1,1,'') as FormulaDeps
	from dbo.ElemDefinition ed
	left join dbo.DbElemDefinition ded on ed.ElemDefinitionId = ded.ElemDefinitionId
	left join dbo.FromulaElemDefinition fed on ed.ElemDefinitionId = fed.ElemDefinitionId
GO
ALTER TABLE [dbo].[DbElemDefinition]  WITH CHECK ADD  CONSTRAINT [FK_DbElemDefinition_ElemDefinition] FOREIGN KEY([ElemDefinitionId])
REFERENCES [dbo].[ElemDefinition] ([ElemDefinitionId])
GO
ALTER TABLE [dbo].[DbElemDefinition] CHECK CONSTRAINT [FK_DbElemDefinition_ElemDefinition]
GO
ALTER TABLE [dbo].[FormulaDependency]  WITH CHECK ADD  CONSTRAINT [FK_FormulaDependency_ElemDefinition] FOREIGN KEY([ElemDefinitionId])
REFERENCES [dbo].[ElemDefinition] ([ElemDefinitionId])
GO
ALTER TABLE [dbo].[FormulaDependency] CHECK CONSTRAINT [FK_FormulaDependency_ElemDefinition]
GO
ALTER TABLE [dbo].[FormulaDependency]  WITH CHECK ADD  CONSTRAINT [FK_FormulaDependency_FromulaElemDefinition] FOREIGN KEY([FormulaId])
REFERENCES [dbo].[FromulaElemDefinition] ([FormulaId])
GO
ALTER TABLE [dbo].[FormulaDependency] CHECK CONSTRAINT [FK_FormulaDependency_FromulaElemDefinition]
GO
ALTER TABLE [dbo].[FromulaElemDefinition]  WITH CHECK ADD  CONSTRAINT [FK_FromulaElemDefinition_ElemDefinition] FOREIGN KEY([ElemDefinitionId])
REFERENCES [dbo].[ElemDefinition] ([ElemDefinitionId])
GO
ALTER TABLE [dbo].[FromulaElemDefinition] CHECK CONSTRAINT [FK_FromulaElemDefinition_ElemDefinition]
GO
