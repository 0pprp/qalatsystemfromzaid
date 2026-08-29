
CREATE proc [dbo].[AsyncStateUpdateSelectItemsAddToStores]
@SelectItemAddToStoreID int = NULL
as
update SelectItemsAddToStores set AsyncState='true' where SelectItemAddToStoreID=@SelectItemAddToStoreID

