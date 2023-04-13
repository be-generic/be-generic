CREATE TABLE [hr].[Departments] (
    [DepartmentID]   INT          IDENTITY (1, 1) NOT NULL,
    [DepartmentName] VARCHAR (50) NOT NULL,
    [ManagerID]      INT          NULL,
    PRIMARY KEY CLUSTERED ([DepartmentID] ASC),
    FOREIGN KEY ([ManagerID]) REFERENCES [hr].[Employees] ([EmployeeID])
);

