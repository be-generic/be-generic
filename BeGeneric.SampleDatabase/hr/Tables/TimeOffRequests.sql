CREATE TABLE [hr].[TimeOffRequests] (
    [TimeOffRequestID] INT            IDENTITY (1, 1) NOT NULL,
    [EmployeeID]       INT            NOT NULL,
    [StartTime]        DATETIME       NOT NULL,
    [EndTime]          DATETIME       NOT NULL,
    [Reason]           VARCHAR (1000) NOT NULL,
    PRIMARY KEY CLUSTERED ([TimeOffRequestID] ASC),
    FOREIGN KEY ([EmployeeID]) REFERENCES [hr].[Employees] ([EmployeeID])
);

