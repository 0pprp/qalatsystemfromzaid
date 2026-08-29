CREATE proc [dbo].[GetDelegateAsyncID]
@DelegateID int = NULL
as
select top 1 AsyncID from Delegates where DelegateID=@DelegateID

