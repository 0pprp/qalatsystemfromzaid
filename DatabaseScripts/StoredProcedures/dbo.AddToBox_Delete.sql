create proc [dbo].[AddToBox_Delete]
@AddToBoxID int = NULL,
@UserID int = NULL
as
declare @CustomerPaymentID int = (select CustomerPaymentID from AddToBox where AddToBoxID=@AddToBoxID)
exec CustomersPayments_Delete @CustomerPaymentID = @CustomerPaymentID ,@UserDeleteID = @UserID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المبلغ '+(select CONVERT(nvarchar(255),AmountDenar) from View_AddToBox where AddToBoxID=@AddToBoxID)+N' من الخزينة '+(select BoxName from View_AddToBox where AddToBoxID=@AddToBoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
exec DeleteAddToBoxAsyncID @AddToBoxID=@AddToBoxID
delete from AddToBox  where AddToBoxID=@AddToBoxID


