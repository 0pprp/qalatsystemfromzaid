
create   proc [dbo].[GetBuyByBuyID]
@BuyID int
as
select * from Buys where BuyID=@BuyID

