CREATE proc [dbo].[GetDelegatesByID]
@DelegateID int = NULL
as
select * from View_Delegates  
where DelegateID=@DelegateID

