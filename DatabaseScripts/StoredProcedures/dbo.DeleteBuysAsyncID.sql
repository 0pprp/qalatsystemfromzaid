CREATE proc [dbo].[DeleteBuysAsyncID]
@BuyID int = NULL
as
Insert into DeleteData (BuysAsyncID) values ((select AsyncID from Buys where BuyID=@BuyID))


