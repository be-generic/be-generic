CREATE TABLE [hr].[EmployeeJobTitles] (
    [EmployeeJobTitleID] INT  IDENTITY (1, 1) NOT NULL,
    [EmployeeID]         INT  NOT NULL,
    [JobTitleID]         INT  NOT NULL,
    [StartDate]          DATE NOT NULL,
    [EndDate]            DATE NULL,
    PRIMARY KEY CLUSTERED ([EmployeeJobTitleID] ASC),
    FOREIGN KEY ([EmployeeID]) REFERENCES [hr].[Employees] ([EmployeeID]),
    FOREIGN KEY ([JobTitleID]) REFERENCES [hr].[JobTitles] ([JobTitleID])
);

