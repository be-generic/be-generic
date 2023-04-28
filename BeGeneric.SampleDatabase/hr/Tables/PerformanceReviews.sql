CREATE TABLE [hr].[PerformanceReviews] (
    [PerformanceReviewID] INT            IDENTITY (1, 1) NOT NULL,
    [EmployeeID]          INT            NOT NULL,
    [ReviewerID]          INT            NOT NULL,
    [ReviewDate]          DATE           NOT NULL,
    [ReviewText]          VARCHAR (1000) NOT NULL,
    PRIMARY KEY CLUSTERED ([PerformanceReviewID] ASC),
    FOREIGN KEY ([EmployeeID]) REFERENCES [hr].[Employees] ([EmployeeID]),
    FOREIGN KEY ([ReviewerID]) REFERENCES [hr].[Employees] ([EmployeeID])
);

