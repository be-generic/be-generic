CREATE TABLE [gba].[EntityRelation] (
    [EntityRelationId]             UNIQUEIDENTIFIER CONSTRAINT [DF_EntityRelation_EntityRelationId] DEFAULT (newid()) NOT NULL,
    [CrossTableName]               NVARCHAR (MAX)   NOT NULL,
    [Entity1Id]                    UNIQUEIDENTIFIER NOT NULL,
    [Entity1ReferencingColumnName] NVARCHAR (MAX)   NOT NULL,
    [Entity1PropertyName]          NVARCHAR (MAX)   NULL,
    [Entity2Id]                    UNIQUEIDENTIFIER NOT NULL,
    [Entity2ReferencingColumnName] NVARCHAR (MAX)   NOT NULL,
    [Entity2PropertyName]          NVARCHAR (MAX)   NULL,
    [ValidFromColumnName]          NVARCHAR (MAX)   NULL,
    [ValidToColumnName]            NVARCHAR (MAX)   NULL,
    [ActiveColumnName]             NVARCHAR (MAX)   NULL,
    [ShowInEntity1]             BIT              CONSTRAINT [DF_EntityRelation_ShowInEntity1] DEFAULT ((1)) NOT NULL,
    [ShowInEntity1Min]             BIT              CONSTRAINT [DF_EntityRelation_ShowInEntity1Min] DEFAULT ((0)) NOT NULL,
    [ShowInEntity2]             BIT              CONSTRAINT [DF_EntityRelation_ShowInEntity2] DEFAULT ((1)) NOT NULL,
    [ShowInEntity2Min]             BIT              CONSTRAINT [DF_EntityRelation_ShowInEntity2Min] DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_EntityRelation] PRIMARY KEY CLUSTERED ([EntityRelationId] ASC),
    CONSTRAINT [FK_EntityRelation_Entities_1] FOREIGN KEY ([Entity1Id]) REFERENCES [gba].[Entities] ([EntityId]),
    CONSTRAINT [FK_EntityRelation_Entities_2] FOREIGN KEY ([Entity2Id]) REFERENCES [gba].[Entities] ([EntityId])
);

