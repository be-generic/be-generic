CREATE TABLE [hr].[JobTitles] (
    [JobTitleID]     INT            IDENTITY (1, 1) NOT NULL,
    [JobTitleName]   VARCHAR (50)   NOT NULL,
    [JobDescription] VARCHAR (1000) NULL,
    PRIMARY KEY CLUSTERED ([JobTitleID] ASC)
);

