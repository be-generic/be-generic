CREATE TABLE [dbo].[Accounts] (
    [Username]     NVARCHAR (100)   NOT NULL,
    [EmailAddress] NVARCHAR (500)   NULL,
    [PasswordHash] VARBINARY (500)  NULL,
    [RoleId]       UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY CLUSTERED ([Username] ASC),
    CONSTRAINT [FK_Accounts_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE
);

