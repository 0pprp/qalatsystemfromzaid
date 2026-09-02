CREATE TABLE [dbo].[SalesCustomerRatings] (
    [RatingID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NOT NULL,
    [CustomerID] INT NULL,
    [CustomerName] NVARCHAR(200) NULL,
    [PhoneNumber] NVARCHAR(50) NULL,
    [RatingLevel] INT NOT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [RejectionReason] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_SalesCustomerRatings_CreatedAt] DEFAULT (GETDATE()),
    CONSTRAINT [PK_SalesCustomerRatings] PRIMARY KEY CLUSTERED ([RatingID] ASC)
);
GO
