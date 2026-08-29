
CREATE proc [dbo].[AsyncStateUpdateBuysItems]
@BuyItemID int = NULL
as
update BuysItems set AsyncState='true' where BuyItemID=@BuyItemID

