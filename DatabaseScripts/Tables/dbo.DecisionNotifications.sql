CREATE TABLE [dbo].[DecisionNotifications] (
    [NotificationID] INT IDENTITY(1,1) NOT NULL,
    [DecisionID] INT NOT NULL,
    [IsRead] BIT NOT NULL DEFAULT (0),
    [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_DecisionNotifications] PRIMARY KEY CLUSTERED ([NotificationID] ASC),
    CONSTRAINT [FK_DecisionNotifications_Decisions] FOREIGN KEY ([DecisionID]) REFERENCES [dbo].[CustomerWeekDecisions] ([DecisionID])
);
