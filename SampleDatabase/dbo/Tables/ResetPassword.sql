CREATE TABLE [dbo].[ResetPassword] (
    [Id]       UNIQUEIDENTIFIER CONSTRAINT [DF_ResetPassword_Id] DEFAULT (newid()) NOT NULL,
    [Username] NVARCHAR (100)   NOT NULL,
    [Expires]  DATETIME2 (7)    NOT NULL,
    [CodeHash] VARBINARY (500)  NOT NULL,
    CONSTRAINT [PK_ResetPassword] PRIMARY KEY CLUSTERED ([Id] ASC)
);

