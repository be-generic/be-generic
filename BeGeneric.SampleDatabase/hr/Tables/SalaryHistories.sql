CREATE TABLE [hr].[SalaryHistories] (
    [SalaryHistoryID] INT             IDENTITY (1, 1) NOT NULL,
    [EmployeeID]      INT             NOT NULL,
    [OldSalary]       DECIMAL (10, 2) NOT NULL,
    [NewSalary]       DECIMAL (10, 2) NOT NULL,
    [EffectiveDate]   DATE            NOT NULL,
    PRIMARY KEY CLUSTERED ([SalaryHistoryID] ASC),
    FOREIGN KEY ([EmployeeID]) REFERENCES [hr].[Employees] ([EmployeeID])
);

