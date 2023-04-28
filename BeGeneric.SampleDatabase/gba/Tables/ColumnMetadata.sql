CREATE TABLE [gba].[ColumnMetadata] (
    [TableName]     NVARCHAR (450) NOT NULL,
    [ColumnName]    NVARCHAR (450) NOT NULL,
    [AllowedValues] NVARCHAR (MAX) NULL,
    [Regex]         NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_ColumnMetadata] PRIMARY KEY CLUSTERED ([TableName] ASC, [ColumnName] ASC)
);

