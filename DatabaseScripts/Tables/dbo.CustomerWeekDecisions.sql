CREATE TABLE [dbo].[CustomerWeekDecisions] (
    [DecisionID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NOT NULL,
    [UserID] INT NOT NULL,
    [DecisionType] NVARCHAR(50) NOT NULL,
    [WeekPaid] FLOAT NULL,
    [AmountTotalSales] FLOAT NULL,
    [PaidPercent] FLOAT NULL,
    [WeekStartDate] DATE NULL,
    [WeekEndDate] DATE NULL,
    [SnoozeUntil] DATETIME NULL,
    [Note] NVARCHAR(MAX) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_CustomerWeekDecisions] PRIMARY KEY CLUSTERED ([DecisionID] ASC),
    CONSTRAINT [FK_CustomerWeekDecisions_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_CustomerWeekDecisions_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
