CREATE TABLE [gba].[Endpoint] (
    [EndpointId]               UNIQUEIDENTIFIER CONSTRAINT [DF_Endpoint_EndpointId] DEFAULT (newid()) NOT NULL,
    [EndpointPath]             NVARCHAR (500)   NOT NULL,
    [StartingEntityId]         UNIQUEIDENTIFIER NOT NULL,
    [RoleId]                   UNIQUEIDENTIFIER NULL,
    [Filter]                   NVARCHAR (MAX)   NULL,
    [DefaultPageNumber]        INT              NULL,
    [DefaultPageSize]          INT              NULL,
    [DefaultSortOrderProperty] NVARCHAR (100)   NULL,
    [DefaultSortOrder]         NVARCHAR (10)    NULL,
    CONSTRAINT [PK_Endpoint] PRIMARY KEY CLUSTERED ([EndpointId] ASC),
    CONSTRAINT [FK_Endpoint_Entities] FOREIGN KEY ([StartingEntityId]) REFERENCES [gba].[Entities] ([EntityId]),
    CONSTRAINT [FK_Endpoint_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([Id])
);

