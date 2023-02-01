CREATE TABLE [gba].[EntityRole] (
    [EntitiesEntityId] UNIQUEIDENTIFIER CONSTRAINT [DF_EntityRole_EntitiesEntityId] DEFAULT (newid()) NOT NULL,
    [RolesId]          UNIQUEIDENTIFIER NOT NULL,
    [GetOne]           BIT              CONSTRAINT [DF_EntityRole_GetOne] DEFAULT ((1)) NOT NULL,
    [GetAll]           BIT              CONSTRAINT [DF_EntityRole_GetAll] DEFAULT ((1)) NOT NULL,
    [Post]             BIT              CONSTRAINT [DF_EntityRole_Post] DEFAULT ((1)) NOT NULL,
    [Put]              BIT              CONSTRAINT [DF_EntityRole_Put] DEFAULT ((1)) NOT NULL,
    [Delete]           BIT              CONSTRAINT [DF_EntityRole_Delete] DEFAULT ((1)) NOT NULL,
    [ViewFilter]       NVARCHAR (MAX)   NULL,
    [EditFilter]       NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_EntityRole] PRIMARY KEY CLUSTERED ([EntitiesEntityId] ASC, [RolesId] ASC),
    CONSTRAINT [FK_EntityRole_Entities_EntitiesEntityId] FOREIGN KEY ([EntitiesEntityId]) REFERENCES [gba].[Entities] ([EntityId]) ON DELETE CASCADE,
    CONSTRAINT [FK_EntityRole_Roles_RolesId] FOREIGN KEY ([RolesId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE
);

