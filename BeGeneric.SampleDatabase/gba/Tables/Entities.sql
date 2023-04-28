CREATE TABLE [gba].[Entities] (
    [EntityId]         UNIQUEIDENTIFIER CONSTRAINT [DF_Entities_EntityId] DEFAULT (newid()) NOT NULL,
    [ControllerName]   NVARCHAR (MAX)   NULL,
    [TableName]        NVARCHAR (MAX)   NULL,
    [ObjectName]       NVARCHAR (MAX)   NULL,
    [SoftDeleteColumn] NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_Entities] PRIMARY KEY CLUSTERED ([EntityId] ASC)
);

