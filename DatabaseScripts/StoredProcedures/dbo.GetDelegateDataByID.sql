CREATE proc [dbo].[GetDelegateDataByID]
@DelegateID int = NULL
as
select * from Delegates where DelegateID=@DelegateID

