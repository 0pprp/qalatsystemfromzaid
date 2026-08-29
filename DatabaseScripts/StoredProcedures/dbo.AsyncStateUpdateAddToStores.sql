
CREATE proc [dbo].[AsyncStateUpdateAddToStores]
@AddToStoreID int = NULL
as
update AddToStores set AsyncState='true' where AddToStoreID=@AddToStoreID

