CREATE proc [dbo].[GetBuysByStoreID]
@StoreID nvarchar(255)
as
SELECT   * from View_Buys

where  StoreID=@StoreID

