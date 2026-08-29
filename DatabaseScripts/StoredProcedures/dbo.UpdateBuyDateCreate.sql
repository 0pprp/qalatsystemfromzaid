CREATE proc [dbo].[UpdateBuyDateCreate]
@BuyID int = NULL,
@NewDate datetime
as
update Buys set DateCreate=@NewDate,DateModify=GETUTCDATE() where BuyID=@BuyID

