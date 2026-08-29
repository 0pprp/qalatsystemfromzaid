create proc [dbo].[WithdrawalFromBox_Delete]
@WithdrawalFromBoxID int = NULL,
@UserID int = NULL
as
exec DeleteOneWithdrawalFromBoxAsyncID @WithdrawalFromBoxID=@WithdrawalFromBoxID

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المبلغ '+(select CONVERT(nvarchar(255),AmountDenar) from View_WithdrawalFromBox where WithdrawalFromBoxID=@WithdrawalFromBoxID)+N' المسحوب من الخزينة '+(select BoxName from View_WithdrawalFromBox where WithdrawalFromBoxID=@WithdrawalFromBoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from WithdrawalFromBox where WithdrawalFromBoxID=@WithdrawalFromBoxID

 

