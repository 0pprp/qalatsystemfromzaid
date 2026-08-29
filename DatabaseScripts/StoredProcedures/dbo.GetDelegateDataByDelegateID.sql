CREATE proc [dbo].[GetDelegateDataByDelegateID]
@DelegateID int = NULL
as
select * from Delegates where DelegateID=@DelegateID

