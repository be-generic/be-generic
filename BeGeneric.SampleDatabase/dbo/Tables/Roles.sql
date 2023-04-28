CREATE TABLE [dbo].[Roles] (
    [Id]              UNIQUEIDENTIFIER CONSTRAINT [DF_Roles_Id] DEFAULT (newid()) NOT NULL,
    [RoleName]        NVARCHAR (100)   NULL,
    [RoleDescription] NVARCHAR (MAX)   NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id] ASC)
);

