create   view [dbo].[View_Documents]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users)
    SELECT        dbo.Documents.DocumentID, dbo.Documents.UserID, dbo.Documents.FromAccount, dbo.Documents.ToAccount, dbo.Documents.DocumentDateCreate, dbo.Documents.DocumentDateModify, dbo.Documents.Notes, 
                              dbo.Documents.DocumentType, dbo.Documents.Amount, dbo.Documents.DocumentState, dbo.Documents.AsyncState, dbo.Documents.AsyncID, UserData_1.UserName, ISNULL(dbo.Documents.Amount * 1448, 0) 
                              AS AmountDenar
     FROM            dbo.Documents LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.Documents.UserID = UserData_1.UserID

