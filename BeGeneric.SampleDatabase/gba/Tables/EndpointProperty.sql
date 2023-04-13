CREATE TABLE [gba].[EndpointProperty] (
    [EndpointPropertyId] UNIQUEIDENTIFIER CONSTRAINT [DF_EndpointProperty_EndpointPropertyId] DEFAULT (newid()) NOT NULL,
    [EndpointId]         UNIQUEIDENTIFIER NOT NULL,
    [PropertyName]       NVARCHAR (500)   NOT NULL,
    [PropertyPath]       NVARCHAR (2000)  NULL,
    CONSTRAINT [PK_EndpointProperty] PRIMARY KEY CLUSTERED ([EndpointPropertyId] ASC),
    CONSTRAINT [FK_EndpointProperty_Endpoint] FOREIGN KEY ([EndpointId]) REFERENCES [gba].[Endpoint] ([EndpointId])
);

