


CREATE proc [dbo].[UpdateStoreStateServer]
@StoreID int =null,
@State bit = null
as
update Stores set State=@State where StoreID=@StoreID

