CREATE proc [dbo].[DeleteSelectItemsAddToStoresTemporary]
@SelectItemAddToStoreTemporaryID int  = NULL
as
delete from SelectItemsAddToStoresTemporary
where  SelectItemAddToStoreTemporaryID=@SelectItemAddToStoreTemporaryID
 


