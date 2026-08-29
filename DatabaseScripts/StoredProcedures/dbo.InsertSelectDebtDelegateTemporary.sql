CREATE proc [dbo].[InsertSelectDebtDelegateTemporary]
@CityID int = NULL,
@DelegateID int = NULL,
@BoxID int = NULL,
@UserID int = NULL,
@TypeDocument nvarchar(255) = NULL,
@Amount float = NULL,
@DateDocument datetime = NULL
as

 
INSERT INTO [dbo].[SelectDebtDelegateTemporaries]
           ([CityID]
           ,[DelegateID]
           ,[BoxID]
           ,[UserID]
           ,[TypeDocument]
           ,[Amount]
           ,[DateDocument] )
     VALUES
           (@CityID 
           ,@DelegateID 
           ,@BoxID 
           ,@UserID 
           ,@TypeDocument 
           ,@Amount 
           ,@DateDocument )
 


