CREATE TABLE [hr].[Employees] (
    [EmployeeID]   INT             IDENTITY (1, 1) NOT NULL,
    [FirstName]    VARCHAR (50)    NOT NULL,
    [LastName]     VARCHAR (50)    NOT NULL,
    [Email]        VARCHAR (100)   NOT NULL,
    [Phone]        VARCHAR (20)    NULL,
    [HireDate]     DATE            NOT NULL,
    [Salary]       DECIMAL (10, 2) NOT NULL,
    [DepartmentID] INT             NOT NULL,
    PRIMARY KEY CLUSTERED ([EmployeeID] ASC),
    FOREIGN KEY ([DepartmentID]) REFERENCES [hr].[Departments] ([DepartmentID]),
    UNIQUE NONCLUSTERED ([Email] ASC)
);

