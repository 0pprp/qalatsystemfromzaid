
CREATE proc [dbo].[AsyncStateUpdateBuys]
@BuyID int = NULL
as
update Buys set AsyncState='true' where BuyID=@BuyID

