create proc [dbo].[TransferBoxs_Delete]   
@TransferBoxID int = NULL,
@UserID int = NULL
as
exec DeleteTransferBoxIDAddToBoxAsyncID @TransferBoxID=@TransferBoxID
exec DeleteTransferBoxIDWithdrawalFromBoxAsyncID @TransferBoxID=@TransferBoxID
exec DeleteTransferBoxIDTransferBoxsAsyncID @TransferBoxID=@TransferBoxID

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المبلغ '+(select CONVERT(nvarchar(255),AmountDenar) from View_TransferBoxs where TransferBoxID=@TransferBoxID)+N' المحول من الخزينة '+(select FromBoxName from View_TransferBoxs where TransferBoxID=@TransferBoxID)+N' الى خزينة '+(select ToBoxName from View_TransferBoxs where TransferBoxID=@TransferBoxID)+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from AddToBox where TransferBoxID=@TransferBoxID
delete from WithdrawalFromBox where TransferBoxID=@TransferBoxID
delete from TransferBoxs  where TransferBoxID=@TransferBoxID

 

