USE [Hcm]
GO

/****** Object:  Table [dbo].[Salarii]    Script Date: 2/26/2020 2:12:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Salarii](
	[PersonId] [uniqueidentifier] NOT NULL,
	[Month] [tinyint] NOT NULL,
	[Year] [smallint] NOT NULL,
	[Brut] [decimal](18, 0) NOT NULL
) ON [PRIMARY]
GO
