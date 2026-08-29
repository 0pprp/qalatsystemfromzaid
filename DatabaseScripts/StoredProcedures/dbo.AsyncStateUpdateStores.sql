
CREATE proc [dbo].[AsyncStateUpdateStores]
@StoreID int = NULL
as
update Stores set AsyncState='true' where StoreID=@StoreID

