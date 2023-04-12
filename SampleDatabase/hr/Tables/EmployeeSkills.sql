CREATE TABLE [hr].[EmployeeSkills] (
    [EmployeeSkillID] INT IDENTITY (1, 1) NOT NULL,
    [EmployeeID]      INT NOT NULL,
    [SkillID]         INT NOT NULL,
    [SkillLevel]      INT NOT NULL,
    PRIMARY KEY CLUSTERED ([EmployeeSkillID] ASC),
    FOREIGN KEY ([EmployeeID]) REFERENCES [hr].[Employees] ([EmployeeID]),
    FOREIGN KEY ([SkillID]) REFERENCES [hr].[Skills] ([SkillID])
);

