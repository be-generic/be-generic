CREATE TABLE [gba].[Properties] (
    [PropertyId]                      UNIQUEIDENTIFIER CONSTRAINT [DF_Properties_PropertyId] DEFAULT (newid()) NOT NULL,
    [PropertyName]                    NVARCHAR (MAX)   NULL,
    [ModelPropertyName]               NVARCHAR (MAX)   NULL,
    [EntityId]                        UNIQUEIDENTIFIER NOT NULL,
    [IsKey]                           BIT              NOT NULL,
    [ReferencingEntityId]             UNIQUEIDENTIFIER NULL,
    [RelatedModelPropertyName]        NVARCHAR (500)   NULL,
    [IsReadOnly]                      BIT              CONSTRAINT [DF_Properties_IsReadOnly] DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_Properties] PRIMARY KEY CLUSTERED ([PropertyId] ASC),
    CONSTRAINT [FK_Properties_Entities_EntityId] FOREIGN KEY ([EntityId]) REFERENCES [gba].[Entities] ([EntityId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Properties_Entities_ReferencingEntityId] FOREIGN KEY ([ReferencingEntityId]) REFERENCES [gba].[Entities] ([EntityId])
);

